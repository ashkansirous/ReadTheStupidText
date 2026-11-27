<#
.SYNOPSIS
  Slice 393 item 15: puts the Dino dev install's in-app language back to English on a warm emulator, with the Debug `set-language` hook.
.DESCRIPTION
  A flow that switches the in-app language (`set-language lang=de`, `fa`, `ar`, ...) and does not clearState leaves it there for the next run: wave 5's device run left emulator-5554 on German.
  A device-test run calls this once before its first flow and once after its last. The script launches the Debug app first (the hook is a broadcast only a running app receives) and sends the broadcast up to -TryCount times, -TryWaitSeconds apart, until the log shows it. It runs inside the device-test run that holds the emulator lease.
  Exit 0 when the app logged `HOOK set-language ok`, 1 when it did not (not running, a Release build, or the hook failed), 2 when the Dino dev app is not installed, 6 when no emulator answers. -DryRun prints the adb command and runs nothing.
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

# Slice 442: true when `pm list packages <name>` output lists exactly this package (a longer name that starts with it is not it). Pure.
function Test-PackageListed([string]$PmText, [string]$PackageName) {
    return [bool]($PmText -match "(?im)^package:$([regex]::Escape($PackageName))\s*$")
}

# Slice 442: the `-T` time stamp of a try ('MM-DD HH:MM:SS.mmm', the device's clock) from the output of `date`; $null when it does not look like one. Pure.
function Get-LogcatSinceStamp([string]$DateText) {
    $stamp = $DateText.Trim()
    if ($stamp -match '^\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}$') { return $stamp }
    return $null
}

# Slice 442: the pid line of `pidof`; true when the app runs. Pure.
function Test-PidFound([string]$PidofText) { return [bool]($PidofText -match '^\s*\d+') }

if ($MyInvocation.InvocationName -eq '.') { return }   # dot-sourced by a self-test: functions only

Set-EmulatorInstance (Resolve-EmulatorInstance $Instance)
$arguments = Get-LanguageResetArguments $Language
if ($DryRun) { Write-Host "RESET LANGUAGE dry run: adb -s $script:Serial $($arguments -join ' ')"; exit 0 }
if (-not (Test-EmulatorOnline)) { Write-Host "RESET LANGUAGE: no emulator answers on $script:Serial."; exit 6 }
if (-not (Test-PackageListed (Invoke-EmulatorAdbText @('shell', 'pm', 'list', 'packages', $script:DinoDevPackage)) $script:DinoDevPackage)) {
    Write-Host "RESET LANGUAGE: $script:DinoDevPackage is not installed on $script:Serial (install the Debug build first: tools/emulator/install-debug.ps1)."
    exit 2   # not a hook failure: nothing to reset
}
$launchArguments = @('shell', 'monkey', '-p', $script:DinoDevPackage, '-c', 'android.intent.category.LAUNCHER', '1')
$null = Invoke-EmulatorAdbText $launchArguments   # the hook is a broadcast: the Debug app must be running to receive it
for ($try = 1; $try -le $TryCount; $try++) {
    # Slice 442: no `logcat -c` (it wipes the whole log another reader may need); the device's clock before the broadcast is the `-T` start, so only a line this try's broadcast wrote counts, never a stale one
    Start-Sleep -Seconds 1   # review pass 2 item 2: the stamp is whole seconds (".000"), so a stale ok line logged in the same second would pass `-T`; after this second every earlier line is older than the stamp
    $since = Get-LogcatSinceStamp (Invoke-EmulatorAdbText @('shell', "date +'%m-%d %H:%M:%S.000'"))
    if (-not $since) { $null = Invoke-EmulatorAdbText @('logcat', '-c') }   # review 4: no usable device clock, so the old log is wiped instead (an unbounded read could match a stale ok line)
    $running = Test-PidFound (Invoke-EmulatorAdbText @('shell', 'pidof', $script:DinoDevPackage))
    if (-not $running) { Write-Host "RESET LANGUAGE: try $try, $script:DinoDevPackage has no process yet."; $null = Invoke-EmulatorAdbText $launchArguments; Start-Sleep -Seconds $TryWaitSeconds; continue }   # a broadcast to a dead app is lost: wait for the launch instead
    $null = Invoke-EmulatorAdbText $arguments
    Start-Sleep -Seconds $TryWaitSeconds   # bounded poll: the receiver may not be registered yet right after the launch, so the broadcast is sent again until the log line shows
    $logArguments = if ($since) { @('logcat', '-d', '-T', $since, '-s', 'RTST-TEST:I') } else { @('logcat', '-d', '-s', 'RTST-TEST:I') }   # review 2: ONE argument, no inner quotes: `adb logcat` escapes each argument itself before the device shell sees it
    $log = Invoke-EmulatorAdbText $logArguments
    if (Test-LanguageResetLogged $log) { Write-Host "RESET LANGUAGE: in-app language is $Language on $script:Serial (try $try)."; exit 0 }
}
Write-Host "RESET LANGUAGE: no 'HOOK set-language ok' line from the app on $script:Serial after $TryCount tries (a Release build, or the app did not start?)."
exit 1
