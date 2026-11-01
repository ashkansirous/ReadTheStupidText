# check: Apps=rtst EstimateSeconds=90 NeedsPack=true Variant=debug
<#
.SYNOPSIS
  voice: pick a voice through the Debug `set-voice` hook (RTST, Slice 209, Slice 213 runner).
.DESCRIPTION
  Sends `set-voice id=<id>` (the voice picker's own path: VoicePickerPage.ApplyVoice stores it and tells the reader), requires `HOOK set-voice ok <id>`, then a `read` must start
  (READ_START) with the picked voice. The id is one of the installed pack's voices (the app answers `failed unknown <id>` for any other, which is a FAIL). Default `auto`: the app picks the first voice of the installed pack that is not the one in use (the pack differs per language; a fixed id such as F3 is refused when the pack has no such voice).
  The Settings page showing the new name (Slice 99) is the Maestro flow's job (.maestro/rtst/set-voice.yaml): this check proves the hook path and that a read follows.
  Needs the neural pack, as every read check does.
#>
param(
    [Parameter(Mandatory)][string]$Phone,
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [string]$VoiceId = 'auto',
    [int]$HookSeconds = 30
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\common.ps1')
. (Join-Path $PSScriptRoot '_lib.ps1')
. (Join-Path $PSScriptRoot '_device.ps1')

$package = Get-DevPackage $App
if ($App -ne 'rtst') { return (New-CheckResult $false 'set-voice exists in RTST only (Dino picks voices per character on the cast screen).') }

$appPid = Start-ReadyApp $Phone $App $package
$sent = Send-DevHook $Phone $App 'set-voice' @{ id = $VoiceId }
$answer = Wait-LogMatch $Phone $package $appPid 'HOOK set-voice (ok|failed)' $HookSeconds $sent 'waiting for set-voice'
if (-not $answer) { return (New-CheckResult $false "no 'HOOK set-voice' line within $HookSeconds s") }
if ($answer.Line -notmatch 'HOOK set-voice ok (\S+)\s*$') { return (New-CheckResult $false "set-voice id=$VoiceId was refused: $(($answer.Line -split 'RTST-TEST ')[-1].Trim())") }
$picked = $Matches[1]
if ($VoiceId -ne 'auto' -and $picked -ne $VoiceId) { return (New-CheckResult $false "set-voice id=$VoiceId answered ok for a different voice: $picked") }
$read = Send-DevHook $Phone $App 'read' @{ text = $script:ReaderPassage; speed = '2' }
$started = Wait-LogMatch $Phone $package $appPid 'READ_START' 60 $read 'waiting for READ_START'
Send-DevHook $Phone $App 'stop-read' | Out-Null
if (-not $started) { return (New-CheckResult $false "set-voice id=$picked was accepted but no read started within 60 s") }
return (New-CheckResult $true "set-voice picked $picked (id=$VoiceId) and a read started with it")
