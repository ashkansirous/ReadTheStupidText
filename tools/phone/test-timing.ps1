<#
.SYNOPSIS
  Self-test for ConvertFrom-TimingLog and Get-ReadTimings (timing.ps1): parses a saved logcat fixture and checks the engine,
  synthesis and first-audio columns. Run it in both `powershell` (5.1) and `pwsh`. Needs no device.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'timing.ps1')

$failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

$lines = Get-Content (Join-Path $PSScriptRoot 'fixtures/timing-log.txt')
$events = @(ConvertFrom-TimingLog $lines)
Assert-Equal 'event count (noise line skipped)' 10 $events.Count
Assert-Equal 'hook outcome' 'start' ($events | Where-Object Kind -eq 'hook').Outcome
Assert-Equal 'chunk events' 2 @($events | Where-Object Kind -eq 'chunk').Count

$timing = Get-ReadTimings $events 1000.0 1.0 500
Assert-Equal 'complete' $true $timing.Complete
Assert-Equal 'not misaligned' $false $timing.Misaligned
Assert-Equal 'engine ms' 480 $timing.EngineMs
Assert-Equal 'first synth ms' 400 $timing.FirstSynthMs
Assert-Equal 'first audio ms' 2100 $timing.FirstAudioMs
Assert-Equal 'row count' 2 $timing.Rows.Count
Assert-Equal 'chunk 1 audio ms' 3000 $timing.Rows[0].AudioMs
Assert-Equal 'chunk 1 audio ready ms' 2000 $timing.Rows[0].AudioReadyMs
Assert-Equal 'chunk 1 gap (3000 ms apart minus 3000 played)' 0 $timing.Rows[0].GapAfterMs
Assert-Equal 'total ms' 7000 $timing.TotalMs
Assert-Equal 'expected ms' 5000 $timing.ExpectedMs

$half = Get-ReadTimings $events 1000.0 2.0 500
Assert-Equal 'rate 2 halves play ms' 1500 $half.Rows[0].PlayMs

$partial = Get-ReadTimings @($events | Where-Object Kind -ne 'end') 1000.0 1.0 500
Assert-Equal 'no READ_END is incomplete' $false $partial.Complete

if ($failed) { Write-Host "$failed timing checks failed (PowerShell $($PSVersionTable.PSVersion))."; exit 1 }
Write-Host "All timing checks pass (PowerShell $($PSVersionTable.PSVersion))."
