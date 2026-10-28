<#
.SYNOPSIS
  Reads a fixed passage (RTST's reader, or Dino's reader) at 1x and 2x on the user's real phone and prints the timing table (Slice 165).
.DESCRIPTION
  Wireless adb only (connect.ps1 explains how to pair). Builds and installs the arm64 Debug build under a second package
  id (<app id>.dbg), so the store install, its data and signature are never touched; nothing is ever uninstalled or cleared.
  Debug builds send no analytics (UsageGate). The Slice 162 test hooks skip first run and read the fixed text at the given speed
  (`read` hook; nothing is stored), so no taps are needed and the phone may stay locked. Time zero is the app's own `HOOK read start` log line.
  The volume is never changed.
  Per run it prints: time to first audio, per-chunk audio / play / synthesis times, the gap after each chunk, and the
  total read time against the audio length at that rate. A gap (beyond the audio length) over -GapLimitMs is flagged.
  The first run on a phone downloads the voice pack; progress lines show while it does (-PackTimeoutSeconds).
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
    [switch]$VoiceChanged,
    [int]$ReadTimeoutSeconds = 240,
    [int]$PackTimeoutSeconds = 1200,
    [string]$LogFile
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'timing.ps1')

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

function Build-AndInstall([string]$Phone) {
    Write-Host "   building $App (arm64 Debug) and installing $package on the phone; this takes a few minutes..."
    $project = Join-Path $script:RepoRoot $cfg.Project
    $apkDir = Join-Path (Split-Path $project) 'bin\Debug\net10.0-android\android-arm64'
    Get-ChildItem $apkDir -Filter '*-Signed.apk' -ErrorAction SilentlyContinue | Remove-Item -Force
    dotnet build $project -f net10.0-android -c Debug -p:RuntimeIdentifier=android-arm64 -p:EmbedAssembliesIntoApk=true `
        -p:IncludeWindows=false -p:AndroidPackageFormat=apk "-p:ApplicationId=$package"
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $apk = Get-ChildItem $apkDir -Filter '*-Signed.apk' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $apk) { throw 'Signed APK not found.' }
    $result = "$(Invoke-Adb $Phone @('install', '-r', $apk.FullName))"
    if ($result -notmatch 'Success') { throw "adb install of $package failed: $result" }
}

# Only the dev app's own lines: the RTST-TEST markers and the .NET runtime tag; never the rest of the phone's log.
# The phone's log buffer is small and rolls over on a busy cold start, so every poll's lines are kept (once each) for the whole run.
$script:Seen = New-Object 'System.Collections.Generic.HashSet[string]'
$script:SeenLines = New-Object 'System.Collections.Generic.List[string]'
function Get-AppLog([string]$Phone) {
    foreach ($line in (Invoke-Adb $Phone @('logcat', '-d', '-v', 'epoch', 'RTST-TEST:I', 'DOTNET:I', '*:S'))) {
        if ($script:Seen.Add("$line")) { $script:SeenLines.Add("$line") }
    }
    return $script:SeenLines.ToArray()
}

function Get-LauncherActivity([string]$Phone) {
    $out = @(Invoke-Adb $Phone @('shell', 'cmd', 'package', 'resolve-activity', '--brief', '-c', 'android.intent.category.LAUNCHER', $package))
    $activity = ("$($out[-1])").Trim()
    if ($activity -notmatch '^[\w.]+/\S+$') { throw "$package is not installed on the phone (no launcher activity: '$activity'). Run without -SkipInstall." }
    return $activity
}

function Start-AppWithHook([string]$Phone, [string]$Hook) {
    Write-Host "   starting $package..."
    Invoke-Adb $Phone @('shell', 'am', 'force-stop', $package) | Out-Null
    Start-Sleep -Milliseconds 800
    Invoke-Adb $Phone @('logcat', '-c') | Out-Null
    $script:Seen.Clear(); $script:SeenLines.Clear()
    Invoke-Adb $Phone @('shell', 'am', 'start', '-n', (Get-LauncherActivity $Phone), '--es', 'cmd', $Hook, '--es', 'lang', 'en') | Out-Null
}

# $After: only a marker line stamped at or after this phone time counts (a stopped earlier read can still log READ_END).
# Returns @{ Time; Line } of the first match, or $null on timeout. $Waiting: printed every 15 s while waiting (no silent waits).
function Wait-Marker([string]$Phone, [string]$Pattern, [int]$TimeoutSeconds, [double]$After = 0, [string]$Waiting) {
    $started = Get-Date
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
        if ($Waiting -and (Get-Date) -ge $nextNote) {
            Write-Host ("   {0} ({1:0} s of {2} s)" -f $Waiting, ((Get-Date) - $started).TotalSeconds, $TimeoutSeconds)
            $nextNote = (Get-Date).AddSeconds(15)
        }
        Start-Sleep -Milliseconds 400
    } while ((Get-Date) -lt $deadline)
    return $null
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
    if (-not $answer) { throw 'The read hook got no answer: is the dev build running and Debug?' }
    if ($answer.Line -match 'HOOK read failed') { throw 'The app refused the read hook (empty text or a bad speed).' }
    if ($answer.Line -match 'HOOK open-book failed no book') { throw "No such book '$Book': give a starter book's id or title slug. ($($answer.Line))" }
    if ($answer.Line -match 'HOOK open-book') { throw "The app could not open the book: $($answer.Line)" }
    return $answer.Time
}

function Invoke-OneRead([string]$Phone, [double]$Rate) {
    Start-AppWithHook $Phone 'skip-first-run'
    if (-not (Wait-Marker $Phone 'HOOK skip-first-run ok' 60 0 'waiting for the app to skip first run')) { throw 'The skip-first-run hook did not report ok.' }
    if (-not (Wait-Marker $Phone 'RTST-TEST\s*:\s*PACK_READY' $PackTimeoutSeconds 0 'waiting for the voice pack download')) { throw "The voice pack did not become ready in $PackTimeoutSeconds s." }
    $tap = Start-Reading $Phone $Rate
    if (-not (Wait-Marker $Phone 'RTST-TEST\s*:\s*READ_END' $ReadTimeoutSeconds $tap 'reading')) { Write-Host '   (READ_END not seen in time)' }
    $log = Get-AppLog $Phone
    $file = Join-Path $script:OutDir ("{0}-{1}x-{2}.log" -f $App, $Rate, (Get-Date -Format 'HHmmss'))
    $log | Set-Content $file
    return @{ Tap = $tap; Lines = $log; File = $file }
}

if ($LogFile) {
    # One read per file; the rate is the first of -Rates (pass -Rates 2 for a 2x log). Time zero is its `HOOK read start` line.
    $events = @(ConvertFrom-TimingLog (Get-Content $LogFile))
    $hook = $events | Where-Object { $_.Kind -eq 'hook' -and $_.Outcome -eq 'start' } | Select-Object -First 1
    if (-not $hook) { Write-Host "== $App (saved log)"; Write-Host '   incomplete: no HOOK read start line in the log'; exit 0 }
    Write-TimingTable "$App (saved log, $($Rates[0])x)" (Get-ReadTimings $events $hook.Time $Rates[0] $GapLimitMs ($App -eq 'dino')) $GapLimitMs
    exit 0
}

$phone = Find-WirelessPhone $Serial
if (-not $phone) { Write-PairingAsk; exit 2 }
# Raw logs go to the shared worktrees folder, not into the repo.
$script:OutDir = Join-Path (Split-Path $script:RepoRoot -Parent) '.phone'
$null = New-Item -ItemType Directory -Force $script:OutDir
if (-not $SkipInstall) { Build-AndInstall $phone }

try {
    foreach ($rate in $Rates) {
        $read = Invoke-OneRead $phone $rate
        $events = @(ConvertFrom-TimingLog $read.Lines)
        Write-TimingTable "$App at $($rate)x" (Get-ReadTimings $events $read.Tap $rate $GapLimitMs ($App -eq 'dino')) $GapLimitMs
        Write-Host "   raw log: $($read.File)"
    }
    if ($VoiceChanged) { Write-Host "listen now: $($cfg.Listen)" }
}
finally {
    Invoke-Adb $phone @('shell', 'am', 'force-stop', $package) | Out-Null
}
