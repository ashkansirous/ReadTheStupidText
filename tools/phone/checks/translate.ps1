# check: Apps=rtst EstimateSeconds=150 NeedsPack=true Variant=debug
<#
.SYNOPSIS
  translate: the Review page's Translate tick set by the Debug `set-translate` hook, then a read that really translates (RTST, Slice 209, Slice 139 race).
.DESCRIPTION
  Mobile Translate is the Review page's per-text tick, so the check opens that page the way a user does (EditLink -> type Italian into ReviewEditor -> tap.ps1), sends
  `set-translate mode=off` and `mode=on` (each must answer `HOOK set-translate ok <mode>`), then taps ReviewRead. Detection runs normally: the Translate row only exists once the text is detected as foreign, so the hook is retried while it
  answers `failed unavailable`. ReviewPage writes `READ translated` when the read really translated: that marker passes. Needs the neural pack, the Italian ML Kit model on the phone and a network.
  Any other `HOOK set-translate failed ...` line is a FAIL with the app's reason (not-on-review: the page was not open).
#>
param(
    [Parameter(Mandatory)][string]$Phone,
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [int]$TranslateSeconds = 90,
    [int]$HookSeconds = 30
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\common.ps1')
. (Join-Path $PSScriptRoot '_lib.ps1')
. (Join-Path $PSScriptRoot '_device.ps1')

$package = Get-DevPackage $App
if ($App -ne 'rtst') { return (New-CheckResult $false 'the Translate tick exists in RTST only.') }
$script:TapScript = Join-Path $PSScriptRoot '..\tap.ps1'
$script:ForeignText = 'Ciao a tutti questa sera leggeremo insieme una lunga storia molto interessante'   # letters and spaces only (no comma or full stop): `input text` takes plain words, and the guard below enforces it
if ($script:ForeignText -notmatch '^[A-Za-z ]+$') { throw 'ForeignText must be ASCII letters and spaces only: adb input text cannot type anything else reliably.' }

function Invoke-Tap([string]$ElementId) {
    Assert-WithinBudget "tapping $ElementId"
    $engine = (Get-Process -Id $PID).Path
    & $engine -NoProfile -File $script:TapScript -Id $ElementId -App $App -Serial $Phone | ForEach-Object { Write-Host "   tap: $_" }
    if ($LASTEXITCODE -ne 0) { throw "tap.ps1 -Id $ElementId exited $LASTEXITCODE (the element was not found or another app is on screen)." }
}

function Set-Tick([string]$AppPid, [string]$Mode) {
    $deadline = (Get-Date).AddSeconds($HookSeconds)
    do {
        $sent = Send-DevHook $Phone $App 'set-translate' @{ mode = $Mode }
        $answer = Wait-LogMatch $Phone $package $AppPid 'HOOK set-translate (ok|failed)' $HookSeconds $sent "waiting for set-translate $Mode"
        if (-not $answer) { throw "No 'HOOK set-translate' line within $HookSeconds s of mode=$Mode." }
        if ($answer.Line -match "HOOK set-translate ok $Mode\s*$") { return }
        if ($answer.Line -notmatch 'failed unavailable\s*$') { throw "set-translate mode=$Mode was refused: $(($answer.Line -split 'RTST-TEST ')[-1].Trim())" }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)
    throw "set-translate mode=$Mode stayed unavailable for $HookSeconds s: the Translate row never showed (no Italian model, or the text was not detected as foreign)."
}

$appPid = Start-ReadyApp $Phone $App $package
Invoke-Tap 'EditLink'
Invoke-Tap 'ReviewEditor'
Invoke-Adb $Phone @('shell', "input text '$($script:ForeignText -replace ' ', '%s')'") | Out-Null
Set-Tick $appPid 'off'
Set-Tick $appPid 'on'
Invoke-Tap 'ReviewRead'
$done = Wait-LogMatch $Phone $package $appPid 'READ translated' $TranslateSeconds 0 'waiting for the translated read'
if (-not $done) { return (New-CheckResult $false "the tick was set on, but no 'READ translated' marker within $TranslateSeconds s of Read (no Italian model or no network?)") }
return (New-CheckResult $true 'set-translate off/on accepted on the Review page and the read translated the Italian text')
