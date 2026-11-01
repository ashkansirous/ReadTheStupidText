# check: Apps=rtst,dino EstimateSeconds=180 NeedsPack=true Variant=debug
<#
.SYNOPSIS
  language: switch the app's UI language through the Debug `set-language` hook, language after language (Slice 209, Slice 213 runner).
.DESCRIPTION
  Per language: `set-language lang=<code>` (RTST: the Settings Language row's path, LanguageChooser.Apply, then the shell is rebuilt; Dino: the Language page's path, then RebuildRoot;
  Dino is sent voice=0 on an emulator, which skips the voice-pack start that crashes its Debug x86_64 build, and voice=1 on a real phone), wait for `HOOK set-language ok <code>` (a `failed` line is a FAIL with the app's reason), then the
  `state` hook must report `language=<code>` (STATE line, RTST and Dino both write it) and the app process must still be alive. With -Read (RTST only, needs the neural pack) a short
  `read` follows and READ_START must appear. The default languages are the ones the S21 night skipped (sv da de fa nb zh), then en to return to the start.
#>
param(
    [Parameter(Mandatory)][string]$Phone,
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [string[]]$Languages = @('sv', 'da', 'de', 'fa', 'nb', 'zh', 'en'),
    [switch]$Read,
    [int]$HookSeconds = 30,
    [int]$VoiceStoreSeconds = 250
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\common.ps1')
. (Join-Path $PSScriptRoot '_lib.ps1')
. (Join-Path $PSScriptRoot '_device.ps1')

$package = Get-DevPackage $App

function Switch-Language([string]$AppPid, [string]$Code) {
    $hookArgs = @{ lang = $Code }
    if ($App -eq 'dino') { $hookArgs.voice = $(if ($Phone -like 'emulator-*') { '0' } else { '1' }) }   # the voice start crashes the Debug x86_64 emulator build; a real phone exercises it
    $sent = Send-DevHook $Phone $App 'set-language' $hookArgs
    $wait = if ($hookArgs.voice -eq '1') { $VoiceStoreSeconds } else { $HookSeconds }   # Dino with the voice start answers ok only once the language is stored, which waits for the last language's pack download
    $answer = Wait-LogMatch $Phone $package $AppPid 'HOOK set-language (ok|failed)' $wait $sent "waiting for set-language $Code"
    if (-not $answer) { throw "No 'HOOK set-language' line within $wait s of set-language lang=$Code." }
    if ($answer.Line -notmatch 'HOOK set-language ok \S+\s*$') { throw "set-language lang=$Code was refused: $(($answer.Line -split 'RTST-TEST ')[-1].Trim())" }
    return $answer.Time
}

function Assert-LanguageStored([string]$AppPid, [string]$Code, [double]$After) {
    $sent = Send-DevHook $Phone $App 'state'
    $state = Wait-LogMatch $Phone $package $AppPid "STATE language=$Code\b" $HookSeconds ([math]::Max($sent, $After)) "waiting for STATE language=$Code"
    if (-not $state) { throw "After set-language lang=$Code the state hook did not report language=$Code." }
}

function Read-OneLine([string]$AppPid) {
    $sent = Send-DevHook $Phone $App 'read' @{ text = 'One short line to read.'; speed = '2' }
    if (-not (Wait-LogMatch $Phone $package $AppPid 'READ_START' 60 $sent 'waiting for READ_START')) { throw 'No READ_START within 60 s of the read hook after the language change.' }
    Send-DevHook $Phone $App 'stop-read' | Out-Null
}

$appPid = Start-DevAppWithHook $Phone $package
if (-not (Wait-LogMatch $Phone $package $appPid 'HOOK skip-first-run ok' 60 0 'waiting for the app to skip first run')) { throw 'The skip-first-run hook did not report ok within 60 s.' }
$done = @()
foreach ($code in $Languages) {
    $after = Switch-Language $appPid $code
    Assert-LanguageStored $appPid $code $after
    if ($Read -and $App -eq 'rtst') { Read-OneLine $appPid }
    $done += $code
    Write-Host "   language $code ok"
}
return (New-CheckResult $true "switched through $($done -join ', ') with set-language; each reported ok and the stored language matched$(if ($Read -and $App -eq 'rtst') { ', a read started after each' })")
