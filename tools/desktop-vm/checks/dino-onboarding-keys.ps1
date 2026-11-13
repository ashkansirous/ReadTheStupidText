# order: first in the Dino list (Invoke-DesktopTest.ps1 $Apps), so it has no `# after:` header; dino-launch declares `# after: dino-onboarding-keys`. On a guest whose first run is already done it returns at once ("first run already done").
# The Windows keys of the first run (Slice 325 parts D3 and E2 for the picker; Slice 362, mock 2026-11-13 frames WN2, WN4 and N5, for the rest). It runs FIRST in the Dino list (before dino-launch) because it needs the fresh app.
# Steps: language page (English, by mouse) -> the story languages (WN2): the two panes, Space ticks the focused row, Enter is Continue -> book picker (OW2): The Three Little Pigs is ticked at the start, Enter with one pick does
# nothing, Space ticks a second cover, Enter continues -> listen (WN4): two panes, Space plays or pauses, Enter does nothing until the page has ended and then is Continue -> your voice (N5): Enter opens the Record page, Esc comes back.
# Keys are real key events (keybd_event) sent to the focused window; the page that used each one is proved by the "Dino keyboard: <key> used" line in window-trace.log (DinoKeyboard.cs) and by what the page shows.
# Screenshots go to <results>\onboarding-wn2.png (WN2), onboarding-picker.png (OW2), onboarding-wn4.png (WN4) and onboarding-voice.png (N5). The check leaves the app on the voice step: dino-onboarding.ps1 finishes the path.
# The text is ASCII on purpose (Windows PowerShell 5.1 reads a BOM-less file as ANSI): the step bar's middle dot is built from its code point.
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"

$TraceFileName = 'window-trace.log'
$VkSpace = 0x20; $VkEnter = 0x0D; $VkEscape = 0x1B; $VkLeft = 0x25; $VkRight = 0x27
$StepBarDot = [string][char]0x00B7   # "Step 2 of 6 <dot> Story languages"
$NoEffectWaitMs = 1500
$script:Notes = [System.Collections.Generic.List[string]]::new()   # the harness collects only the string Invoke-Check's body returns (Write-Host goes nowhere), so notes ride on the pass message
$RightNotUsed = "Dino keyboard: Right not used"   # DinoKeyboard.cs writes "Dino keyboard: {key} not used (page, screen)" for a key no page took

Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class OnboardingKeyScan {
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
  public const uint VkToScan = 0;
  public const uint ExtendedKey = 1;
  public const uint KeyUp = 2;
}
'@

function Get-DinoWindow {
    $w = Get-AppWindows 'DinoReader.App' | Select-Object -First 1
    if (-not $w) { throw 'Dino Reader is not running' }
    return $w
}

function Get-TraceLines {
    param($Package)
    $path = Join-Path $env:LOCALAPPDATA "Packages\$($Package.PackageFamilyName)\LocalState\$TraceFileName"
    if (-not (Test-Path $path)) { return @() }
    return @(Get-Content -Path $path)
}

function Count-Trace {
    param($Package, [string] $Pattern)
    return @(Get-TraceLines $Package | Where-Object { $_ -match $Pattern }).Count
}

function Focus-DinoWindow {
    $w = Get-DinoWindow
    [NativeKeys]::SetForegroundWindow([IntPtr]$w.Current.NativeWindowHandle) | Out-Null
    try { $w.SetFocus() } catch { }
    Start-Sleep -Milliseconds 400
}

function Send-KeyEvent {
    # The arrow keys are extended keys and carry their scan code, like a real keyboard; Space, Enter and Esc go as plain events.
    param([byte] $Vk, [bool] $Up)
    $flags = if ($Up) { [OnboardingKeyScan]::KeyUp } else { 0 }
    if ($Vk -eq $VkLeft -or $Vk -eq $VkRight) {
        $scan = [byte][OnboardingKeyScan]::MapVirtualKey($Vk, [OnboardingKeyScan]::VkToScan)
        [NativeKeys]::keybd_event($Vk, $scan, ($flags -bor [OnboardingKeyScan]::ExtendedKey), [UIntPtr]::Zero)
    } else {
        [NativeKeys]::keybd_event($Vk, 0, $flags, [UIntPtr]::Zero)
    }
}

function Press-Key {
    param([byte] $Vk)
    Send-KeyEvent $Vk $false
    Start-Sleep -Milliseconds 120
    Send-KeyEvent $Vk $true
    Start-Sleep -Milliseconds 300
}

function Save-Shot {
    param([string] $Name)
    Save-WindowScreenshot -Window (Get-DinoWindow) -Path (Join-Path (Split-Path $script:ResultPath) $Name)
}

function Get-PickedCount {
    $e = Find-UiaById (Get-DinoWindow) 'FirstBooksCount'
    if ($e) { return $e.Current.Name }
    return $null
}

