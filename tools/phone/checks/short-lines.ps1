<#
.SYNOPSIS
  short-lines: what Kokoro returns on the phone for short lines, on the normal engine and on a single-thread engine (Slice 220, follow-up to Slice 210).
.DESCRIPTION
  Dino only, Debug build, kokoro-en pack. For each voice: sets the remembered Kokoro voice in the dev app's preferences (the app is stopped first), relaunches, and reads each short line
  (Slice 210 set: "Yes.", "Oh no!", "Help!", "Ann.") -Repeats times through the `read` hook, waiting for READ_END. In a Debug build the guard writes, for every short Kokoro line, two
  `short line probe` log lines (no text), one per arm: 3 plain takes on the normal multi-thread engine and 3 on a single-thread engine, each with samples, NaN count, peak over the finite
  samples and voiced ms. A take with NaN samples is a failed take: the guard logs `take has NaN samples`, and its retry runs on the single-thread engine (KokoroEngineConfig.NanRescue).
  The check prints one row per read (multi-thread and single-thread takes, guard lines) and a summary per arm (NaN takes) and per outcome (rescued by a single-thread retry, gave up), so one
  phone run shows whether NaN comes with multiple threads only. Passes when every read ended, every read has a probe line and none gave up. Default is bm_daniel and bf_alice, 3 reads of each of the 4 lines. Before EVERY read the check deletes the app's sentence-audio cache (`run-as <pkg> rm -rf cache/sentences`; never the packs folder), because a cached line is played with no synthesis and so logs no probe line (the cache index is read from disk once per process, but a vanished file is a miss: SentenceAudioCache.TryGetAsync). It refuses to run unless the installed app is debuggable (Debug or Debug-AOT build; only those carry the probe and the `read` hook). -AllVoices runs the four that failed. The active kokoro-en pack folder is read from the app's `active` file and logged; the check fails unless it is v<-ExpectedPackVersion> (default 3, the float32-STFT model) or newer, and an adb/run-as failure reading it is reported as that failure.
  Hypothesis and what confirms it: docs/plan/slices/220-kokoro-silent-short-lines-arm64.md.
#>
param(
    [Parameter(Mandatory)][string]$Phone,
    [Parameter(Mandatory)][ValidateSet('dino')][string]$App,
    [string[]]$Voices = @('bm_daniel', 'bf_alice'),
    [switch]$AllVoices,
    [string[]]$Lines = @('Yes.', 'Oh no!', 'Help!', 'Ann.'),
    [int]$Repeats = 3,
    [string]$ExpectedPackVersion = '3',
    [int]$ReadTimeoutSeconds = 40
)
$ErrorActionPreference = 'Stop'
if ($AllVoices) { $Voices = @('bm_daniel', 'bf_alice', 'bf_emma', 'bm_fable') }
. (Join-Path $PSScriptRoot '..\common.ps1')
. (Join-Path $PSScriptRoot '_lib.ps1')
. (Join-Path $PSScriptRoot '_device.ps1')

$package = Get-DevPackage $App
$script:VoiceKey = 'LastNeuralVoiceId.Kokoro'

# Sets the remembered Kokoro voice in the dev app's preferences (the key must exist: pick a Kokoro voice in the app once). The app is stopped first, so nothing rewrites the file over the edit.
function Set-KokoroVoice([string]$VoiceKey) {
    Stop-DevApp $Phone $package
    $key = $script:VoiceKey
    $local = Join-Path ([System.IO.Path]::GetTempPath()) 'rtst-set-kokoro-voice.sh'
    # Prints the value of that one key after the edit (not a match count), so a match on another key cannot hide an unchanged voice.
    $script = ('f=$(grep -l {0} shared_prefs/*.xml | head -1); [ -n "$f" ] || exit 3; sed -i ''s#name=.{0}.>[^<]*<#name="{0}">kokoro:{1}<#'' "$f"; grep -o "name=.{0}.>[^<]*<" "$f" | head -1 | sed ''s#.*>\(.*\)<#\1#''' -f $key, $VoiceKey)
    [System.IO.File]::WriteAllText($local, ($script + "`n"))
    Invoke-Adb $Phone @('push', $local, '/data/local/tmp/rtst-set-kokoro-voice.sh') | Out-Null
    Remove-Item $local -ErrorAction SilentlyContinue
    $out = @(Invoke-Adb $Phone @('shell', "run-as $package sh /data/local/tmp/rtst-set-kokoro-voice.sh"))
    if ("$($out | Select-Object -Last 1)".Trim() -ne "kokoro:$VoiceKey") { throw "Could not set $key to kokoro:$VoiceKey in the preferences of $package (output: $($out -join ' | ')). Pick a Kokoro voice in the app once so the key exists." }
}

# The active kokoro-en pack folder name (the `active` file in the pack's folder, e.g. v3); empty when the pack is not installed. An adb/run-as failure throws (it is not "no pack").
function Get-ActiveKokoroVersion {
    $out = @(Invoke-Adb $Phone @('shell', "run-as $package sh -c 'cat $($script:PacksDirectory)/kokoro-en/active'"))
    $text = ($out | ForEach-Object { "$_" }) -join ' | '
    if ($text -match 'No such file or directory') { return '' }
    if ($text -match '(?i)run-as:|not debuggable|unknown package|inaccessible|error:|device .*not found|offline|unauthorized') { throw "Could not read the kokoro-en pack version of $package from the phone (adb said: $text)." }
    return "$($out | Select-Object -First 1)".Trim()
}

