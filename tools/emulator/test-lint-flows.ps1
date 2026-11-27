<#
.SYNOPSIS
  Self-test for lint-flows.ps1 (Slice 207): each rule against a small fixture under fixtures/lint, plus the real .maestro passing clean.
  Run it in both `powershell` (5.1) and `pwsh`. Needs no device.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$lint =Join-Path $PSScriptRoot 'lint-flows.ps1'
$fixtures = Join-Path $PSScriptRoot 'fixtures\lint'
$failed = 0

# Runs the lint in this process (a script's exit sets $LASTEXITCODE only); returns its exit code and the finding lines.
function Invoke-Lint([string]$Path, [string]$Manifest, [string]$SourceRoot = (Join-Path $PSScriptRoot 'fixtures\lint-src')) {
    $lintArgs = @{ Path = $Path; SourceRoot = $SourceRoot; CheckSource = $true }   # Slice 406 item 3: the fixtures' markers are checked against fixtures\lint-src, the real flows against src
    if ($Manifest) { $lintArgs.Manifest = $Manifest }
    $out = @(& $lint @lintArgs 6>&1 | ForEach-Object { "$_" })
    return [pscustomobject]@{ Exit = $LASTEXITCODE; Findings = @($out | Where-Object { $_ -notmatch '^lint-flows:' -and $_ -notmatch ': warning: ' }) }
}

function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