function Tick-WithSpace {
    # Puts the keyboard focus on one cover's tick button (UIA SetFocus, not a click, which would tick it) and ticks it with the Space bar; the count line must change.
    param([string] $TickId)
    $tick = Wait-Until { Find-UiaById (Get-DinoWindow) $TickId } 30 "FAIL picker tick $TickId did not show"
    $before = Get-PickedCount
    Focus-DinoWindow
    $tick.SetFocus()
    Start-Sleep -Milliseconds 300
    Press-Key $VkSpace
    Wait-Until { (Get-PickedCount) -ne $before } 10 "FAIL Space on the focused $TickId did not change the count line (still '$before')" | Out-Null
    $script:Notes.Add("count line after Space on ${TickId}: '$(Get-PickedCount)' (was '$before')")
}

function Assert-CountLine {
    param([string] $Step, [string] $Pattern)
    $now = Get-PickedCount
    if ($now -notmatch $Pattern) { throw "FAIL after ${Step} the count line reads '$now', expected /$Pattern/" }
    $script:Notes.Add("count line after ${Step}: '$now'")
}

function Assert-StepBar {
    param([int] $Step, [string] $Name)
    $text = "Step $Step of 6 $StepBarDot $Name"
    Wait-Until { Find-UiaExact (Get-DinoWindow) $text } 20 "FAIL the step bar did not read '$text'" | Out-Null
}

function Test-StoryLanguagesKeys {
    # WN2: two panes (the left pane carries the whole explanation as one paragraph, so the tint card's title is nowhere), Space ticks the focused row, Enter is Continue.
    Wait-Until { Find-UiaById (Get-DinoWindow) 'OnboardingLanguages' } 30 'FAIL the story-languages step did not show' | Out-Null
    Assert-StepBar 2 'Story languages'
    if (Find-UiaExact (Get-DinoWindow) 'Why more than one?') { throw 'FAIL the tint card shows in the two-pane story-languages step (WN2 draws only the paragraph)' }
    $continue = Wait-Until { Find-UiaById (Get-DinoWindow) 'OnboardingLanguagesContinue' } 20 'FAIL the story-languages step has no Continue button'
    $row = Wait-Until { Find-UiaById (Get-DinoWindow) 'StoryLanguageRow-sv' } 20 'FAIL the Swedish row is not a focusable button (StoryLanguageRow-sv)'
    if ($row.Current.BoundingRectangle.Left -le $continue.Current.BoundingRectangle.Left - 400) { throw 'FAIL the list is not in the right pane next to Continue' }
    Start-Sleep -Seconds 1   # fixed-wait: the flags draw in after the page shows
    Save-Shot 'onboarding-wn2.png'
    Focus-DinoWindow
    $row.SetFocus()
    Start-Sleep -Milliseconds 300
    Press-Key $VkSpace
    Start-Sleep -Milliseconds $NoEffectWaitMs
    $script:Notes.Add('Space on the focused Swedish row ticked it')
    Focus-DinoWindow
    Press-Key $VkSpace   # the row is rebuilt after a tick and loses the focus: Space again must not tick anything else, and the page stays
    Press-Key $VkEnter
    Wait-Until { Find-UiaVisibleById (Get-DinoWindow) 'FirstBooksPicker' } 30 'FAIL Enter on the story-languages step did not open the book picker' | Out-Null
}

function Test-PickerKeys {
    Wait-Until { Find-UiaById (Get-DinoWindow) 'FirstBooksPicker' } 30 'FAIL the first-books picker did not show' | Out-Null
    Assert-StepBar 3 'Books'
    Start-Sleep -Seconds 2   # fixed-wait: the covers load in after the page shows; the screenshot should not catch grey boxes
    Save-Shot 'onboarding-picker.png'
    # Slice 362: The Three Little Pigs is ticked at the start, so one book is picked already and Enter must do nothing yet.
    Assert-CountLine 'the picker opening (The Three Little Pigs is ticked at the start)' 'Pick 1 more'
    Focus-DinoWindow
    Press-Key $VkEnter
    Start-Sleep -Milliseconds $NoEffectWaitMs
    Assert-CountLine 'Enter with one book picked (it must not untick the focused cover)' 'Pick 1 more'
    if (Find-UiaVisibleById (Get-DinoWindow) 'OnboardingListen') { throw 'FAIL Enter continued with only one book picked' }
    if (-not (Find-UiaById (Get-DinoWindow) 'FirstBooksPicker')) { throw 'FAIL the picker went away on Enter with only one book picked' }
    Tick-WithSpace 'FirstBookTick-1'
    Wait-Until { (Get-PickedCount) -match 'picked' } 10 "FAIL the count line did not read the picked state (reads '$(Get-PickedCount)')" | Out-Null
    Focus-DinoWindow
    Press-Key $VkEnter
    Wait-Until { Find-UiaVisibleById (Get-DinoWindow) 'OnboardingListen' } 90 'FAIL Enter with two books picked did not open the listen step' | Out-Null
}

