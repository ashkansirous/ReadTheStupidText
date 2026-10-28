<#
.SYNOPSIS
  Runs the Maestro flows of one app (.maestro/<app>/*.yaml) on the warm emulator and prints pass/fail only (Slice 162).
.DESCRIPTION
  Maestro is found in $env:MAESTRO_HOME, else <worktrees folder>\.tools\maestro (shared by all worktrees).
  A flow may start with comment lines `# expect-log: <text>`: after the flow ends, each text must appear in the
  RTST-TEST logcat lines (the Debug-only markers) within -LogTimeoutSeconds. Failures print the last lines of Maestro's output.
  The debug app must already be installed (install-debug.ps1 -App rtst|dino); -Install does that first.
#>
param(
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [string]$Flow,
    [switch]$Install,
    [int]$LogTimeoutSeconds = 40
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$packages = @{ rtst = 'uk.sirous.readthestupidtext'; dino = 'uk.sirous.dinoreader' }
$logTag = 'RTST-TEST'

function Find-Maestro {
    $candidates = @()
    if ($env:MAESTRO_HOME) { $candidates += Join-Path $env:MAESTRO_HOME 'bin\maestro.bat' }
    $candidates += Join-Path (Split-Path $script:StateDir -Parent) '.tools\maestro\bin\maestro.bat'
    $found = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $found) { throw "Maestro not found. Unzip maestro.zip (github.com/mobile-dev-inc/maestro/releases) to $($candidates[-1] -replace '\\bin\\maestro.bat$') or set MAESTRO_HOME." }
    return $found
}

function Get-ExpectedLogs([string]$FlowFile) {
    Get-Content $FlowFile | Where-Object { $_ -match '^#\s*expect-log:\s*(.+)$' } | ForEach-Object { $Matches[1].Trim() }
}

function Get-TestLog {
    $ErrorActionPreference = 'Continue'
    return (& $script:Adb -s $script:Serial logcat -d -s $logTag 2>&1 | Out-String)
}

# Whole-token match: `PAGE 1` must not match `PAGE 10`.
function Test-LogMarker([string]$Log, [string]$Marker) {
    return $Log -match ('(?<!\S)' + [regex]::Escape($Marker) + '(?!\S)')
}

# Returns the expected texts still missing from the marker log after the timeout.
function Wait-LogMarkers([string[]]$Expected) {
    $deadline = (Get-Date).AddSeconds($LogTimeoutSeconds)
    do {
        $log = Get-TestLog
        $missing = @($Expected | Where-Object { -not (Test-LogMarker $log $_) })
        if ($missing.Count -eq 0) { return @() }
        Start-Sleep -Milliseconds 700
    } while ((Get-Date) -lt $deadline)
    return $missing
}

function Invoke-Flow([string]$Maestro, [System.IO.FileInfo]$File) {
    $ErrorActionPreference = 'Continue'
    & $script:Adb -s $script:Serial logcat -c
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $output = & $Maestro --device $script:Serial test --no-reinstall-driver --debug-output $script:MaestroOut $File.FullName 2>&1 | Out-String
    $exit = $LASTEXITCODE
    $problem = $null
    if ($exit -ne 0) {
        $lines = $output -split "`r?`n" | Where-Object { $_.Trim() -and $_ -notmatch '[│╭╰]' }
        $problem = "Maestro failed:`n" + (($lines | Select-Object -Last 10) -join "`n")
    }
    else {
        $missing = Wait-LogMarkers @(Get-ExpectedLogs $File.FullName)
        if ($missing.Count -gt 0) { $problem = "Missing log marker(s) under tag ${logTag}: " + ($missing -join '; ') }
    }
    return [pscustomobject]@{ Name = $File.BaseName; Seconds = $sw.Elapsed.TotalSeconds; Problem = $problem }
}

$maestro = Find-Maestro
$env:MAESTRO_CLI_NO_ANALYTICS = '1'
# Maestro keeps its state and logs under the Java user.home: put them in the shared tools folder, not the user profile.
$toolsDir = Split-Path (Split-Path (Split-Path $maestro -Parent) -Parent) -Parent
$script:MaestroOut = Join-Path $toolsDir 'maestro-out'
$null = New-Item -ItemType Directory -Force $script:MaestroOut, (Join-Path $toolsDir 'maestro-home')
$env:MAESTRO_OPTS = "-Duser.home=$(Join-Path $toolsDir 'maestro-home')"
$env:MAESTRO_CLI_NO_ANALYTICS_NOTIFICATION = '1'
if ($Install) { & (Join-Path $PSScriptRoot 'install-debug.ps1') -App $App }
else { & (Join-Path $PSScriptRoot 'start-warm.ps1') | Out-Null }

$ErrorActionPreference = 'Continue'
$installed = & $script:Adb -s $script:Serial shell pm list packages $packages[$App]
$ErrorActionPreference = 'Stop'
if (-not ("$installed" -match [regex]::Escape($packages[$App]))) { throw "$($packages[$App]) is not installed; run install-debug.ps1 -App $App (or pass -Install)." }

$flowDir = Join-Path $script:RepoRoot ".maestro\$App"
$files = Get-ChildItem $flowDir -Filter '*.yaml' | Where-Object { -not $Flow -or $_.BaseName -eq $Flow } | Sort-Object Name
if (-not $files) { throw "No flows found in $flowDir$(if ($Flow) { " named $Flow" })." }

$total = [Diagnostics.Stopwatch]::StartNew()
$results = foreach ($file in $files) {
    $r = Invoke-Flow $maestro $file
    Write-Host ("{0,-5} {1} ({2:n1} s)" -f $(if ($r.Problem) { 'FAIL' } else { 'PASS' }), $r.Name, $r.Seconds)
    if ($r.Problem) { Write-Host ($r.Problem -replace '(?m)^', '      ') }
    $r
}
$failed = @($results | Where-Object { $_.Problem }).Count
Write-Host ("{0}: {1} passed, {2} failed in {3:n1} s." -f $App, (@($results).Count - $failed), $failed, $total.Elapsed.TotalSeconds)
if ($failed -gt 0) { exit 1 }
