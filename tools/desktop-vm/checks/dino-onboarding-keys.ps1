# order: first in the Dino list (Invoke-DesktopTest.ps1 $Apps), so it has no `# after:` header; dino-launch declares `# after: dino-onboarding-keys`. On a guest whose first run is already done it returns at once ("first run already done").
# The Windows keys of the first run (Slice 325 parts D3 and E2, mock frames OW2, OW5 and OW6). It runs FIRST in the Dino list (before dino-launch) because it needs the fresh app: dino-launch finds the shelf
# it leaves behind and only re-checks it. Steps: language page (English, by mouse) -> book picker (OW2): Space ticks the focused cover, Enter before two are picked does nothing, Enter after two continues ->
# "Ready to read" (OW5) -> "See what Dino can do" -> intro (OW6): Enter = Next, Left steps back, Right steps forward and does nothing on the last step, Esc = Skip all and lands on the kids' shelf.
# Keys are real key events (keybd_event) sent to the focused window; the page that used each one is proved by the "Dino keyboard: <key> used" line in window-trace.log (DinoKeyboard.cs) and by what the page shows.
# Screenshots go to <results>\onboarding-picker.png (OW2), onboarding-ready.png (OW5) and onboarding-intro.png (OW6). The intro uses Enter only on the first step: Enter on the next two steps is "Try it" (audio, microphone).
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"

$TraceFileName = 'window-trace.log'
$VkSpace = 0x20; $VkEnter = 0x0D; $VkEscape = 0x1B; $VkLeft = 0x25; $VkRight = 0x27
$IntroSteps = 4
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

function Test-PickerKeys {
    Wait-Until { Find-UiaById (Get-DinoWindow) 'FirstBooksPicker' } 30 'FAIL the first-books picker did not show' | Out-Null
    Start-Sleep -Seconds 2   # fixed-wait: the covers load in after the page shows; the screenshot should not catch grey boxes
    Save-Shot 'onboarding-picker.png'
    Tick-WithSpace 'FirstBookTick-0'
    # One book picked: Enter must do nothing yet.
    Press-Key $VkEnter
    Start-Sleep -Milliseconds $NoEffectWaitMs
    Assert-CountLine 'Enter with one book picked (it must not untick the focused cover)' 'Pick 1 more'
    if (Find-UiaVisibleById (Get-DinoWindow) 'FirstRunReady') { throw 'FAIL Enter continued with only one book picked' }
    if (-not (Find-UiaById (Get-DinoWindow) 'FirstBooksPicker')) { throw 'FAIL the picker went away on Enter with only one book picked' }
    Tick-WithSpace 'FirstBookTick-1'
    Wait-Until { (Get-PickedCount) -match 'picked' } 10 "FAIL the count line did not read the picked state (reads '$(Get-PickedCount)')" | Out-Null
    Focus-DinoWindow
    Press-Key $VkEnter
    Wait-Until { Find-UiaVisibleById (Get-DinoWindow) 'FirstRunReady' } 60 'FAIL Enter with two books picked did not open the Ready page' | Out-Null
}

function Get-IntroStep {
    for ($n = 1; $n -le $IntroSteps; $n++) {
        if (Find-UiaExact (Get-DinoWindow) "$n of $IntroSteps") { return $n }
    }
    return 0
}

function Wait-IntroStep {
    param([int] $Step, [string] $Failure)
    Wait-Until { (Get-IntroStep) -eq $Step } 15 "FAIL $Failure (the intro shows step $(Get-IntroStep) of $IntroSteps)" | Out-Null
}

function Test-IntroKeys {
    param($Package)
    Wait-IntroStep 1 'the intro did not open on step 1'
    Start-Sleep -Seconds 1   # fixed-wait: the step picture draws in after the counter shows
    Save-Shot 'onboarding-intro.png'
    Focus-DinoWindow
    Press-Key $VkEnter
    Wait-IntroStep 2 'Enter (Next) did not move to step 2'
    Press-Key $VkLeft
    Wait-IntroStep 1 'Left did not step back to step 1'
    Press-Key $VkRight
    Wait-IntroStep 2 'Right did not step forward to step 2'
    Press-Key $VkRight
    Wait-IntroStep 3 'Right did not step forward to step 3'
    Press-Key $VkRight
    Wait-IntroStep $IntroSteps "Right did not step forward to step $IntroSteps"
    $before = Count-Trace $Package $RightNotUsed
    Press-Key $VkRight
    Start-Sleep -Milliseconds $NoEffectWaitMs
    if ((Get-IntroStep) -ne $IntroSteps) { throw "FAIL Right on the last step moved the intro to step $(Get-IntroStep)" }
    if ((Count-Trace $Package $RightNotUsed) -le $before) { throw 'FAIL Right on the last step was not reported as not used in window-trace.log' }
    Focus-DinoWindow
    Press-Key $VkEscape
    Wait-Until { Find-UiaById (Get-DinoWindow) 'GalleryTitle' } 30 'FAIL Esc (Skip all) did not land on the kids'' shelf' | Out-Null
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
        Test-PickerKeys
        Start-Sleep -Seconds 1   # fixed-wait: the Ready page fades in
        Save-Shot 'onboarding-ready.png'
        Invoke-Uia (Wait-Until { Find-UiaByName -Root (& $window) -Name 'See what Dino can do' -ControlType Pane } 30 'FAIL the Ready page has no "See what Dino can do" button')
        Test-IntroKeys $pkg
        'picker: Space ticked two covers, Enter waited for two then continued; intro: Enter next, Left back, Right forward (nothing on step 4), Esc = Skip all to the shelf; screenshots onboarding-picker/ready/intro.png; ' + ($script:Notes -join '; ')
    } catch {
        $tail = (Get-TraceLines $pkg | Select-Object -Last 25) -join ' | '
        throw "$($_.Exception.Message) -- window-trace.log tail: $tail"
    }
}
