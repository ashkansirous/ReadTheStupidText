<#
.SYNOPSIS
  Self-test for Slice 317 item 4: a `# needs: voice` flow is skipped with a "no voice" line only when the RTST-TEST log has no ENGINE_READY marker;
  a flow without the need, or a log with the marker, is never skipped. Pure logic, no device. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
if (-not $env:ANDROID_HOME -and -not $env:ANDROID_SDK_ROOT) { $env:ANDROID_HOME = [IO.Path]::GetTempPath() }
. (Join-Path $PSScriptRoot 'common.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

$voiceless = "I RTST-TEST: HOOK skip-first-run ok`nI RTST-TEST: READ_REQUESTED"
$spoke = "$voiceless`nI RTST-TEST: ENGINE_READY 812`nI RTST-TEST: READ_START"
Assert-Equal 'voice flow, no engine marker, no TTS engine: skipped' $true (Test-NoVoiceSkip @('voice') $voiceless $true)
Assert-Equal 'voice flow, no engine marker but the emulator HAS a TTS engine: a failure, not a skip' $false (Test-NoVoiceSkip @('voice') $voiceless $false)
Assert-Equal 'voice flow, engine ready: not skipped' $false (Test-NoVoiceSkip @('voice') $spoke $true)
Assert-Equal 'flow without the need: not skipped' $false (Test-NoVoiceSkip @('cellular-only') $voiceless $true)
Assert-Equal 'no needs at all: not skipped' $false (Test-NoVoiceSkip @() $voiceless $true)
Assert-Equal 'empty log, voice flow, no engine: skipped' $true (Test-NoVoiceSkip @('voice', 'screen-cycle') '' $true)
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
