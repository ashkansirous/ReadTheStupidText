<#
.SYNOPSIS
  cold-start: N fresh-process reads, each truly cold (Slice 213). Prints first audio, engine-ready and first-synthesis time per run, then the median.
.DESCRIPTION
  Per run: force-stop the dev app, EMPTY THE SENTENCE AUDIO CACHE (<app data>/cache/sentences, written by SentenceAudioCache through
  FileSentenceStore, AndroidSupertonicSpeechReader.SentenceFolderName), launch with skip-first-run, wait for the installed voice pack, send the read hook, and read the numbers
  from the app's own markers (HOOK read start -> first CHUNK, ENGINE_READY, CHUNK_SYNTH 0 end). Without the cache clear, repeat runs read sentences from disk and
  measured 240-578 ms, which is not a cold start. RTST reads the fixed passage; Dino opens a real starter page (same hooks as audio-timing.ps1).
#>
param(
    [Parameter(Mandatory)][string]$Phone,
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [int]$Runs = 3,
    [string]$Book = 'three-little-pigs',
    [int]$ChunkTimeoutSeconds = 90,
    [ValidateSet('1w', '1s', '2s', 'passage')][string]$Preset = 'passage',   # the first-read text: 1 word, 1 sentence, 2 sentences, or the fixed passage (Slice 214)
    [string]$Text,                                                           # free text instead of a preset (RTST only)
    [switch]$DropCaches                                                      # also empty the phone's file cache each run (needs root; see Clear-PageCache)
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\common.ps1')
. (Join-Path $PSScriptRoot '_lib.ps1')
. (Join-Path $PSScriptRoot '_device.ps1')

$package = Get-DevPackage $App
$firstRead = Resolve-ColdStartText $Preset $Text
if ($App -eq 'dino' -and ($Text -or $Preset -ne 'passage')) { Write-Host "   WARNING: -Preset/-Text do not apply to Dino (it reads a fixed book page); ignored." }
if ($App -eq 'dino') { $firstRead = [pscustomobject]@{ Label = 'book page'; Text = $null } }   # Dino reads a fixed book page: -Preset and -Text do not apply, and the report says so
$script:DropCachesWorked = $null

function Send-ColdRead([string]$AppPid) {
    $hook = 'read'; $hookArgs = @{ text = $(if ($firstRead.Text) { $firstRead.Text } else { $script:ReaderPassage }); speed = '1' }
    if ($App -eq 'dino') { $hook = 'open-book'; $hookArgs = @{ id = $Book; page = '1'; play = 'page'; speed = '1' } }
    $sent = Send-DevHook $Phone $App $hook $hookArgs
    $answer = Wait-LogMatch $Phone $package $AppPid 'HOOK (read (start|failed)|open-book (failed|fault))' $script:HookAnswerSeconds $sent 'waiting for the app to accept the read'
    if (-not $answer -or $answer.Line -notmatch 'HOOK read start') { throw "The read hook was not accepted: $(if ($answer) { $answer.Line } else { 'no HOOK line in 30 s' })" }
    return $answer.Time
}

function Measure-OneColdRun([int]$Index) {
    Assert-WithinBudget "starting cold run $Index"
    Write-Host "   run ${Index}: force-stop, empty the sentence cache, launch"
    Stop-DevApp $Phone $package
    Clear-SentenceCache $Phone $package
    if ($DropCaches) {
        $dropped = Clear-PageCache $Phone
        if ($null -eq $script:DropCachesWorked) { $script:DropCachesWorked = $dropped; Write-Host $(if ($dropped) { '   file cache dropped each run (root)' } else { '   WARNING: -DropCaches needs root and this phone has none: runs are force-stop cold only (model files stay in the file cache). Use -Reinstall or reboot for the fresh-install case.' }) }
    }
    Set-DevNoUsageMarker $Phone $package
    $appPid = Start-ReadyApp $Phone $App $package
    $tap = Send-ColdRead $appPid
    if (-not (Wait-LogMatch $Phone $package $appPid 'CHUNK 1\s*$' $ChunkTimeoutSeconds $tap 'waiting for the first audio')) { throw "No audio (CHUNK 1) within $ChunkTimeoutSeconds s of the read." }
    $numbers = Get-ColdStartNumbers @(ConvertFrom-TimingLog (Get-PidLog $Phone $appPid)) $tap
    Send-DevHook $Phone $App 'stop-read' | Out-Null
    Write-Host ("   run {0} [{1}]: first audio {2} ms, engine {3} ms, first synthesis {4} ms, chunk 0 audio {5} ms" -f $Index, $firstRead.Label, $numbers.FirstAudioMs, $numbers.EngineMs, $numbers.FirstSynthMs, $numbers.FirstAudioLengthMs)
    return $numbers
}

$measured = @(1..$Runs | ForEach-Object { Measure-OneColdRun $_ })
$first = @($measured | ForEach-Object { $_.FirstAudioMs })
if (@($first | Where-Object { $null -eq $_ }).Count -gt 0) { return (New-CheckResult $false 'a run produced no first-audio number') }
# No pass criterion: the numbers are reported as MEASURED, never PASS (a slow start must not read as "cold-start passed").
$detail = '[{5}{6}] {0} cold runs, first audio median {1} ms (min {2}, max {3}); {4}' -f $measured.Count, (Get-Median $first), ($first | Measure-Object -Minimum).Minimum, ($first | Measure-Object -Maximum).Maximum, (Format-ColdStartRuns $measured), $firstRead.Label, $(if ($DropCaches -and $script:DropCachesWorked) { ', page cache dropped' } else { '' })
return (New-CheckResult $true $detail $true)   # Measured: MEASURED in the table
