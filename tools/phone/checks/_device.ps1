# Device helpers shared by the check scripts (Slice 213). Dot-sourced after common.ps1 and _lib.ps1; every wait honours the check's deadline
# (Assert-WithinBudget), so a check can never run past the runner's budget, and none waits in silence (a progress line every 15 s).
$script:PidWaitSeconds = 30
$script:StopWaitSeconds = 10
$script:LogPollMs = 400
$script:ProgressSeconds = 15
$script:ProcessCheckSeconds = 3
$script:HookAnswerSeconds = 30
$script:PackReadySeconds = 60
$script:SentenceCacheDir = 'cache/sentences'   # FileSystem.CacheDirectory/sentences (AndroidSupertonicSpeechReader.SentenceFolderName), under the app's data dir
$script:ReaderPassage = 'The little dinosaur walked down to the river every morning. She watched the water run over the smooth grey stones, and she counted the fish that swam past her feet. ' +
    'Then she climbed the tall hill behind the trees and looked at the whole green valley below her.'

function Get-DevAppPid([string]$Phone, [string]$Package) {
    $out = "$(Invoke-Adb $Phone @('shell', 'pidof', $Package))".Trim()
    return ($out -split '\s+' | Where-Object { $_ -match '^\d+$' } | Select-Object -First 1)
}

# force-stop of the Debug package only (Get-DevPackage's allow-list is checked by the caller's package), then waits until no process of it is left.
function Stop-DevApp([string]$Phone, [string]$Package) {
    Assert-DevPackage $Package
    $deadline = (Get-Date).AddSeconds($script:StopWaitSeconds)
    do {
        Assert-WithinBudget 'stopping the app'
        Invoke-Adb $Phone @('shell', 'am', 'force-stop', $Package) | Out-Null
        Start-Sleep -Milliseconds 500
        if (-not (Get-DevAppPid $Phone $Package)) { return }
    } while ((Get-Date) -lt $deadline)
    throw "$Package is still running 10 s after am force-stop."
}

function Get-LauncherActivity([string]$Phone, [string]$Package) {
    $out = @(Invoke-Adb $Phone @('shell', 'cmd', 'package', 'resolve-activity', '--brief', '-c', 'android.intent.category.LAUNCHER', $Package))
    $activity = ("$($out[-1])").Trim()
    if ($activity -notmatch '^[\w.]+/\S+$') { throw "$Package is not installed on the phone (no launcher activity: '$activity')." }
    return $activity
}

# Launches the app with a hook (skip-first-run by default) in a fresh process; returns the new pid. The log buffer is cleared first.
function Start-DevAppWithHook([string]$Phone, [string]$Package, [string]$Hook = 'skip-first-run', [string]$Language = 'en') {
    Assert-WithinBudget 'launching the app'
    Stop-DevApp $Phone $Package
    Invoke-Adb $Phone @('logcat', '-c') | Out-Null
    Invoke-Adb $Phone @('shell', 'am', 'start', '-n', (Get-LauncherActivity $Phone $Package), '--es', 'cmd', $Hook, '--es', 'lang', $Language) | Out-Null
    $deadline = (Get-Date).AddSeconds($script:PidWaitSeconds)
    do {
        Assert-WithinBudget 'waiting for the app process to start'
        $appPid = Get-DevAppPid $Phone $Package
        if ($appPid) { return $appPid }
        Start-Sleep -Milliseconds 300
    } while ((Get-Date) -lt $deadline)
    throw "$Package did not start a process within $($script:PidWaitSeconds) s of the launch."
}

# Every log line of the app's process (all tags; the RTST-TEST markers are among them). With $Since (an epoch time) only the lines from then on: a poll then costs
# what is new, not the whole log (`logcat -T`).
function Get-PidLog([string]$Phone, [string]$AppPid, [double]$Since = 0) {
    $arguments = @('logcat', '-d', '-v', 'epoch', "--pid=$AppPid")
    if ($Since -gt 0) { $arguments += '-T', $Since.ToString('F3', [Globalization.CultureInfo]::InvariantCulture) }
    return @(Invoke-Adb $Phone $arguments | ForEach-Object { "$_" })
}

