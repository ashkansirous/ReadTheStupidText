<#
.SYNOPSIS
  Self-test for Slice 253 item 1 (dialog-guard.ps1) and item 5 (the closure scoping that hid a bug from local runs). Needs no device. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
if (-not $env:ANDROID_HOME -and -not $env:ANDROID_SDK_ROOT) { $env:ANDROID_HOME = [IO.Path]::GetTempPath() }
. (Join-Path $PSScriptRoot 'common.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

Assert-Equal 'on: setting put' 'settings put global hide_error_dialogs 1' ((Get-HideErrorDialogsOnArguments) -join ' ')
Assert-Equal 'close: broadcast' 'am broadcast -a android.intent.action.CLOSE_SYSTEM_DIALOGS' ((Get-CloseSystemDialogsArguments) -join ' ')
Assert-Equal 'restore: absent setting is deleted' 'settings delete global hide_error_dialogs' ((Get-HideErrorDialogsRestoreArguments 'null') -join ' ')
Assert-Equal 'restore: empty read is deleted' 'settings delete global hide_error_dialogs' ((Get-HideErrorDialogsRestoreArguments '') -join ' ')
Assert-Equal 'restore: earlier 0 is put back' 'settings put global hide_error_dialogs 0' ((Get-HideErrorDialogsRestoreArguments "0`r`n") -join ' ')

Assert-Equal 'prior: a leftover 1 counts as not set' '' (Resolve-PriorHideDialogs "1`r`n")
Assert-Equal 'prior: an earlier 0 is kept' '0' (Resolve-PriorHideDialogs '0')
Assert-Equal 'prior: null stays null' 'null' (Resolve-PriorHideDialogs 'null')
Assert-Equal 'restore without enable does nothing' $true ($null -eq (Restore-DialogGuard))
$script:HideDialogsPending = $true; $script:DialogGuardLeaseLost = $true
$script:Adb = 'adb-must-not-run'; $script:Serial = 'emulator-0'
Restore-DialogGuard 6>$null
Assert-Equal 'restore after lease loss leaves the setting and clears pending' $false $script:HideDialogsPending
$script:DialogGuardLeaseLost = $false

Assert-Equal 'screen cycle: sleep, wake, dismiss' 'input keyevent KEYCODE_SLEEP|input keyevent KEYCODE_WAKEUP|wm dismiss-keyguard' ((Get-ScreenCycleCommands) -join '|')

# run-flows.ps1 must wire the guard in: set before the flows, closed before each flow, restored at the end.
$runFlows = Get-Content (Join-Path $PSScriptRoot 'run-flows.ps1') -Raw
Assert-Equal 'run-flows enables the guard' $true ($runFlows -match 'Enable-DialogGuard')
Assert-Equal 'run-flows restores the guard' $true ($runFlows -match 'Restore-DialogGuard')
Assert-Equal 'run-flows closes dialogs before a flow' $true ($runFlows -match '(?s)function Wake-Emulator.*?Close-SystemDialogs')

Assert-Equal 'run-flows starts and stops the screen cycle' $true ($runFlows -match '(?s)Start-ScreenCycle \}.*?Stop-ScreenCycle \$cycleJob')

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-dialog-guard: all passed.'
exit 0
