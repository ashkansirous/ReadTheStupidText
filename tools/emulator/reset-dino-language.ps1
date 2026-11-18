<#
.SYNOPSIS
  Slice 393 item 15: puts the Dino dev install's in-app language back to English on a warm emulator, with the Debug `set-language` hook.
.DESCRIPTION
  A flow that switches the in-app language (`set-language lang=de`, `fa`, `ar`, ...) and does not clearState leaves it there for the next run: wave 5's device run left emulator-5554 on German.
  A device-test run calls this once before its first flow and once after its last. The script launches the Debug app first (the hook is a broadcast only a running app receives) and sends the broadcast up to -TryCount times, -TryWaitSeconds apart, until the log shows it. It runs inside the device-test run that holds the emulator lease.
  Exit 0 when the app logged `HOOK set-language ok`, 1 when it did not (not running, a Release build, or the hook failed), 6 when no emulator answers. -DryRun prints the adb command and runs nothing.
.EXAMPLE
  .\reset-dino-language.ps1 -Instance 1
  .\reset-dino-language.ps1 -DryRun
#>
param(
    [ValidateSet('1', '2', '3', 'tablet')][string]$Instance = '1',
    [string]$Language = 'en',
    [int]$TryCount = 6,
    [int]$TryWaitSeconds = 3,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

# The broadcast the Dino Debug app listens for (uk.sirous.dino-test.TEST_HOOK, tools/phone/common.ps1 HookActionFormat) as one adb argument list. Pure.
function Get-LanguageResetArguments([string]$Language) {
    if ($Language -notmatch '^[a-z]{2,3}(-[A-Za-z]{2,4})?$') { throw "Not a language code: '$Language'." }
    return @('shell', 'am', 'broadcast', '-a', 'uk.sirous.dino-test.TEST_HOOK', '--es', 'cmd', 'set-language', '--es', 'lang', $Language)
}

# True when the log text holds the hook's success line. Pure.
function Test-LanguageResetLogged([string]$LogText) {
    return [bool]($LogText -match '(?m)HOOK set-language ok\b')
}

if ($MyInvocation.InvocationName -eq '.') { return }   # dot-sourced by a self-test: functions only

Set-EmulatorInstance (Resolve-EmulatorInstance $Instance)
$arguments = Get-LanguageResetArguments $Language
if ($DryRun) { Write-Host "RESET LANGUAGE dry run: adb -s $script:Serial $($arguments -join ' ')"; exit 0 }
if (-not (Test-EmulatorOnline)) { Write-Host "RESET LANGUAGE: no emulator answers on $script:Serial."; exit 6 }
$null = Invoke-EmulatorAdbText @('shell', 'monkey', '-p', $script:DinoDevPackage, '-c', 'android.intent.category.LAUNCHER', '1')   # the hook is a broadcast: the Debug app must be running to receive it
for ($try = 1; $try -le $TryCount; $try++) {
    $null = Invoke-EmulatorAdbText @('logcat', '-c')   # cleared before each broadcast: only a line this try's broadcast wrote counts, never a stale one
    $null = Invoke-EmulatorAdbText $arguments
    Start-Sleep -Seconds $TryWaitSeconds   # bounded poll: the receiver may not be registered yet right after the launch, so the broadcast is sent again until the log line shows
    $log = Invoke-EmulatorAdbText @('logcat', '-d', '-s', 'RTST-TEST:I')
    if (Test-LanguageResetLogged $log) { Write-Host "RESET LANGUAGE: in-app language is $Language on $script:Serial (try $try)."; exit 0 }
}
Write-Host "RESET LANGUAGE: no 'HOOK set-language ok' line from the app on $script:Serial after $TryCount tries (a Release build, or the app did not start?)."
exit 1
