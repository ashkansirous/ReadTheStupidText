<#
.SYNOPSIS
  Runs the Maestro flows of one app (.maestro/<app>/*.yaml) on the warm emulator and prints pass/fail only (Slice 162).
.DESCRIPTION
  Maestro is found in $env:MAESTRO_HOME, else <worktrees folder>\.tools\maestro (shared by all worktrees).
  A flow may start with comment lines `# expect-log: <text>`: after the flow ends, each text must appear in the
  RTST-TEST logcat lines (the Debug-only markers) within -LogTimeoutSeconds. Failures print the last lines of Maestro's output.
  The debug app must already be installed (install-debug.ps1 -App rtst|dino); -Install does that first.
  Slice 180: -App auto runs only the apps the change touches (tools/test-targets.ps1) and prints the ones it skipped and why;
  -Instance 1|2 picks the emulator (default 1; start it with start-warm.ps1 -Instance n).
#>
param(
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino', 'auto')][string]$App,
    [ValidateSet(1, 2)][int]$Instance = 1,
    [string]$Flow,
    [switch]$Install,
    [int]$LogTimeoutSeconds = 40
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'build-cache.ps1')
Set-EmulatorInstance $Instance

# -App auto: one run per touched app, each through this same script, so a failing app does not hide the next one.
if ($App -eq 'auto') {
    $touched = @(& (Join-Path $PSScriptRoot '..\test-targets.ps1') -Platform android -Raw)
    if ($touched.Count -eq 0) { Write-Host 'NO ANDROID APP IS TOUCHED BY THIS CHANGE: no flows ran. That is a skip, not a test pass.'; exit 0 }
    $worst = 0
    foreach ($name in $touched) {
        $flowArgs = if ($Flow) { @('-Flow', $Flow) } else { @() }
        & $PSCommandPath -App $name -Instance $Instance -Install:$Install -LogTimeoutSeconds $LogTimeoutSeconds @flowArgs
        if ($LASTEXITCODE -ne 0) { $worst = 1 }
    }
    exit $worst
}

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

# "<app> isn't responding" dialogs (System UI ANR after a CPU-starved boot) block every later flow until the emulator restarts.
# Returns the dialog's title text when one is on screen, else $null.
function Get-AnrDialogXml {
    $ErrorActionPreference = 'Continue'
    $xml = & $script:Adb -s $script:Serial shell uiautomator dump /dev/tty 2>&1 | Out-String
    if ($xml -match "isn't responding|aerr_wait|aerr_close") { return $xml }
    return $null
}

function Get-NodeCenter([string]$Xml, [string]$ResourceId) {
    $m = [regex]::Match($Xml, 'resource-id="' + [regex]::Escape($ResourceId) + '"[^>]*?bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"')
    if (-not $m.Success) { return $null }
    $g = $m.Groups
    return @([int](([int]$g[1].Value + [int]$g[3].Value) / 2), [int](([int]$g[2].Value + [int]$g[4].Value) / 2))
}

# Taps "Wait" (else "Close app") on an ANR dialog and says so; throws, naming the dialog, when it cannot clear it.
function Clear-AnrDialog([string]$When) {
    $ErrorActionPreference = 'Continue'
    for ($try = 1; $try -le 3; $try++) {
        $xml = Get-AnrDialogXml
        if (-not $xml) { return }
        $point = (Get-NodeCenter $xml 'android:id/aerr_wait'), (Get-NodeCenter $xml 'android:id/aerr_close') | Where-Object { $_ } | Select-Object -First 1
        if ($point) { & $script:Adb -s $script:Serial shell input tap $point[0] $point[1] | Out-Null }
        else { & $script:Adb -s $script:Serial shell input keyevent KEYCODE_BACK | Out-Null }
        Write-Host "Dismissed an 'isn't responding' (ANR) dialog $When on $($script:Serial)."
        Start-Sleep -Seconds 2
    }
    if (Get-AnrDialogXml) { throw "An 'isn't responding' (ANR) dialog is on screen on $($script:Serial) $When and could not be dismissed; restart the emulator (stop.ps1 -Instance $Instance, then start-warm.ps1)." }
}

function Invoke-Flow([string]$Maestro, [System.IO.FileInfo]$File) {
    $ErrorActionPreference = 'Continue'
    & $script:Adb -s $script:Serial logcat -c
    $sw = [Diagnostics.Stopwatch]::StartNew()
    # Applied to the Maestro call only (a JVM reads JAVA_TOOL_OPTIONS whatever the launcher does with MAESTRO_OPTS); the Android build's Java must keep the real user.home.
    $env:JAVA_TOOL_OPTIONS = "-Duser.home=$($script:MaestroHome)"
    try { $output = & $Maestro --device $script:Serial test --no-reinstall-driver --debug-output $script:MaestroOut $File.FullName 2>&1 | Out-String }
    finally { Remove-Item Env:JAVA_TOOL_OPTIONS -ErrorAction SilentlyContinue }
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
$script:MaestroOut = Join-Path $toolsDir "maestro-out\i$Instance"
# One Java user.home per emulator instance, so two flows runs at once never share Maestro's port forwarding or state.
$script:MaestroHome = Join-Path $toolsDir "maestro-home\i$Instance"
$null = New-Item -ItemType Directory -Force $script:MaestroOut, $script:MaestroHome
Write-Host "Instance ${Instance}: Maestro home $($script:MaestroHome)"
$env:MAESTRO_OPTS = "-Duser.home=$($script:MaestroHome)"
$env:MAESTRO_CLI_NO_ANALYTICS_NOTIFICATION = '1'
if ($Install) { & (Join-Path $PSScriptRoot 'install-debug.ps1') -App $App -Instance $Instance }
else { & (Join-Path $PSScriptRoot 'start-warm.ps1') -Instance $Instance | Out-Null }

# Without -Install the installed app must be the build for the current inputs; a stale one is replaced (from the cache when it exists).
if (-not $Install) {
    $currentKey = Get-BuildKey
    if ((Get-InstalledKey $App) -ne $currentKey) {
        Write-Host "The installed $App build is not the one for the current inputs (key $currentKey); installing it first."
        & (Join-Path $PSScriptRoot 'install-debug.ps1') -App $App -Instance $Instance
    }
}

$ErrorActionPreference = 'Continue'
$installed = & $script:Adb -s $script:Serial shell pm list packages $packages[$App]
$ErrorActionPreference = 'Stop'
if (-not ("$installed" -match [regex]::Escape($packages[$App]))) { throw "$($packages[$App]) is not installed; run install-debug.ps1 -App $App (or pass -Install)." }

$flowDir = Join-Path $script:RepoRoot ".maestro\$App"
$files = Get-ChildItem $flowDir -Filter '*.yaml' | Where-Object { -not $Flow -or $_.BaseName -eq $Flow } | Sort-Object Name
if (-not $files) { throw "No flows found in $flowDir$(if ($Flow) { " named $Flow" })." }

$total = [Diagnostics.Stopwatch]::StartNew()
$activity = Start-Activity "flows-$App-i$Instance"
try {
    Clear-AnrDialog 'before the flows'
    $results = foreach ($file in $files) {
        $r = Invoke-Flow $maestro $file
        Write-Host ("{0,-5} {1} ({2:n1} s)" -f $(if ($r.Problem) { 'FAIL' } else { 'PASS' }), $r.Name, $r.Seconds)
        if ($r.Problem) { Write-Host ($r.Problem -replace '(?m)^', '      ') }
        $r
    }
    Clear-AnrDialog 'after the flows'
} finally { Stop-Activity $activity }
$failed = @($results | Where-Object { $_.Problem }).Count
Write-Host ("{0}: {1} passed, {2} failed in {3:n1} s." -f $App, (@($results).Count - $failed), $failed, $total.Elapsed.TotalSeconds)
if ($failed -gt 0) { exit 1 }
