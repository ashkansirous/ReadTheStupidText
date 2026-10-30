<#
.SYNOPSIS
  highlight: the reader highlights one sentence at a time while a chunk is still playing (Slice 190 marker, Slice 213).
.DESCRIPTION
  Drives the RTST reader the way a user does (the `read` hook bypasses ReaderPage, which owns the highlight): EditLink -> type three sentences into
  ReviewEditor -> ReviewRead, through tap.ps1 (AutomationIds from .maestro/rtst/sentence-highlight.yaml). ReaderPage.ShowSentence then writes `SENTENCE <chunk> <n>`
  (ReaderPage.xaml.cs) each time the highlight moves to a later sentence of the playing chunk; a `SENTENCE <chunk> <n>` with n >= 1 before READ_END passes.
  Needs the neural voice pack (the system voice keeps the whole-chunk highlight and writes no marker).
#>
param(
    [Parameter(Mandatory)][string]$Phone,
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [int]$MoveTimeoutSeconds = 60
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\common.ps1')
. (Join-Path $PSScriptRoot '_lib.ps1')
. (Join-Path $PSScriptRoot '_device.ps1')

$package = Get-DevPackage $App
$script:TypedText = 'The first sentence is long enough to stand alone. The second sentence follows right after it. The third sentence closes this short paragraph.'
$script:TapScript = Join-Path $PSScriptRoot '..\tap.ps1'

$script:EditorId = 'ReviewEditor'
$script:DraftClearKeys = 400                  # DEL and FORWARD_DEL presses each: empties a draft of up to 400 characters on either side of the cursor
$script:KeyCodeDelete = '67'
$script:KeyCodeForwardDelete = '112'
$script:SpaceEscape = '%s'                    # `input text` takes %s for a space

function Invoke-Tap([string]$ElementId) {
    Assert-WithinBudget "tapping $ElementId"
    $engine = (Get-Process -Id $PID).Path
    & $engine -NoProfile -File $script:TapScript -Id $ElementId -App $App -Serial $Phone | ForEach-Object { Write-Host "   tap: $_" }
    if ($LASTEXITCODE -ne 0) { throw "tap.ps1 -Id $ElementId exited $LASTEXITCODE (the element was not found or another app is on screen)." }
}

# `input text` appends, so a draft already in the editor is deleted first (the cursor sits where the tap put it: delete both sides).
function Clear-ReviewDraft {
    foreach ($code in $script:KeyCodeDelete, $script:KeyCodeForwardDelete) {
        Invoke-Adb $Phone (@('shell', 'input', 'keyevent') + @($code) * $script:DraftClearKeys) | Out-Null
    }
}

# The editor must hold exactly the typed text, or the highlight would be judged on someone else's sentences.
function Assert-TypedTextShown {
    $engine = (Get-Process -Id $PID).Path
    $listed = @(& $engine -NoProfile -File $script:TapScript -List -App $App -Serial $Phone | ForEach-Object { "$_" })
    if (-not (Test-EditorHoldsText $listed $script:EditorId $script:TypedText)) { throw "The review editor does not hold exactly the typed text (an old draft was not cleared, or the text did not go in)." }
}

function Enter-ReviewText {
    Invoke-Tap 'EditLink'
    Invoke-Tap $script:EditorId
    Clear-ReviewDraft
    Invoke-Adb $Phone @('shell', "input text '$($script:TypedText -replace ' ', $script:SpaceEscape)'") | Out-Null
    Invoke-Adb $Phone @('shell', 'input', 'keyevent', 'KEYCODE_BACK') | Out-Null   # hides the keyboard, as Maestro's hideKeyboard
    Assert-TypedTextShown
}

$appPid = Start-ReadyApp $Phone $App $package
Enter-ReviewText
Invoke-Tap 'ReviewRead'
$moved = Wait-LogMatch $Phone $package $appPid 'SENTENCE \d+ [1-9]\d*\s*$' $MoveTimeoutSeconds 0 'waiting for the highlight to move to sentence 2'
$events = @(ConvertFrom-SentenceLog (Get-PidLog $Phone $appPid))
Send-DevHook $Phone $App 'stop-read' | Out-Null
if ($moved -and (Test-HighlightMoved $events)) { return (New-CheckResult $true "highlight moved inside a playing chunk ($(($moved.Line -split 'RTST-TEST ')[-1].Trim()))") }
return (New-CheckResult $false "no SENTENCE <chunk> <n>=1+ marker within $MoveTimeoutSeconds s of tapping Read (needs the neural pack and a Debug build)")
