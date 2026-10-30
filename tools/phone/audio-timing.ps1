<#
.SYNOPSIS
  Reads a fixed passage (RTST's reader, or Dino's reader) at 1x and 2x on the user's real phone and prints the timing table (Slice 165).
.DESCRIPTION
  Wireless adb only (connect.ps1 explains how to pair). Builds and installs the arm64 Debug build under a second package
  id (<app id>.dbg), so the store install, its data and signature are never touched; nothing is ever uninstalled, and data is cleared only with -FreshInstall (the dev package's own).
  Debug builds send no analytics (UsageGate). The Slice 162 test hooks skip first run and read the fixed text at the given speed
  (`read` hook; nothing is stored), so no taps are needed and the phone may stay locked while it is charging (a keep-awake loop always runs and wakes the screen every 15 s, no unlock, no taps). Time zero is the app's own `HOOK read start` log line.
  The volume is never changed.
  Per run it prints: time to first audio, per-chunk audio / play / synthesis times, the gap after each chunk, and the
  total read time against the audio length at that rate. A gap (beyond the audio length) over -GapLimitMs is flagged.
  The first run on a phone downloads the voice pack; progress lines show while it does (-PackTimeoutSeconds).
  Slice 180, never wait in silence: the power state is printed first (a warning on battery); the app must log within -FirstLogSeconds of launch; a voice pack download whose
  .partial file does not grow for -PackStallSeconds fails as stalled; a read with no new app log line for -ReadSilenceSeconds fails with the last line seen.
  -LogFile parses a saved logcat (adb logcat -d -v epoch) instead of using the phone.
#>
param(
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [string]$Serial,
    [double[]]$Rates = @(1.0, 2.0),
    # Gap = how long past a chunk's audio length (at the rate) its successor starts. Fitted from the re-run baseline (see the slice file).
    [int]$GapLimitMs = 1300,
    # Dino only: the starter book and the page (1-based) the read starts on, through the open-book hook (Slice 175).
    [string]$Book = 'three-little-pigs',
    [int]$Page = 1,
    [switch]$SkipInstall,
    # Slice 180 follow-up: build even when the cache holds the APK for the current build inputs.
    [switch]$Rebuild,
    # Slice 177: proxy for a machine with no phone: the headless emulator (x86_64); -Serial picks the device (default emulator-5554).
    [switch]$Emulator,
    # Slice 177: AOT-compile the Debug build like Release is. The test hooks (skip-first-run, PACK_READY, read, open-book, chunk markers) exist only in Debug
    # builds, so this is how Release speed is measured: AOT like the store build, hooks kept. Compare against the same run without -Aot.
    [switch]$Aot,
    # Slice 177: after each cold read (fresh process) read again in the same process and print that warm table too.
    [switch]$Warm,
    # Slice 177: take the process cold by clearing the dev package's data (`pm clear`) before every run: it wipes the dev app's data and the voice pack
    # downloads again each time (up to -PackTimeoutSeconds). Only the .dbg package is cleared, never the store install.
    [switch]$FreshInstall,
    # Slice 177: seconds to wait after the voice pack is ready before the read (a child takes a moment to press Play; the background warm-up runs meanwhile).
    [int]$SettleSeconds = 0,
    # Slice 177: the app language the first-run skip sets (its pack is downloaded), e.g. fa to check that language's engine is warmed.
    [string]$AppLanguage = 'en',
    [switch]$VoiceChanged,
    [int]$ReadTimeoutSeconds = 240,
    [int]$PackTimeoutSeconds = 1200,
    # Slice 180: fail early instead of waiting out the timeouts above.
    [int]$FirstLogSeconds = 60,
    [int]$PackStallSeconds = 120,
    [int]$ReadSilenceSeconds = 120,
    [string]$LogFile
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'timing.ps1')
. (Join-Path $PSScriptRoot '..\emulator\build-cache.ps1')

$apps = @{
    rtst = @{ Project = 'src\ReadTheStupidText.Mobile\ReadTheStupidText.Mobile.csproj'; Id = 'uk.sirous.readthestupidtext'; Scheme = 'rtst-test'
        Listen = 'the voice reads the passage smoothly: no clicks, no robotic stretching, no cut-off words, no pause in the middle of a sentence' }
    dino = @{ Project = 'src\DinoReader.App\DinoReader.App.csproj'; Id = 'uk.sirous.dinoreader'; Scheme = 'dino-test'
        Listen = "the voice (a real starter book page, read per character through Dino's own reader) sounds like a warm storyteller: no clicks, no cut-off words, no pause in the middle of a sentence" }
}
$cfg = $apps[$App]
$package = $cfg.Id + '.dbg'
# Fixed passage: plain words, commas and full stops only, so it makes several chunks.
$passage = 'The little dinosaur walked down to the river every morning. She watched the water run over the smooth grey stones, and she counted the fish that swam past her feet. ' +
    'Then she climbed the tall hill behind the trees and looked at the whole green valley below her. ' +
    'When the sun went down she walked home slowly, humming a quiet song to herself, and fell asleep before the first star came out.'

# Slice 180 follow-up: the phone's builds use the same cache as install-debug.ps1 (build-cache.ps1). The platform part of the cache name carries what
# makes the APK different from the emulator's x86_64 one: the dev package id (.dbg), the ABI and the AOT flag, so a cached APK is never installed
# on the wrong target. The key is the hash of the build inputs, as in install-debug.
$script:PhoneRid = if ($Emulator) { 'android-x64' } else { 'android-arm64' }
$script:PhonePlatform = 'android-dbg-' + ($script:PhoneRid -replace '^android-', '') + $(if ($Aot) { '-aot' } else { '' })
$script:ApkFilter = '*-Signed.apk'

# Debug build for the phone's ABI (AOT when asked) into the project's bin folder, copied into a temp folder that is renamed into the cache folder
# once the metadata file is in it. The caller holds the build locks.
function Build-PhoneApk([string]$Dir, [string]$Key) {
    $project = Join-Path $script:RepoRoot $cfg.Project
    $apkDir = Join-Path (Split-Path $project) "bin\Debug\net10.0-android\$($script:PhoneRid)"
    Get-ChildItem $apkDir -Filter $script:ApkFilter -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
    $extra = @()
    if ($Aot) { $extra += '-p:RunAOTCompilation=true', '-p:AndroidEnableProfiledAot=false' }
    dotnet build $project -f net10.0-android -c Debug "-p:RuntimeIdentifier=$($script:PhoneRid)" -p:EmbedAssembliesIntoApk=true `
        -p:IncludeWindows=false -p:AndroidPackageFormat=apk "-p:ApplicationId=$package" @extra
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $built = Get-ChildItem $apkDir -Filter $script:ApkFilter -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $built) { throw 'Signed APK not found.' }
    $temp = New-BuildTempDir $Dir
    try {
        Copy-Item $built.FullName $temp
        Save-BuildMeta $temp $App $script:PhonePlatform $Key
        Publish-BuildDir $temp $Dir
    } finally { Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue }
    Remove-OldBuilds $App $script:PhonePlatform
}

# The cached APK for the current inputs, building it (BelowNormal priority, under the build locks, outside the quiet-machine lock) when there is none
# or -Rebuild. The key is read again once the locks are held, because the files may have changed while this run waited.
function Get-PhoneApk {
    $key = Get-BuildKey
    $dir = Get-BuildDir $App $script:PhonePlatform $key
    $cached = if ($Rebuild) { $null } else { Find-CachedBuild $dir $script:ApkFilter }
    if ($cached) { Write-Host "Reusing cached $App APK (key $key, $($script:PhonePlatform)): $($cached.FullName)"; return $cached }
    $activity = Start-Activity "build-$App-$($script:PhonePlatform)"
    try {
        $locks = Enter-BuildLocks $App $script:PhonePlatform $key
        try {
            $key = Get-BuildKey
            $dir = Get-BuildDir $App $script:PhonePlatform $key
            $cached = if ($Rebuild) { $null } else { Find-CachedBuild $dir $script:ApkFilter }
            if ($cached) { Write-Host "Reusing cached $App APK (key $key, $($script:PhonePlatform)): built meanwhile by another agent."; return $cached }
            Write-Host "Building $App APK (key $key, $($script:PhonePlatform)); this takes a few minutes..."
            $priority = Set-BuildPriority
            try { Build-PhoneApk $dir $key | Out-Host } finally { Restore-ProcessPriority $priority }
        } finally { Exit-BuildLocks $locks }
    } finally { Stop-Activity $activity }
    return Find-CachedBuild $dir $script:ApkFilter
}

function Install-PhoneApk([string]$Phone, $Apk) {
    $result = "$(Invoke-Adb $Phone @('install', '-r', $Apk.FullName))"
    if ($result -notmatch 'Success') { throw "adb install of $package failed: $result" }
}

# Slice 179: counting off for every harness run, even a Release build (the marker the app reads, UsageEligibility.NoUsageMarkerFile). Written and
# checked before every read, so -SkipInstall and a `pm clear` (which wipes the external files folder) never leave a run without it.
function Set-NoUsageMarker([string]$Phone) { Set-DevNoUsageMarker $Phone $package }

# With -SkipInstall the installed APK must still be the cached build for the current inputs (right ABI, AOT or not): it is pulled and compared with
# the cached APK; $false when there is no cached build or the installed one differs.
function Test-InstalledMatches([string]$Phone, $Apk) {
    if (-not $Apk) { return $false }
    $installed = "$(Invoke-Adb $Phone @('shell', 'pm', 'path', $package))" -replace '^\s*package:', '' | ForEach-Object { $_.Trim() }
    if (-not $installed) { return $false }
    $pulled = Join-Path $script:OutDir 'installed-check.apk'
    Invoke-Adb $Phone @('pull', ($installed -split '\s+')[0], $pulled) | Out-Null
    $same = (Test-Path $pulled) -and ((Get-FileHash $pulled).Hash -eq (Get-FileHash $Apk.FullName).Hash)
    Remove-Item $pulled -Force -ErrorAction SilentlyContinue
    return $same
}

# Only the dev app's own lines: the RTST-TEST markers and the .NET runtime tag; never the rest of the phone's log.
# The phone's log buffer is small and rolls over on a busy cold start, so every poll's lines are kept (once each) for the whole run.
$script:Seen = New-Object 'System.Collections.Generic.HashSet[string]'
$script:SeenLines = New-Object 'System.Collections.Generic.List[string]'
$script:LastNewAt = Get-Date
$script:LastNewLine = ''
function Add-SeenLine([string]$Line) {
    if ($Line -notmatch '^\s*\d+\.\d+' -or -not $script:Seen.Add($Line)) { return }
    $script:SeenLines.Add($Line)
    # Only the app's test markers count as progress: unrelated chatter (GC, system tags) must not hide a stuck read.
    if ($Line -match 'RTST-TEST') {
        $script:LastNewAt = Get-Date
        $script:LastNewLine = $Line
    }
}

# The pid is re-read at most every few seconds (it changes only when the app restarts), not on every 400 ms poll.
$script:PidPollSeconds = 3
$script:CachedPid = $null
$script:CachedPidAt = [datetime]::MinValue
function Get-AppPid([string]$Phone) {
    if (((Get-Date) - $script:CachedPidAt).TotalSeconds -lt $script:PidPollSeconds) { return $script:CachedPid }
    $out = "$(Invoke-Adb $Phone @('shell', 'pidof', $package))".Trim()
    $script:CachedPid = ($out -split '\s+' | Where-Object { $_ -match '^\d+$' } | Select-Object -First 1)
    $script:CachedPidAt = Get-Date
    return $script:CachedPid
}

# The process this run launched (set by Start-AppWithHook after the relaunch); only its lines are kept. An RTST-TEST line from any other pid
# means an earlier process is still alive and reading: the run fails naming both, instead of mixing two processes in one table.
$script:RunPid = ''
function Get-AppLog([string]$Phone) {
    $lines = @(Invoke-Adb $Phone @('logcat', '-d', '-v', 'epoch', 'RTST-TEST:I', 'DOTNET:I', '*:S'))
    # Slice 180: the app's own process, every tag (a tag filter alone missed the first lines of a cold start).
    if ($script:RunPid) { $lines += @(Invoke-Adb $Phone @('logcat', '-d', '-v', 'epoch', "--pid=$($script:RunPid)")) }
    $lines = @($lines | ForEach-Object { "$_" })
    Assert-SingleAppPid $lines $script:RunPid
    foreach ($line in $lines) {
        if (-not $script:RunPid -or $line -match ('^\s*\d+\.\d+\s+' + $script:RunPid + '\s')) { Add-SeenLine $line }
    }
    return $script:SeenLines.ToArray()
}

# Stops the dev app (never the Play package: $package is the .dbg id) and waits until no process of it is left, so a still-running
# earlier process cannot log into the next read.
function Stop-AppAndWait([string]$Phone) {
    $deadline = (Get-Date).AddSeconds(10)
    do {
        Invoke-Adb $Phone @('shell', 'am', 'force-stop', $package) | Out-Null
        Start-Sleep -Milliseconds 500
        $script:CachedPidAt = [datetime]::MinValue
        if (-not (Get-AppPid $Phone)) { return }
    } while ((Get-Date) -lt $deadline)
    throw "$package is still running (pid $(Get-AppPid $Phone)) 10 s after am force-stop."
}

# Waits for the launched process and remembers its pid; it must differ from the previous run's pid.
function Wait-NewAppPid([string]$Phone, [string]$OldPid) {
    $deadline = (Get-Date).AddSeconds(30)
    do {
        $script:CachedPidAt = [datetime]::MinValue
        $found = Get-AppPid $Phone
        if ($found -and $found -ne $OldPid) { $script:RunPid = "$found"; return }
        Start-Sleep -Milliseconds 300
    } while ((Get-Date) -lt $deadline)
    throw "$package did not start a new process within 30 s of the launch."
}

function Get-LauncherActivity([string]$Phone) {
    $out = @(Invoke-Adb $Phone @('shell', 'cmd', 'package', 'resolve-activity', '--brief', '-c', 'android.intent.category.LAUNCHER', $package))
    $activity = ("$($out[-1])").Trim()
    if ($activity -notmatch '^[\w.]+/\S+$') { throw "$package is not installed on the phone (no launcher activity: '$activity'). Run without -SkipInstall." }
    return $activity
}

function Start-AppWithHook([string]$Phone, [string]$Hook) {
    Write-Host "   starting $package..."
    $oldPid = $script:RunPid
    $script:RunPid = ''
    Stop-AppAndWait $Phone
    Invoke-Adb $Phone @('logcat', '-c') | Out-Null
    $script:Seen.Clear(); $script:SeenLines.Clear(); $script:LastNewAt = Get-Date; $script:LastNewLine = ''
    Invoke-Adb $Phone @('shell', 'am', 'start', '-n', (Get-LauncherActivity $Phone), '--es', 'cmd', $Hook, '--es', 'lang', $AppLanguage) | Out-Null
    Wait-NewAppPid $Phone $oldPid
}

# $After: only a marker line stamped at or after this phone time counts (a stopped earlier read can still log READ_END).
# Returns @{ Time; Line } of the first match, or $null on timeout. $Waiting: printed every 15 s while waiting (no silent waits).
function Wait-Marker([string]$Phone, [string]$Pattern, [int]$TimeoutSeconds, [double]$After = 0, [string]$Waiting, [int]$MaxSilenceSeconds = 0) {
    $started = Get-Date
    $script:LastNewAt = $started
    $deadline = $started.AddSeconds($TimeoutSeconds)
    $nextNote = $started.AddSeconds(15)
    do {
        $lines = Get-AppLog $Phone
        foreach ($line in $lines) {
            if ($line -match $Pattern -and $line -match '^\s*(\d+\.\d+)') {
                $time = [double]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture)
                if ($time -ge $After) { return @{ Time = $time; Line = "$line" } }
            }
        }
        if ($MaxSilenceSeconds -gt 0) { Assert-NotSilent $Phone $MaxSilenceSeconds $Waiting }
        if ($Waiting -and (Get-Date) -ge $nextNote) {
            Write-Host ("   {0} ({1:0} s of {2} s)" -f $Waiting, ((Get-Date) - $started).TotalSeconds, $TimeoutSeconds)
            $nextNote = (Get-Date).AddSeconds(15)
        }
        Start-Sleep -Milliseconds 400
    } while ((Get-Date) -lt $deadline)
    return $null
}

# Slice 180: fails a wait when the app has logged nothing new for this long, with the last line seen and the phone's state.
function Assert-NotSilent([string]$Phone, [int]$MaxSilenceSeconds, [string]$Waiting) {
    $silent = ((Get-Date) - $script:LastNewAt).TotalSeconds
    if ($silent -lt $MaxSilenceSeconds) { return }
    $power = Get-PowerState $Phone
    $last = if ($script:LastNewLine) { $script:LastNewLine } else { '(none)' }
    throw ("No new RTST-TEST log line for {0:0} s while '{1}'. Last line seen: {2}. Phone: {3}, {4}." -f $silent, $Waiting, $last, $(if ($power.Powered) { 'charging' } else { 'on battery' }), $power.Wakefulness)
}

# Slice 180: the app must log within this time of launch, else it never started or Android froze it.
function Assert-AppStarted([string]$Phone) {
    $started = Get-Date
    $deadline = $started.AddSeconds($FirstLogSeconds)
    $nextNote = $started.AddSeconds(15)
    do {
        if (@(Get-AppLog $Phone).Count -gt 0) { return }
        if ((Get-Date) -ge $nextNote) {
            Write-Host ("   waiting for the app's first log line ({0:0} s of {1} s)" -f ((Get-Date) - $started).TotalSeconds, $FirstLogSeconds)
            $nextNote = (Get-Date).AddSeconds(15)
        }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    $power = Get-PowerState $Phone
    $appPid = Get-AppPid $Phone
    $tail = (Invoke-Adb $Phone @('logcat', '-d', '-t', '5')) -join ' | '
    throw ("The app logged nothing within {0} s of launch (pid: {1}, phone: {2}, {3}). Last lines on the phone: {4}" -f $FirstLogSeconds, $(if ($appPid) { $appPid } else { 'none, not running' }), $(if ($power.Powered) { 'charging' } else { 'on battery' }), $power.Wakefulness, $tail)
}

# Total bytes of the voice pack download in progress (the Debug app's files/packs/*.partial), or -1 when there is none.
function Get-PartialPackBytes([string]$Phone) {
    $out = @(Invoke-Adb $Phone @('shell', "run-as $package sh -c 'stat -c %s files/packs/*.partial 2>/dev/null'"))
    # run-as fails with a message (not a debuggable build, unknown package): that is not "no download", so say so.
    $runAsError = $out | Where-Object { "$_" -match 'run-as:|not debuggable|Unknown package|is not an application|error:' } | Select-Object -First 1
    if ($runAsError) { throw "Cannot read the pack folder of $package on the phone (run-as said: $runAsError). Is it the Debug dev build?" }
    $sizes = @($out | ForEach-Object { "$_".Trim() } | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [long]$_ })
    if ($sizes.Count -eq 0) { return -1 }
    return ($sizes | Measure-Object -Sum).Sum
}

# Slice 180: waits for PACK_READY, printing the download size at most every 30 s, and fails as soon as the .partial file stops growing.
function Wait-PackReady([string]$Phone) {
    $started = Get-Date
    $lastSize = -1; $grewAt = $started; $nextNote = $started; $seenPartial = $false
    while (((Get-Date) - $started).TotalSeconds -lt $PackTimeoutSeconds) {
        if (Wait-Marker $Phone 'RTST-TEST\s*:\s*PACK_READY' 5) { return }
        $size = Get-PartialPackBytes $Phone
        if ($size -ge 0) { $seenPartial = $true }
        if ($size -ne $lastSize) { $lastSize = $size; $grewAt = Get-Date }
        if ($lastSize -ge 0 -and ((Get-Date) - $grewAt).TotalSeconds -ge $PackStallSeconds) {
            $power = Get-PowerState $Phone
            throw ("pack download stalled at {0} bytes (no growth for {1} s; phone: {2}, {3})." -f $lastSize, $PackStallSeconds, $(if ($power.Powered) { 'charging' } else { 'on battery' }), $power.Wakefulness)
        }
        # Never-started only while no .partial file has ever been seen: after the download ends the file is gone while the pack is unzipped and verified.
        if (-not $seenPartial -and ((Get-Date) - $started).TotalSeconds -ge $PackStallSeconds) {
            $power = Get-PowerState $Phone
            $tail = (@(Get-AppLog $Phone) | Select-Object -Last 5) -join ' | '
            throw ("pack download never started (no .partial file and no PACK_READY within {0} s; phone: {1}, {2}). Last app lines: {3}" -f $PackStallSeconds, $(if ($power.Powered) { 'charging' } else { 'on battery' }), $power.Wakefulness, $tail)
        }
        if ((Get-Date) -ge $nextNote) {
            $shown = if ($lastSize -ge 0) { "$lastSize bytes" } elseif ($seenPartial) { 'download finished, preparing the pack' } else { 'not started' }
            Write-Host ("   waiting for the voice pack download ({0:0} s of {1} s, {2})" -f ((Get-Date) - $started).TotalSeconds, $PackTimeoutSeconds, $shown)
            $nextNote = (Get-Date).AddSeconds(30)
        }
    }
    throw "The voice pack did not become ready in $PackTimeoutSeconds s."
}

# Starts the read at this rate and waits for the app to accept it; returns the phone time of its `HOOK read start`
# log line (the app's own start of the read, so adb and `am` start-up latency are not counted). Fails fast if the hook is refused or missing.
# RTST reads the fixed passage through the `read` hook. Dino opens a real starter book page through `open-book ... play=page speed=...` and reads it
# on the app's own reading session (per-character voices), the same path as the Play button.
function Start-Reading([string]$Phone, [double]$Rate) {
    $speed = $Rate.ToString([Globalization.CultureInfo]::InvariantCulture)
    Write-Host '   starting the read on the phone...'
    $hookName = 'read'; $hookArgs = @{ text = $passage; speed = $speed }
    if ($App -eq 'dino') { $hookName = 'open-book'; $hookArgs = @{ id = $Book; page = "$Page"; play = 'page'; speed = $speed } }
    $sent = Send-TimedHook $Phone $cfg.Scheme $hookName $hookArgs
    $answer = Wait-Marker $Phone 'RTST-TEST\s*:\s*HOOK (read (start|failed)|open-book (failed|fault))' 30 $sent 'waiting for the app to accept the read'
    if (-not $answer) { throw "The read hook got no HOOK line within 30 s. Sent broadcast action $($script:HookActionFormat -f $cfg.Scheme) (a wrong action fails silently). Is the dev build running and Debug?" }
    if ($answer.Line -match 'HOOK read failed') { throw 'The app refused the read hook (empty text or a bad speed).' }
    if ($answer.Line -match 'HOOK open-book failed no book') { throw "No such book '$Book': give a starter book's id or title slug. ($($answer.Line))" }
    if ($answer.Line -match 'HOOK open-book') { throw "The app could not open the book: $($answer.Line)" }
    return $answer.Time
}

# Slice 177: a second read in the same process (warm). The log is cleared first so chunk numbers do not mix with the cold read.
function Invoke-WarmRead([string]$Phone, [double]$Rate) {
    Invoke-Adb $Phone @('logcat', '-c') | Out-Null
    $script:Seen.Clear(); $script:SeenLines.Clear(); $script:LastNewAt = Get-Date; $script:LastNewLine = ''
    Start-Sleep -Milliseconds 500
    $tap = Start-Reading $Phone $Rate
    if (-not (Wait-Marker $Phone 'RTST-TEST\s*:\s*READ_END' $ReadTimeoutSeconds $tap 'reading (warm)' $ReadSilenceSeconds)) { Write-Host '   (READ_END not seen in time)' }
    $log = Get-AppLog $Phone
    $file = Join-Path $script:OutDir ("{0}-{1}x-warm-{2}.log" -f $App, $Rate, (Get-Date -Format 'HHmmss'))
    $log | Set-Content $file
    return @{ Tap = $tap; Lines = $log; File = $file }
}

function Invoke-OneRead([string]$Phone, [double]$Rate) {
    if ($FreshInstall) { Invoke-Adb $Phone @('shell', 'pm', 'clear', $package) | Out-Null }
    Set-NoUsageMarker $Phone
    Start-AppWithHook $Phone 'skip-first-run'
    Assert-AppStarted $Phone
    if (-not (Wait-Marker $Phone 'HOOK skip-first-run ok' 60 0 'waiting for the app to skip first run')) { throw 'The skip-first-run hook did not report ok.' }
    Wait-PackReady $Phone
    if ($SettleSeconds -gt 0) { Write-Host "   settling $SettleSeconds s after the pack is ready..."; Start-Sleep -Seconds $SettleSeconds }
    $tap = Start-Reading $Phone $Rate
    $ended = [bool](Wait-Marker $Phone 'RTST-TEST\s*:\s*READ_END' $ReadTimeoutSeconds $tap 'reading' $ReadSilenceSeconds)
    if (-not $ended) { Write-Host '   (READ_END not seen in time)' }
    $log = Get-AppLog $Phone
    $file = Join-Path $script:OutDir ("{0}-{1}x-{2}.log" -f $App, $Rate, (Get-Date -Format 'HHmmss'))
    $log | Set-Content $file
    return @{ Tap = $tap; Lines = $log; File = $file; Ended = $ended }
}

if ($LogFile) {
    # One read per file; the rate is the first of -Rates (pass -Rates 2 for a 2x log). Time zero is its `HOOK read start` line.
    Assert-SingleAppPid @(Get-Content $LogFile)
    $events = @(ConvertFrom-TimingLog (Get-Content $LogFile))
    $hook = $events | Where-Object { $_.Kind -eq 'hook' -and $_.Outcome -eq 'start' } | Select-Object -First 1
    if (-not $hook) { Write-Host "== $App (saved log)"; Write-Host '   incomplete: no HOOK read start line in the log'; exit 0 }
    Write-TimingTable "$App (saved log, $($Rates[0])x)" (Get-ReadTimings $events $hook.Time $Rates[0] $GapLimitMs ($App -eq 'dino')) $GapLimitMs
    exit 0
}

$phone = if ($Emulator) { if ($Serial) { $Serial } else { 'emulator-5554' } } else { Find-WirelessPhone $Serial }
if (-not $phone) { Write-PairingAsk; exit 2 }
if (-not $Emulator) { Assert-PhoneLease $phone }  # Slice 206: exits 4 when another session holds the real phone (lease.ps1)
# Raw logs go to the shared worktrees folder, not into the repo.
$script:OutDir = Join-Path (Split-Path $script:RepoRoot -Parent) '.phone'
$null = New-Item -ItemType Directory -Force $script:OutDir
# Slice 180 follow-up: the build (or the cache lookup) comes first and holds no quiet-machine lock, as in install-debug.ps1; only the install and the timing
# run inside it. -SkipInstall never builds or installs: it checks that the installed APK is the cached build for the current inputs on this
# platform (right ABI and AOT) and fails with a message when it is not, so the flag never silently replaces the installed app.
$apk = $null
$needsInstall = -not $SkipInstall
if ($needsInstall) { $apk = Get-PhoneApk }
else {
    $currentDir = Get-BuildDir $App $script:PhonePlatform (Get-BuildKey)
    $current = Find-CachedBuild $currentDir $script:ApkFilter
    if (-not (Test-InstalledMatches $phone $current)) {
        throw "-SkipInstall: the installed $package is not the cached $App build for the current inputs on $($script:PhonePlatform) (wrong ABI, AOT or code, or nothing cached). Run again without -SkipInstall to build and install it."
    }
}
# The copy is named per run, so two runs of the same app never share or delete each other's file.
$held = $null
if ($apk) { $held = Join-Path $script:OutDir "install-$App-$($script:PhonePlatform)-$PID-$([guid]::NewGuid().ToString('N').Substring(0, 8)).apk" }
$quiet = $null
$awake = $null
# Renews the emulator lease; stops the run (exit 4) when another run took the emulator meanwhile. A phone run holds no emulator lease.
function Assert-EmulatorLease {
    if (-not $emulatorLeaseOwner) { return }
    if (-not (Confirm-DeviceLease $phone $emulatorLeaseOwner $script:EmulatorLeaseMinutes 'emulator')) {
        Write-Host "LEASE lost: another run took $phone during this timing run. Stopping, so the measurement is not shared."
        exit $script:ExitLeaseHeld
    }
}
# Slice 207: an emulator is leased too (the same lease as run-flows.ps1), so a timing run never shares it with a flows run. Taken after the build and the
# -SkipInstall check, right before the try that releases it, and renewed before each timing read.
$emulatorLeaseOwner = $null
if ($Emulator) {
    $leaseOwner = Get-DeviceLeaseOwner 'audio-timing'
    try { $heldBy = Wait-ForLease $phone $leaseOwner $script:EmulatorLeaseMinutes 15 'emulator' }
    catch { if ($held) { Remove-Item $held -Force -ErrorAction SilentlyContinue }; throw }  # the lease lock can time out: do not leak the APK copy
    if ($heldBy) { Write-Host "LEASE held: $phone is used by $heldBy; waited 15 min. Not running."; if ($held) { Remove-Item $held -Force -ErrorAction SilentlyContinue }; exit $script:ExitLeaseHeld }
    $emulatorLeaseOwner = $leaseOwner
}
try {
    if ($apk) {
        # The cached file can be pruned or replaced (-Rebuild, another build) while this run waits for the quiet machine, so install from a copy.
        Copy-Item $apk.FullName $held -Force
        $apk = Get-Item $held
    }
    # Slice 180: a timing run needs a quiet machine: wait for builds, flows runs and VM runs to end, and hold new ones off until this run is done.
    Write-Host 'Waiting for a quiet machine (no builds, emulator flows or VM runs)...'
    $quiet = Enter-QuietMachine
    Assert-EmulatorLease  # the wait for the quiet machine can be long
    $power = Write-PowerState $phone
    $awake = Start-KeepAwake $phone  # always on: harmless while charging, and it covers an unplug mid-run
    if ($needsInstall) { Write-Host "   installing $package..."; Install-PhoneApk $phone $apk }
    foreach ($rate in $Rates) {
        Assert-EmulatorLease
        $read = Invoke-OneRead $phone $rate
        $events = @(ConvertFrom-TimingLog $read.Lines)
        Write-TimingTable "$App at $($rate)x" (Get-ReadTimings $events $read.Tap $rate $GapLimitMs ($App -eq 'dino')) $GapLimitMs
        Write-Host "   raw log: $($read.File)"
        if ($Warm -and -not $read.Ended) {
            Write-Host '   skipping the warm read: the cold read never ended, and a second read would overlap it.'
        }
        elseif ($Warm) {
            $w = Invoke-WarmRead $phone $rate
            Write-TimingTable "$App at $($rate)x WARM" (Get-ReadTimings @(ConvertFrom-TimingLog $w.Lines) $w.Tap $rate $GapLimitMs ($App -eq 'dino')) $GapLimitMs
            Write-Host "   raw log: $($w.File)"
        }
    }
    if ($VoiceChanged) { Write-Host "listen now: $($cfg.Listen)" }
}
finally {
    Stop-KeepAwake $awake
    if ($held) { Remove-Item $held -Force -ErrorAction SilentlyContinue }
    Invoke-Adb $phone @('shell', 'am', 'force-stop', $package) | Out-Null
    if ($quiet) { Exit-QuietMachine $quiet }
    if ($emulatorLeaseOwner) { Remove-Lease $phone $emulatorLeaseOwner 0 'emulator' | Out-Null }
}