# Expects the fixture to report exactly these (line, text) findings, in order, and exit 1; no findings means exit 0.
function Assert-Findings([string]$Fixture, $Expected, [string]$Manifest, [string]$SourceRoot = (Join-Path $PSScriptRoot 'fixtures\lint-src')) {
    $result = Invoke-Lint (Join-Path $fixtures $Fixture) $Manifest $SourceRoot
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
Assert-Findings 'launch-hook-restart.yaml' @(New-Expect 6 'stopApp: false')
Assert-Findings 'launch-hook-ok.yaml' @()
Assert-Findings 'launch-hook-inline.yaml' @(New-Expect 4 'stopApp: false')
Assert-Findings 'launch-hook-plain-first.yaml' @(New-Expect 4 'stopApp: false')
Assert-Findings 'launch-hook-comment.yaml' @()
Assert-Findings 'bad-appid.yaml' @(New-Expect 1 'is not a dev package')
Assert-Findings 'plain-dino-appid.yaml' @(New-Expect 1 'is not a dev package')  # Slice 221: the store id is never a flow target
Assert-Findings 'no-separator.yaml' @(New-Expect 1 'no --- line')
Assert-Findings 'no-appid.yaml' @(New-Expect 2 'the header has no appId')
Assert-Findings 'needs-ok.yaml' @()
Assert-Findings 'needs-stub-ok.yaml' @()   # Slice 320 item 5: report-stub and crash-stub are known needs
Assert-Findings 'needs-two-stubs.yaml' @(New-Expect 1 'is a second host stub')   # Slice 320 check 1 #3: both stubs use port 8787
Assert-Findings 'needs-unknown.yaml' @((New-Expect 1 "'# needs: wifi-only' is not a known value"), (New-Expect 2 "'# needs: offline' is not a known value"))
Assert-Findings 'expect-log-ok.yaml' @()
Assert-Findings 'expect-log-bad-start.yaml' @(New-Expect 2 'must start a token')   # Slice 315 item 15: the incident marker ': failed:network'
Assert-Findings 'expect-log-empty.yaml' @(New-Expect 2 'is empty')
Assert-Findings 'expect-log-missing-source.yaml' @(New-Expect 2 'appears nowhere in the app source')   # Slice 406 item 3: a reworded marker the source no longer logs
Assert-Findings 'expect-log-missing-source.yaml' @() '' (Join-Path $fixtures 'no-such-src')   # no source folder switches the rule off
# Review #2: the old check looked only at the first two fixed words; '[Reading] animal zebra clip Pig plays' passed it ('[Reading]' and 'animal' exist) and must fail now ('zebra' is nowhere in the source).
Assert-Findings 'expect-log-two-words.yaml' @(New-Expect 2 'appears nowhere in the app source')
Assert-Findings 'expect-log-source-kinds.yaml' @()   # a marker logged by a Swift file, a literal cut at its first value, and one starting with a value (a warning, not a finding)
Assert-Findings 'expect-log-escapes.yaml' @()   # Slice 406 item 3d: a marker in a C# verbatim string and one in a Kotlin-escaped string are found
# Slice 406 item 3c: several flows joined with ';' are linted in one call, markers checked against the source.
$multi = Invoke-Lint ((Join-Path $fixtures 'expect-log-ok.yaml') + ';' + (Join-Path $fixtures 'expect-log-missing-source.yaml')) ''
Assert-Equal 'two flows in one call: the one with a missing marker is reported' 1 @($multi.Findings | Where-Object { $_ -match 'expect-log-missing-source.*appears nowhere' }).Count
Assert-Equal 'two flows in one call: exit 1' 1 $multi.Exit
# Slice 406 item 3e: a source root the walk cannot enumerate (here a file, which throws an IOException) is skipped, never an abort: the rule switches off and the lint ends 0.
$rootIsFile = Invoke-Lint (Join-Path $fixtures 'expect-log-missing-source.yaml') '' (Join-Path $fixtures 'expect-log-ok.yaml')
Assert-Equal 'an unreadable source root does not abort the lint' 0 $rootIsFile.Exit
# Without -CheckSource a single flow is not checked against the source (run-flows.ps1's per-flow calls stay fast); a folder always is.
$single = @(& $lint -Path (Join-Path $fixtures 'expect-log-missing-source.yaml') -SourceRoot (Join-Path $PSScriptRoot 'fixtures\lint-src') 6>&1 | ForEach-Object { "$_" })
Assert-Equal 'a single flow without -CheckSource skips the source rule' 0 @($single | Where-Object { $_ -match 'appears nowhere' }).Count
$missingSrc = @(& $lint -Path (Join-Path $fixtures 'expect-log-ok.yaml') -SourceRoot (Join-Path $fixtures 'no-such-src') -CheckSource 6>&1 | ForEach-Object { "$_" })
Assert-Equal 'a missing source folder prints a warning' 1 @($missingSrc | Where-Object { $_ -match 'warning: source folder' }).Count
# Review #2: a marker with a quote is written escaped in the C# source (`\"`), and still matches. Review #3: an empty folder or a file path switches the rule off with the warning.
Assert-Findings 'expect-log-escaped.yaml' @()
$emptySrc = Join-Path $fixtures 'empty-src'
New-Item -ItemType Directory -Force -Path $emptySrc | Out-Null
$emptyRun = @(& $lint -Path (Join-Path $fixtures 'expect-log-missing-source.yaml') -SourceRoot $emptySrc -CheckSource 6>&1 | ForEach-Object { "$_" })
Remove-Item -LiteralPath $emptySrc -Force
Assert-Equal 'an empty source folder switches the rule off with a warning' 1 @($emptyRun | Where-Object { $_ -match 'warning: source folder' }).Count
Assert-Equal 'an empty source folder gives no finding' 0 @($emptyRun | Where-Object { $_ -match 'appears nowhere' }).Count
$fileRun = @(& $lint -Path (Join-Path $fixtures 'expect-log-missing-source.yaml') -SourceRoot (Join-Path $PSScriptRoot 'fixtures\lint-src\markers.cs') -CheckSource 6>&1 | ForEach-Object { "$_" })
Assert-Equal 'a file as source root switches the rule off with a warning' 1 @($fileRun | Where-Object { $_ -match 'warning: source folder' }).Count
Assert-Equal 'an empty marker can never match' $false (Test-LogMarkerCanMatch '')
Assert-Equal 'a non-empty marker can match' $true (Test-LogMarkerCanMatch '(cached) hit')

# Slice 215: runFlow files are followed, the inline launchApp form is tokenized, the clearState comment may sit above its item, stopApp: true is a restart.
Assert-Findings 'runflow-launch.yaml' @(New-Expect 5 'stopApp: false')
Assert-Findings 'runflow-inline.yaml' @(New-Expect 4 'stopApp: false')
Assert-Findings 'runflow-block.yaml' @(New-Expect 4 'stopApp: false')
Assert-Findings 'runflow-comment.yaml' @(New-Expect 5 'stopApp: false')
Assert-Findings 'runflow-missing.yaml' @(New-Expect 11 'does not exist')
Assert-Findings 'runflow-ok.yaml' @()
Assert-Findings 'runflow-nested.yaml' @(New-Expect 8 'stopApp: false')
Assert-Findings 'runflow-variable.yaml' @()
Assert-Findings 'launch-inline-quoted.yaml' @(New-Expect 5 'stopApp: false')
Assert-Findings 'clearstate-item-comment.yaml' @()
Assert-Findings 'launch-hook-stopapp-true.yaml' @(New-Expect 4 'stopApp: false')
Assert-Findings 'launch-hook-stopapp-true-comment.yaml' @()

# Slice 376 item 4 (Slice 354 item 3): a takeScreenshot missing from the contact-sheet manifest fails the lint, a listed one passes, and no manifest switches the rule off.
$shotManifest = Join-Path $fixtures '..\shot-manifest.json'
Assert-Findings 'shot-listed.yaml' @() $shotManifest
Assert-Findings 'shot-unlisted.yaml' @(New-Expect 5 "takeScreenshot 'shot-fixture-unlisted' is not in shot-manifest.json") $shotManifest
Assert-Findings 'shot-unlisted.yaml' @() (Join-Path $fixtures 'no-such-manifest.json')
# A trailing comment and a quoted name are read (the quoted unlisted one is reported); the map form is reported, never skipped.
Assert-Findings 'shot-forms.yaml' @((New-Expect 5 "takeScreenshot 'shot-fixture-quoted-unlisted' is not in shot-manifest.json"), (New-Expect 6 "takeScreenshot is not in the plain form")) $shotManifest

# Slice 215: line endings. Written at run time (git would normalize a checked-in fixture): doubled CRs and mixed endings are findings, a clean CRLF or LF file is not.
$endings = Join-Path ([IO.Path]::GetTempPath()) ('rtst-lint-endings-' + [guid]::NewGuid().ToString('N'))
try {
    $null = New-Item -ItemType Directory -Force $endings
    $head = 'appId: uk.sirous.readthestupidtext'
    $cr = "`r"; $lf = "`n"
    $files = @{
        'crlf-ok.yaml' = $head + $cr + $lf + '---' + $cr + $lf + '- launchApp' + $cr + $lf
        'lf-ok.yaml'   = $head + $lf + '---' + $lf + '- launchApp' + $lf
        'doubled.yaml' = $head + $cr + $lf + '---' + $cr + $lf + '- launchApp' + $cr + $cr + $lf
        'mixed.yaml'   = $head + $cr + $lf + '---' + $lf + '- launchApp' + $cr + $lf
    }
    foreach ($name in $files.Keys) { [IO.File]::WriteAllBytes((Join-Path $endings $name), [Text.Encoding]::ASCII.GetBytes($files[$name])) }
    $fixtures = $endings
    Assert-Findings 'crlf-ok.yaml' @()
    Assert-Findings 'lf-ok.yaml' @()
    Assert-Findings 'doubled.yaml' @(New-Expect 3 'doubled CR')
    Assert-Findings 'mixed.yaml' @(New-Expect 2 'mixed line endings')
} finally { Remove-Item $endings -Recurse -Force -ErrorAction SilentlyContinue }

# The real flows pass clean.
$real = Invoke-Lint (Join-Path $PSScriptRoot '..\..\.maestro') '' (Join-Path $PSScriptRoot '..\..\src')
Assert-Equal 'the real .maestro exit code' 0 $real.Exit
Assert-Equal 'the real .maestro findings' 0 $real.Findings.Count
if ($real.Findings.Count) { $real.Findings | ForEach-Object { Write-Host "  $_" } }

# A copy of the real flows with openNotifications fails, naming the line.
$copy = Join-Path ([IO.Path]::GetTempPath()) ('rtst-lint-test-' + [guid]::NewGuid().ToString('N'))
try {
    Copy-Item (Join-Path $PSScriptRoot '..\..\.maestro') $copy -Recurse
    Add-Content (Join-Path $copy 'rtst\speed.yaml') '- openNotifications'
    $broken = Invoke-Lint $copy '' (Join-Path $PSScriptRoot '..\..\src')
    Assert-Equal 'a copy with openNotifications fails' 1 $broken.Exit
    Assert-Equal 'it names the command' $true ([bool]($broken.Findings -match "speed\.yaml:\d+: 'openNotifications'"))
} finally { Remove-Item $copy -Recurse -Force -ErrorAction SilentlyContinue }

if ($failed) { Write-Host "$failed check(s) failed."; exit 1 }
Write-Host "All lint-flows checks pass (PowerShell $($PSVersionTable.PSVersion))."
exit 0