# True when the pack folder name (v3, v4, ...) is the expected version or newer.
function Test-PackVersionAtLeast([string]$Active, [string]$Expected) {
    return ($Active -match '^v(\d+)$') -and ([int]$Matches[1] -ge [int]$Expected)
}

# The sentence-audio cache is cache/sentences in the app's data folder (AndroidSupertonicSpeechReader SentenceFolderName, under FileSystem.CacheDirectory); a cached line is played with no synthesis, so no probe line. Never the packs folder (files/packs).
$sentenceCachePath = 'cache/sentences'
function Clear-SentenceCache {
    $out = @(Invoke-Adb $Phone @('shell', "run-as $package sh -c 'rm -rf $sentenceCachePath; [ -e $sentenceCachePath ] && echo STILL_THERE || echo CLEARED'"))
    if ("$($out | Select-Object -Last 1)".Trim() -ne 'CLEARED') { throw "Could not clear $sentenceCachePath of $package (output: $($out -join ' | '))." }
}

# The probe needs a Debug-flavoured build (its debug log sink and the `read` hook are #if DEBUG); the JIT and the AOT dev builds both are. A release build is not debuggable.
function Assert-DebuggableBuild {
    $out = @(Invoke-Adb $Phone @('shell', "dumpsys package $package"))
    if (-not ($out | Where-Object { "$_" -match 'flags=\[.*\bDEBUGGABLE\b' })) { throw "$package on the phone is not a debuggable (Debug) build, so it carries no short line probe. Reinstall the Debug or Debug-AOT build." }
}

function Read-OneLine([string]$AppPid, [string]$Text) {
    $sent = Send-DevHook $Phone $App 'read' @{ text = $Text; speed = '1' }
    $end = Wait-LogMatch $Phone $package $AppPid 'READ_END\s*$' $ReadTimeoutSeconds $sent "waiting for READ_END of a $($Text.Length)-char line"
    $log = Get-PidLog $Phone $AppPid ($sent - 1)
    return [pscustomobject]@{ Ended = [bool]$end; Facts = ConvertFrom-ShortLineLog $log }
}

# The probe (6 extra syntheses per short line) runs only while this marker file is in the app's cache folder (ShortLineProbe.MarkerFileName); removed again at the end.
$markerPath = 'cache/rtst-short-line-probe'
function Set-ProbeMarker([bool]$On) {
    $cmd = if ($On) { "touch $markerPath" } else { "rm -f $markerPath" }
    Invoke-Adb $Phone @('shell', "run-as $package sh -c '$cmd'") | Out-Null
}

$rows = @()
$activeVersion = Get-ActiveKokoroVersion
Write-Host "   active kokoro-en pack: '$activeVersion' (expected 'v$ExpectedPackVersion')"
if (-not $activeVersion) { return (New-CheckResult $false "no active kokoro-en pack on the phone (expected v$ExpectedPackVersion or newer): install the pack first") }
if (-not (Test-PackVersionAtLeast $activeVersion $ExpectedPackVersion)) { return (New-CheckResult $false "active kokoro-en pack is '$activeVersion', expected v$ExpectedPackVersion or newer: update the pack on the phone first") }
Assert-DebuggableBuild
Set-ProbeMarker $true
try {
    foreach ($voice in $Voices) {
        Set-KokoroVoice $voice
        $appPid = Start-ReadyApp $Phone $App $package
        foreach ($line in $Lines) {
            for ($i = 1; $i -le $Repeats; $i++) {
                Clear-SentenceCache
                $one = Read-OneLine $appPid $line
                $row = [pscustomobject]@{ Voice = $voice; Repeat = $i; Chars = $line.Length; Ended = $one.Ended; Probe = $one.Facts.Probe; GaveUp = $one.Facts.GaveUp; Rescue = $one.Facts.Rescue; Retries = $one.Facts.Retries }
                $rows += $row
                Write-Host ("   {0} {1} chars: {2}" -f $voice, $line.Length, (Format-ShortLineRow $row))
            }
        }
    }
    Send-DevHook $Phone $App 'stop-read' | Out-Null
}
finally { Set-ProbeMarker $false }
$summary = Get-ShortLineSummary $rows
Write-Host "   $summary"
$probes = @($rows | Where-Object { $_.Probe }).Count
if ($probes -eq 0) { return (New-CheckResult $false 'no short line probe log line: needs a Debug build of this slice and the kokoro-en pack') }
$gaveUp = @($rows | Where-Object { $_.GaveUp }).Count
$notEnded = @($rows | Where-Object { -not $_.Ended }).Count
$noProbe = @($rows | Where-Object { -not $_.Probe }).Count
$ok = (Test-PackVersionAtLeast $activeVersion $ExpectedPackVersion) -and ($gaveUp -eq 0) -and ($notEnded -eq 0) -and ($noProbe -eq 0)
return (New-CheckResult $ok "kokoro-en $activeVersion, $($rows.Count) reads, $gaveUp gave up, $notEnded never ended, $noProbe reads without a probe line. $summary")
