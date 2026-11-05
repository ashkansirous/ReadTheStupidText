# Slice 253 item 1: the "System UI isn't responding" dialog guard, in the tool instead of in the briefs. Dot-sourced by speed.ps1, not run. Pure parts are covered by test-dialog-guard.ps1.
# Five emulator runs on 2026-11-01 were lost to that dialog (tablet AVD, CPU-starved boot). `hide_error_dialogs 1` stops the system showing crash and ANR dialogs at all (the crash watch reads logcat,
# not the dialog), and CLOSE_SYSTEM_DIALOGS before each flow closes any that is already up. run-flows.ps1 sets the value for every AVD at the start of the flows and puts the old value back at the end.
$script:HideErrorDialogsSetting = 'hide_error_dialogs'
$script:CloseSystemDialogsAction = 'android.intent.action.CLOSE_SYSTEM_DIALOGS'

# Pure: the `adb shell` argument list that switches the dialogs off.
function Get-HideErrorDialogsOnArguments { return @('settings', 'put', 'global', $script:HideErrorDialogsSetting, '1') }

# Pure: the `adb shell` argument list that closes any system dialog now.
function Get-CloseSystemDialogsArguments { return @('am', 'broadcast', '-a', $script:CloseSystemDialogsAction) }

# Pure: the `adb shell` argument list that puts the setting back. $Prior is what `settings get` printed: nothing or "null" means the setting did not exist, so it is deleted again.
function Get-HideErrorDialogsRestoreArguments([string]$Prior) {
    $value = "$Prior".Trim()
    if (-not $value -or $value -eq 'null') { return @('settings', 'delete', 'global', $script:HideErrorDialogsSetting) }
    return @('settings', 'put', 'global', $script:HideErrorDialogsSetting, $value)
}

# Pure: the value to restore later. This tool is the only thing here that sets the value to 1, so a "1" found before switching it on is a leftover from a run that was killed (or lost its lease)
# before it could restore: it is treated as "was not set", so the leftover is deleted at the end of this run instead of being kept for ever. Any other value is the emulator's own.
function Resolve-PriorHideDialogs([string]$Read) {
    $value = "$Read".Trim()
    if ($value -eq '1') { return '' }
    return $value
}

# Switches the dialogs off on the current emulator. The value to restore is kept in $script:HideDialogsPrior, set BEFORE the setting is changed, so Restore-DialogGuard works on every exit path
# of the script (error, lease loss, Ctrl+C). A process kill cannot be caught: the next run's Enable-DialogGuard then finds the leftover 1 and clears it as above.
function Enable-DialogGuard {
    $ErrorActionPreference = 'Continue'
    $read = (& $script:Adb -s $script:Serial shell settings get global $script:HideErrorDialogsSetting | Out-String).Trim()
    $script:HideDialogsPrior = Resolve-PriorHideDialogs $read
    $script:HideDialogsPending = $true
    & $script:Adb -s $script:Serial shell @(Get-HideErrorDialogsOnArguments) | Out-Null
    Write-Host "DIALOGGUARD on $($script:Serial): hide_error_dialogs was '$read'$(if ($read -eq '1') { ' (a leftover from an earlier run; it will be cleared)' }), now 1."
}

# Puts the setting back once, if Enable-DialogGuard started; safe to call from any finally.
function Restore-DialogGuard {
    if (-not $script:HideDialogsPending) { return }
    $script:HideDialogsPending = $false
    if ($script:DialogGuardLeaseLost) {   # another run owns the emulator now and may have switched the guard on itself: its value is not this run's to change
        Write-Host "DIALOGGUARD left as is on $($script:Serial): this run lost the emulator lease, so hide_error_dialogs is not restored (the run that holds it now owns the setting)."
        return
    }
    $ErrorActionPreference = 'Continue'
    & $script:Adb -s $script:Serial shell @(Get-HideErrorDialogsRestoreArguments $script:HideDialogsPrior) | Out-Null
    Write-Host "DIALOGGUARD off on $($script:Serial): hide_error_dialogs back to '$($script:HideDialogsPrior)'."
}

function Close-SystemDialogs {
    $ErrorActionPreference = 'Continue'
    & $script:Adb -s $script:Serial shell @(Get-CloseSystemDialogsArguments) | Out-Null
}
