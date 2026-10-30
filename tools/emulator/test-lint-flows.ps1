<#
.SYNOPSIS
  Self-test for lint-flows.ps1 (Slice 207): each rule against a small fixture under fixtures/lint, plus the real .maestro passing clean.
  Run it in both `powershell` (5.1) and `pwsh`. Needs no device.
#>
$ErrorActionPreference = 'Stop'
$lint = Join-Path $PSScriptRoot 'lint-flows.ps1'
$fixtures = Join-Path $PSScriptRoot 'fixtures\lint'
$failed = 0

# Runs the lint in this process (a script's exit sets $LASTEXITCODE only); returns its exit code and the finding lines.
function Invoke-Lint([string]$Path) {
    $out = @(& $lint -Path $Path 6>&1 | ForEach-Object { "$_" })
    return [pscustomobject]@{ Exit = $LASTEXITCODE; Findings = @($out | Where-Object { $_ -notmatch '^lint-flows:' }) }
}

function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

# Expects the fixture to report exactly these (line, text) findings, in order, and exit 1; no findings means exit 0.
function Assert-Findings([string]$Fixture, $Expected) {
    $result = Invoke-Lint (Join-Path $fixtures $Fixture)
    Assert-Equal "$Fixture exit code" $(if ($Expected.Count) { 1 } else { 0 }) $result.Exit
    Assert-Equal "$Fixture finding count" $Expected.Count $result.Findings.Count
    for ($i = 0; $i -lt [Math]::Min($Expected.Count, $result.Findings.Count); $i++) {
        $line = $result.Findings[$i]
        $prefix = "${Fixture}:$($Expected[$i].Line): "
        if (-not ($line.Contains($prefix) -and $line.Contains($Expected[$i].Text))) { $script:failed++; Write-Host "FAIL: $Fixture finding $i expected '$prefix...$($Expected[$i].Text)' got [$line]" }
    }
}

function New-Expect([int]$Line, [string]$Text) { return @{ Line = $Line; Text = $Text } }

Assert-Findings 'ok.yaml' @()
Assert-Findings 'unknown-command.yaml' @(New-Expect 4 "'openNotifications' is not a Maestro command")
Assert-Findings 'nested-unknown.yaml' @(New-Expect 6 "'swipeAway' is not a Maestro command")
Assert-Findings 'bad-escape.yaml' @(New-Expect 4 "'\+' is not a YAML escape")
Assert-Findings 'clearstate-bare.yaml' @((New-Expect 4 'clearState needs a comment'), (New-Expect 5 'clearState needs a comment'))
Assert-Findings 'clearstate-vague.yaml' @(New-Expect 5 'clearState needs a comment')
Assert-Findings 'header-comment.yaml' @()
Assert-Findings 'clearstate-inline.yaml' @(New-Expect 5 'clearState needs a comment')
Assert-Findings 'bad-appid.yaml' @(New-Expect 1 'is not a dev package')
Assert-Findings 'no-separator.yaml' @(New-Expect 1 'no --- line')
Assert-Findings 'no-appid.yaml' @(New-Expect 2 'the header has no appId')

# The real flows pass clean.
$real = Invoke-Lint (Join-Path $PSScriptRoot '..\..\.maestro')
Assert-Equal 'the real .maestro exit code' 0 $real.Exit
Assert-Equal 'the real .maestro findings' 0 $real.Findings.Count
if ($real.Findings.Count) { $real.Findings | ForEach-Object { Write-Host "  $_" } }

# A copy of the real flows with openNotifications fails, naming the line.
$copy = Join-Path ([IO.Path]::GetTempPath()) ('rtst-lint-test-' + [guid]::NewGuid().ToString('N'))
try {
    Copy-Item (Join-Path $PSScriptRoot '..\..\.maestro') $copy -Recurse
    Add-Content (Join-Path $copy 'rtst\speed.yaml') '- openNotifications'
    $broken = Invoke-Lint $copy
    Assert-Equal 'a copy with openNotifications fails' 1 $broken.Exit
    Assert-Equal 'it names the command' $true ([bool]($broken.Findings -match "speed\.yaml:\d+: 'openNotifications'"))
} finally { Remove-Item $copy -Recurse -Force -ErrorAction SilentlyContinue }

if ($failed) { Write-Host "$failed check(s) failed."; exit 1 }
Write-Host "All lint-flows checks pass (PowerShell $($PSVersionTable.PSVersion))."