# The first line of the pid's log matching $Pattern with a phone time at or after $After: @{ Time; Line }, or $null on timeout. Fails at once when the process is gone.
# Each poll reads only the log since the last one; the process is looked up every ProcessCheckSeconds, not every poll.
function Wait-LogMatch([string]$Phone, [string]$Package, [string]$AppPid, [string]$Pattern, [int]$TimeoutSeconds, [double]$After, [string]$Waiting) {
    $started = Get-Date
    $nextNote = $started.AddSeconds($script:ProgressSeconds)
    $nextProcessCheck = $started.AddSeconds($script:ProcessCheckSeconds)
    $cursor = 0.0
    do {
        Assert-WithinBudget $Waiting
        $lines = Get-PidLog $Phone $AppPid $cursor
        foreach ($line in $lines) {
            if ($line -match $Pattern -and $line -match '^\s*(\d+\.\d+)' -and [double]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture) -ge $After) {
                return @{ Time = [double]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture); Line = $line }
            }
        }
        $cursor = Get-LogCursor $lines $cursor
        if ((Get-Date) -ge $nextProcessCheck) {
            if ((Get-DevAppPid $Phone $Package) -ne $AppPid) { throw "The app process $AppPid ended while $Waiting (a crash or a kill): see 'adb logcat -b crash'." }
            $nextProcessCheck = (Get-Date).AddSeconds($script:ProcessCheckSeconds)
        }
        if ((Get-Date) -ge $nextNote) { Write-Host ("   {0} ({1:0} s of {2} s)" -f $Waiting, ((Get-Date) - $started).TotalSeconds, $TimeoutSeconds); $nextNote = (Get-Date).AddSeconds($script:ProgressSeconds) }
        Start-Sleep -Milliseconds $script:LogPollMs
    } while (((Get-Date) - $started).TotalSeconds -lt $TimeoutSeconds)
    return $null
}

function Send-DevHook([string]$Phone, [string]$App, [string]$Command, [hashtable]$HookArgs = @{}) {
    return (Send-TimedHook $Phone $script:DevApps[$App].Scheme $Command $HookArgs)
}

# Launches fresh with skip-first-run and waits for the hook's ok and for the installed voice pack to be ready (PACK_READY). The setup guarantees a pack is
# installed, so a wait here that outlasts a minute means a download started: that is a failure of the setup, not something to wait out.
function Start-ReadyApp([string]$Phone, [string]$App, [string]$Package) {
    $appPid = Start-DevAppWithHook $Phone $Package
    if (-not (Wait-LogMatch $Phone $Package $appPid 'HOOK skip-first-run ok' 60 0 'waiting for the app to skip first run')) { throw 'The skip-first-run hook did not report ok within 60 s.' }
    if (-not (Wait-LogMatch $Phone $Package $appPid 'PACK_READY' $script:PackReadySeconds 0 'waiting for the installed voice pack')) {
        throw "The voice pack was not ready within $($script:PackReadySeconds) s: it is missing or a download started. Restore it (reset-dev-app.ps1) instead of waiting."
    }
    return $appPid
}

# Empties the app's sentence audio cache so the next read synthesises every sentence again (a warm cache made repeat runs 240-578 ms). Verifies it is empty.
function Clear-SentenceCache([string]$Phone, [string]$Package) {
    Assert-DevPackage $Package
    $out = @(Invoke-Adb $Phone @('shell', "run-as $Package sh -c 'rm -rf $($script:SentenceCacheDir); mkdir -p $($script:SentenceCacheDir); ls $($script:SentenceCacheDir) | wc -l'"))
    $left = "$($out | Select-Object -Last 1)".Trim()
    if ($left -ne '0') { throw "Could not empty the sentence cache of $Package (run-as said: $($out -join ' | ')): the run would not be cold." }
}

# ---- -Prepare (Slice 213): download a missing voice pack with the app in the foreground, outside any budget
# Deletes the stale <id>-<version>.zip.partial files of the needed packs, so the watch below never follows an old download.
function Remove-StalePartials([string]$Phone, [string]$Package, [string[]]$PackIds) {
    Assert-DevPackage $Package
    foreach ($id in $PackIds) { Invoke-Adb $Phone @('shell', "run-as $Package sh -c 'rm -f $($script:PacksDirectory)/$id-*.zip.partial'") | Out-Null }
}

# Size of the biggest .partial file in the app's packs folder (0 when none yet).
function Get-PartialBytesOnPhone([string]$Phone, [string]$Package) {
    Assert-DevPackage $Package
    return (Get-PartialBytes @(Invoke-Adb $Phone @('shell', "run-as $Package sh -c 'stat -c %s $($script:PartialFileGlob) 2>/dev/null'")))
}

