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
    [int]$ChunkTimeoutSeconds = 90
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\common.ps1')
. (Join-Path $PSScriptRoot '_lib.ps1')
. (Join-Path $PSScriptRoot '_device.ps1')

$package = Get-DevPackage $App

function Send-ColdRead([string]$AppPid) {
    $hook = 'read'; $hookArgs = @{ text = $script:ReaderPassage; speed = '1' }
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
    Set-DevNoUsageMarker $Phone $package
    $appPid = Start-ReadyApp $Phone $App $package
    $tap = Send-ColdRead $appPid
    if (-not (Wait-LogMatch $Phone $package $appPid 'CHUNK 1\s*$' $ChunkTimeoutSeconds $tap 'waiting for the first audio')) { throw "No audio (CHUNK 1) within $ChunkTimeoutSeconds s of the read." }
    $numbers = Get-ColdStartNumbers @(ConvertFrom-TimingLog (Get-PidLog $Phone $appPid)) $tap
    Send-DevHook $Phone $App 'stop-read' | Out-Null
    Write-Host ("   run {0}: first audio {1} ms, engine {2} ms, first synthesis {3} ms" -f $Index, $numbers.FirstAudioMs, $numbers.EngineMs, $numbers.FirstSynthMs)
    return $numbers
}

$measured = @(1..$Runs | ForEach-Object { Measure-OneColdRun $_ })
$first = @($measured | ForEach-Object { $_.FirstAudioMs })
if (@($first | Where-Object { $null -eq $_ }).Count -gt 0) { return (New-CheckResult $false 'a run produced no first-audio number') }
# No pass criterion: the numbers are reported as MEASURED, never PASS (a slow start must not read as "cold-start passed").
$detail = '{0} cold runs, first audio median {1} ms (min {2}, max {3}); {4}' -f $measured.Count, (Get-Median $first), ($first | Measure-Object -Minimum).Minimum, ($first | Measure-Object -Maximum).Maximum, (Format-ColdStartRuns $measured)
return (New-CheckResult $true $detail $true)   # Measured: MEASURED in the table