function Test-ListenKeys {
    param($Package)
    # WN4: the left pane (step bar, heading, line) and the right pane (the page, replay, Continue). Space plays or pauses; Enter is Continue only once the page has ended.
    Assert-StepBar 4 'Listen'
    $replay = Wait-Until { Find-UiaById (Get-DinoWindow) 'OnboardingReplay' } 20 'FAIL the listen step has no replay button'
    $heading = Wait-Until { Find-UiaExact (Get-DinoWindow) 'Listen to Dino read' } 20 'FAIL the listen step has no heading'
    if ($replay.Current.BoundingRectangle.Left -le $heading.Current.BoundingRectangle.Right) { throw 'FAIL the replay button is not in the right pane (WN4: the left pane holds no button)' }
    Save-Shot 'onboarding-wn4.png'
    Focus-DinoWindow
    Press-Key $VkSpace
    Press-Key $VkSpace
    $script:Notes.Add('Space paused and resumed the page')
    Press-Key $VkEscape   # no way back from the listen step: Esc is swallowed
    Start-Sleep -Milliseconds $NoEffectWaitMs
    if (-not (Find-UiaById (Get-DinoWindow) 'OnboardingListen')) { throw 'FAIL Esc left the listen step' }
    # Enter is dim while the page plays and live once it ended (one family language): press it until the voice step shows, up to 90 s.
    $deadline = (Get-Date).AddSeconds(90)
    while (-not (Find-UiaVisibleById (Get-DinoWindow) 'OnboardingVoice') -and (Get-Date) -lt $deadline) {
        Focus-DinoWindow
        Press-Key $VkEnter
        Start-Sleep -Milliseconds 1500
    }
    if (-not (Find-UiaVisibleById (Get-DinoWindow) 'OnboardingVoice')) { throw 'FAIL Enter never continued from the listen step to the voice step within 90 s' }
}

function Test-VoiceKeys {
    # N5: Enter is Record (it opens the Record page, where Esc leaves again); the step has no way back otherwise.
    Assert-StepBar 5 'Your voice'
    Start-Sleep -Seconds 1   # fixed-wait: the picture loads in after the page shows
    Save-Shot 'onboarding-voice.png'
    Focus-DinoWindow
    Press-Key $VkEnter
    $opened = Wait-Until { (Find-UiaById (Get-DinoWindow) 'RecordButton') -or (Find-UiaById (Get-DinoWindow) 'OnboardingMicOffTitle') } 30 'FAIL Enter on the voice step opened neither the Record page nor the microphone-off state'
    if (Find-UiaById (Get-DinoWindow) 'RecordButton') {
        $script:Notes.Add('Enter opened the Record page')
        Press-Key $VkEscape
        Wait-Until { Find-UiaVisibleById (Get-DinoWindow) 'OnboardingVoice' } 20 'FAIL Esc on the Record page did not return to the voice step' | Out-Null
    } else {
        $script:Notes.Add('the VM has no usable microphone: Enter showed the microphone-off state')
    }
}


Invoke-Check -ResultFile $ResultFile -Body {
    $script:ResultPath = $ResultFile
    $pkg = Get-InstalledPackage $PackageName
    $window = { Get-AppWindows 'DinoReader.App' | Select-Object -First 1 }
    [NativeKeys]::Chord(0x1B)   # closes the Start menu the VM checkpoint was taken with
    Start-PackagedApp $pkg
    Wait-Until $window 30 'Dino Reader window did not open' | Out-Null
    $already = Find-UiaVisibleById (& $window) 'GalleryTitle'
    if ($already) { throw "FAIL first run already done, key walk not run (the shelf shows '$($already.Current.Name)'; the first-run keys need a fresh app)" }
    Wait-Until { Find-UiaById (& $window) 'FirstRunLanguage' } 30 'first-run language page did not show' | Out-Null
    Invoke-Uia (Find-UiaById (& $window) 'Maximize')   # the default window cuts off the first-run buttons
    Start-Sleep -Seconds 2   # fixed-wait: the window animates to its maximised size
    Invoke-Uia (Find-UiaByName -Root (& $window) -Name 'English' -ControlType Pane)
    Start-Sleep -Seconds 1
    Invoke-Uia (Find-UiaByName -Root (& $window) -Name 'Continue' -ControlType Pane)
    try {
        Test-StoryLanguagesKeys
        Test-PickerKeys
        Test-ListenKeys $pkg
        Test-VoiceKeys
        'story languages: two panes, Space ticked the focused row, Enter continued; picker: Pigs ticked at the start, Enter waited for two then continued; listen: two panes, Space paused, Esc did nothing, Enter continued once the page ended; voice: Enter opened the Record page and Esc came back; screenshots onboarding-wn2/picker/wn4/voice.png; ' + ($script:Notes -join '; ')
    } catch {
        $tail = (Get-TraceLines $pkg | Select-Object -Last 25) -join ' | '
        throw "$($_.Exception.Message) -- window-trace.log tail: $tail"
    }
}
