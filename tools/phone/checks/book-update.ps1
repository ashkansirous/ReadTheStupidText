# check: Apps=dino EstimateSeconds=240 NeedsPack=false Variant=debug
<#
.SYNOPSIS
  book-update: stage the store's newer version of a Dino book, cold-start, and time the apply (Slice 209 hook, Slice 181 apply, Slice 213 runner).
.DESCRIPTION
  1. Fresh process, skip-first-run. 2. `stage-book-update id=<book>` downloads the store's newer version into book-updates/<id>/v<n>.dino (BookUpdater.StageNowAsync, the staging the
  7-day check uses) and the app logs `HOOK stage-book-update ok <id> v<n>`; a `failed <reason>` line (offline, not in the store, edited, up to date, not on the shelf) is a FAIL with that reason
  (the check passes `force=true`, so a copy that already matches the store is staged anyway: its recorded version is set one lower and the hook logs `ok <id> v<n> forced`; the apply then swaps in the same version). 3. force-stop, cold start again. 4. GalleryPage applies the staged
  package before it first loads; the `[BookUpdate] apply start` / `apply end after <ms> ms` lines (StagedBookUpdates) time it and `<id>: swapped to v<n>` proves the swap. Reports the
  milliseconds (MEASURED, no pass criterion) and fails when the swap line is missing. Dino only.
#>
param(
    [Parameter(Mandatory)][string]$Phone,
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [string]$Book = 'three-little-pigs',
    [int]$StageSeconds = 120,
    [int]$ApplySeconds = 90
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\common.ps1')
. (Join-Path $PSScriptRoot '_lib.ps1')
. (Join-Path $PSScriptRoot '_device.ps1')

$package = Get-DevPackage $App
if ($App -ne 'dino') { return (New-CheckResult $false 'book updates exist in Dino Reader only.') }

function Stage-Book([string]$AppPid) {
    $sent = Send-DevHook $Phone $App 'stage-book-update' @{ id = $Book; force = 'true' }   # force: stage the store's version even when the phone's copy already matches it (the apply then swaps in the same version)
    $answer = Wait-LogMatch $Phone $package $AppPid 'HOOK stage-book-update (ok|failed)' $StageSeconds $sent 'waiting for the book to be staged'
    if (-not $answer) { throw "No 'HOOK stage-book-update ok|failed' line within $StageSeconds s." }
    if ($answer.Line -notmatch 'HOOK stage-book-update ok (\S+) v(\d+)') { throw "stage-book-update id=$Book did not stage: $(($answer.Line -split 'RTST-TEST ')[-1].Trim())" }
    return "$($Matches[1]) v$($Matches[2])"
}

$appPid = Start-DevAppWithHook $Phone $package
if (-not (Wait-LogMatch $Phone $package $appPid 'HOOK skip-first-run ok' 60 0 'waiting for the app to skip first run')) { throw 'The skip-first-run hook did not report ok within 60 s.' }
$staged = Stage-Book $appPid
$stageLines = @(Get-PidLog $Phone $appPid | Where-Object { $_ -match '\[BookUpdate\]' } | ForEach-Object { ($_ -split 'I\s+\S+\s*:\s*', 2)[-1].Trim() })
Write-Host "   staged $staged; restarting cold to apply it"
Stop-DevApp $Phone $package
$appPid = Start-DevAppWithHook $Phone $package
$end = Wait-LogMatch $Phone $package $appPid '\[BookUpdate\] apply end after \d+ ms' $ApplySeconds 0 'waiting for the staged book to be applied'
function Format-BookUpdateLines([string[]]$Stage, [string[]]$Apply) {
    return " | stage process: $($Stage -join ' ; ') | apply process: $($Apply -join ' ; ')"
}
$log = Get-PidLog $Phone $appPid
$applyLines = @($log | Where-Object { $_ -match '\[BookUpdate\]' } | ForEach-Object { ($_ -split 'I\s+\S+\s*:\s*', 2)[-1].Trim() })
if (-not $end) { return (New-CheckResult $false ("staged $staged, but no '[BookUpdate] apply end' line within $ApplySeconds s of the cold start" + (Format-BookUpdateLines $stageLines $applyLines))) }
$swapped = @($log | Where-Object { $_ -match '\[BookUpdate\] \S+: swapped to v\d+' }) | Select-Object -First 1
$ms = if ($end.Line -match 'apply end after (\d+) ms') { [int]$Matches[1] } else { -1 }
if (-not $swapped) { return (New-CheckResult $false ("staged $staged and the apply ran ($ms ms) but no '<id>: swapped to v<n>' line: the book was dropped or left" + (Format-BookUpdateLines $stageLines $applyLines))) }
return (New-CheckResult $true "staged $staged; the cold-start apply took $ms ms and swapped the book" $true)
