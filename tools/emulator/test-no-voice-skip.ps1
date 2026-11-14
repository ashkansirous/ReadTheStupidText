<#
.SYNOPSIS
  Self-test for Slice 317 item 4: a `# needs: voice` flow is skipped (before it runs) only when adb's TTS query contains "No services found";
  a flow without the need, or an emulator with an engine or an unknown answer, is never skipped. Pure logic, no device. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
if (-not $env:ANDROID_HOME -and -not $env:ANDROID_SDK_ROOT) { $env:ANDROID_HOME = [IO.Path]::GetTempPath() }
. (Join-Path $PSScriptRoot 'common.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

Assert-Equal 'voice flow, no TTS engine: skipped' $true (Test-NoVoiceSkip @('voice') $true)
Assert-Equal 'voice flow, the emulator HAS a TTS engine: runs' $false (Test-NoVoiceSkip @('voice') $false)
Assert-Equal 'flow without the need: not skipped' $false (Test-NoVoiceSkip @('cellular-only') $true)
Assert-Equal 'no needs at all: not skipped' $false (Test-NoVoiceSkip @() $true)
Assert-Equal 'several needs including voice, no engine: skipped' $true (Test-NoVoiceSkip @('voice', 'screen-cycle') $true)
Assert-Equal 'one helper decides skipped: a SKIP result' $true (Test-FlowSkipped ([pscustomobject]@{ NotRun = 'SKIP a: no voice' }))
Assert-Equal 'one helper decides skipped: another not-run reason is not a skip' $false (Test-FlowSkipped ([pscustomobject]@{ NotRun = 'app crashed in x' }))
Assert-Equal 'one helper decides skipped: a pass is not a skip' $false (Test-FlowSkipped ([pscustomobject]@{ NotRun = $null }))
Assert-Equal 'extra adb line before the answer still counts' $true (Test-NoTtsEngine "WARNING: linker something`r`nNo services found`r`n")
Assert-Equal 'unknown (adb timeout or empty output): no skip' $false (Test-NoTtsEngine '')
Assert-Equal 'unknown (adb timeout returns null): no skip' $false (Test-NoTtsEngine $null)
Assert-Equal 'adb error text: no skip' $false (Test-NoTtsEngine 'error: device offline')
Assert-Equal 'adb error text with word/word: no skip' $false (Test-NoTtsEngine 'error: and/or closed')
Assert-Equal 'no TTS engine: adb answered "No services found"' $true (Test-NoTtsEngine "No services found`r`n")
Assert-Equal 'TTS engine present: a component line' $false (Test-NoTtsEngine "Services:`n  priority=0 com.google.android.tts/.service.GoogleTTSService")
Assert-Equal 'TTS engine present: bare component' $false (Test-NoTtsEngine 'com.google.android.tts/.service.GoogleTTSService')
$mixed = Format-FlowsSummary 'rtst' @([pscustomobject]@{ Problem = 'x'; NotRun = $null }, [pscustomobject]@{ Problem = 'x'; NotRun = $null }, [pscustomobject]@{ Problem = $null; NotRun = 'SKIP a: no voice' }) 1.0
Assert-Equal 'failures next to a skip keep the crash label and the real counts' 'rtst: 0 passed, 2 failed, 1 not run (app crashed; 1 skipped, no voice) in 1.0 s.' $mixed
$summary = Format-FlowsSummary 'rtst' @([pscustomobject]@{ Problem = $null; NotRun = 'SKIP sentence-highlight: no voice' }, [pscustomobject]@{ Problem = $null; NotRun = $null }) 1.0
Assert-Equal 'a skipped flow counts as not run, never passed' 'rtst: 1 passed, 0 failed, 1 not run (skipped, no voice) in 1.0 s.' $summary
Assert-Equal 'the skip line names the flow and says no voice' $true ((Format-NoVoiceSkipLine 'sentence-highlight') -match '^SKIP sentence-highlight: no voice on this emulator')

if ($script:failed) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-no-voice-skip: all checks passed.'
exit 0