function New-PackWaitResult([string]$State, [string]$Detail) { return [pscustomobject]@{ State = $State; Detail = $Detail } }

# One look at the download: reads the app's new log lines into $Log (kept whole for the failure message), returns Present, Failed (the app's PACKDL failed line), NoStart or Running.
function Get-PackWaitStep([string]$Phone, [string]$Package, [string]$AppPid, [string[]]$PackIds, $Watch) {
    if (@(Get-InstalledPackIds $Phone $Package $PackIds).Count -gt 0) { return (New-PackWaitResult 'Present' $null) }
    $lines = Get-PidLog $Phone $AppPid $Watch.Cursor
    $Watch.Cursor = Get-LogCursor $lines $Watch.Cursor
    $Watch.Log += $lines
    $log = Resolve-PackDownloadLog $Watch.Log $PackIds
    if ($log.State -eq 'Failed') { return (New-PackWaitResult 'Failed' "the app reported: $($log.Line)") }
    if ($log.State -ne 'Idle' -and -not $Watch.Started) { $Watch.Started = Get-Date; $Watch.LastGrowth = $Watch.Started; Write-Host "   download started: $($log.Line)" }
    if (-not $Watch.Started -and ((Get-Date) - $Watch.Launched).TotalSeconds -ge $script:PackStartSeconds) { return (New-PackWaitResult 'NoStart' (Get-NoPackStartReason $Watch.Log $PackIds)) }
    return (New-PackWaitResult 'Running' $null)
}

# Tracks the .partial file: the bytes reached, and a stall (no growth for PackStallSeconds) once the download has started.
function Update-PackGrowth([string]$Phone, [string]$Package, $Watch) {
    $bytes = Get-PartialBytesOnPhone $Phone $Package
    if ($bytes -gt $Watch.Bytes) { $Watch.Bytes = $bytes; $Watch.LastGrowth = Get-Date }
    if ($Watch.Started -and (Test-PackStalled $Watch.LastGrowth (Get-Date) $script:PackStallSeconds)) {
        return (New-PackWaitResult 'Failed' "pack download stalled: the .partial file did not grow for $($script:PackStallSeconds) s ($(Format-PackProgress $Watch.Bytes))")
    }
    return $null
}

# Launches the dev app in the foreground (skip-first-run starts the pack download), requires a PACKDL start line within 60 s (else FAIL quoting the app's last 10 RTST-TEST lines),
# then waits until the pack passes Test-PackInstalled: progress every 30 s, a .partial file not growing for 2 minutes is a FAIL. State Present or Failed with a Detail.
function Wait-PackInstalled([string]$Phone, [string]$Package, [string[]]$PackIds) {
    Remove-StalePartials $Phone $Package $PackIds
    $appPid = Start-DevAppWithHook $Phone $Package
    $watch = [pscustomobject]@{ Cursor = 0.0; Log = @(); Started = $null; LastGrowth = Get-Date; Bytes = [long]0; Launched = Get-Date }
    $nextNote = (Get-Date).AddSeconds($script:PackProgressSeconds)
    try {
        while ($true) {
            $step = Get-PackWaitStep $Phone $Package $appPid $PackIds $watch
            if ($step.State -in 'Present', 'Failed', 'NoStart') { return (New-PackWaitResult $(if ($step.State -eq 'NoStart') { 'Failed' } else { $step.State }) $step.Detail) }
            $stall = Update-PackGrowth $Phone $Package $watch
            if ($stall) { return $stall }
            $elapsed = ((Get-Date) - $watch.Launched).TotalSeconds
            if ($elapsed -ge $script:PrepareMaxSeconds) { return (New-PackWaitResult 'Failed' "the pack was not installed after $($script:PrepareMaxSeconds) s ($(Format-PackProgress $watch.Bytes))") }
            if ((Get-Date) -ge $nextNote) { Write-Host ("   downloading the voice pack: {0} ({1:0} s)" -f (Format-PackProgress $watch.Bytes), $elapsed); $nextNote = (Get-Date).AddSeconds($script:PackProgressSeconds) }
            Start-Sleep -Seconds $script:PackPollSeconds
        }
    } finally { try { Stop-DevApp $Phone $Package } catch { Write-Host "   WARNING: could not stop the app after the pack download: $($_.Exception.Message)" } }
}
