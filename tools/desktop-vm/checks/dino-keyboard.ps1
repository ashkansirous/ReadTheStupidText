# The keyboard of the Windows window (Slice 235 part B, mock Batch 46 frames 158 and 160): the grown-up gate passes with the Space bar held and the sum typed then Enter,
# Esc closes a book's options sheet, Space and the arrows work on the reading page (the window title names the book), Esc goes back to the shelf, and the title bar
# changes colour between the kids' shelf (cream) and the grown-ups' corner (ink). Keys are real key events (keybd_event) sent to the focused window.
# The Release VM build shows no Debug.WriteLine, so a key the pages used is proved by the window-trace.log lines "Dino keyboard: <key> used" and the title bar colour by
# "title bar tone <tone>" (DinoKeyboard.cs, DinoWindowFrame.cs). Runs after dino-settings (which ends on the kids' shelf) and before dino-window (which closes the app).
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"

$HoldRingName = 'Press and hold for 3 seconds'   # Gate_HoldDescription on the HoldRing
$TraceFileName = 'window-trace.log'
$VkSpace = 0x20; $VkEnter = 0x0D; $VkEscape = 0x1B; $VkLeft = 0x25; $VkRight = 0x27; $VkDigit0 = 0x30
$HoldMs = 3600
$ShortHoldMs = 1200
$RepeatMs = 250   # a held key repeats about every 33 ms on a real keyboard; keybd_event sends one press, so repeat it by hand

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

function Wait-KeyUsed {
    # Polls window-trace.log for a "Dino keyboard: <Key> used" line beyond the $Before count (so one written after the press), up to $TimeoutSec.
    # Returns the milliseconds since $Watch started, or -1 when no such line arrived. The app's UI thread can be busy for seconds after a read starts (VM run 4: Right came 2.6 s after Space).
    param($Package, [string] $Key, [int] $Before, $Watch, [int] $TimeoutSec = 10)
    while ($Watch.Elapsed.TotalSeconds -lt $TimeoutSec) {
        if ((Count-Trace $Package "Dino keyboard: $Key used") -gt $Before) { return [int]$Watch.ElapsedMilliseconds }
        Start-Sleep -Milliseconds 100
    }
    return -1
}

function Focus-DinoWindow {
    $w = Get-DinoWindow
    [NativeKeys]::SetForegroundWindow([IntPtr]$w.Current.NativeWindowHandle) | Out-Null
    try { $w.SetFocus() } catch { }
    Start-Sleep -Milliseconds 400
}

Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class DinoKeyScan {
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
  public const uint VkToScan = 0;
  public const uint ExtendedKey = 1;
  public const uint KeyUp = 2;
}
'@

function Send-KeyEvent {
    # A real keyboard sends the scan code; the arrow keys are extended keys, so they also carry KEYEVENTF_EXTENDEDKEY. Space, Enter, Esc and the digits go as before.
    param([byte] $Vk, [bool] $Up)
    $flags = if ($Up) { [DinoKeyScan]::KeyUp } else { 0 }
    if ($Vk -eq $VkLeft -or $Vk -eq $VkRight) {
        $scan = [byte][DinoKeyScan]::MapVirtualKey($Vk, [DinoKeyScan]::VkToScan)
        [NativeKeys]::keybd_event($Vk, $scan, ($flags -bor [DinoKeyScan]::ExtendedKey), [UIntPtr]::Zero)
    } else {
        [NativeKeys]::keybd_event($Vk, 0, $flags, [UIntPtr]::Zero)
    }
}

function Press-Key {
    param([byte] $Vk, [int] $DownMs = 120)
    Send-KeyEvent $Vk $false
    Start-Sleep -Milliseconds $DownMs
    Send-KeyEvent $Vk $true
    Start-Sleep -Milliseconds 300
}

function Hold-Key {
    # Key down, then a repeat of the key down every $RepeatMs (what Windows does for a held key), then key up.
    param([byte] $Vk, [int] $HoldForMs)
    $until = (Get-Date).AddMilliseconds($HoldForMs)
    try {
        while ((Get-Date) -lt $until) {
            [NativeKeys]::keybd_event($Vk, 0, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds $RepeatMs
        }
    } finally { [NativeKeys]::keybd_event($Vk, 0, [NativeKeys]::KeyUp, [UIntPtr]::Zero) }
    Start-Sleep -Milliseconds 300
}

function Type-Number {
    param([int] $Number)
    foreach ($ch in ([string]$Number).ToCharArray()) { Press-Key ([byte]($VkDigit0 + [int][string]$ch)) }
}

function Find-GateSum {
    foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
        if ($e.Current.Name -match 'What is (\d+) plus (\d+)') { return [int]$Matches[1] + [int]$Matches[2] }
    }
    return $null
}

function Wait-VisibleId {
    param([string] $Id, [int] $TimeoutSec, [string] $Failure)
    return Wait-Until { Find-UiaVisibleById (Get-DinoWindow) $Id } $TimeoutSec $Failure
}

function Open-GateFromShelf {
    Enter-KidsShelf   # a tap on the lock locks an unlocked shelf (Slice 418: the gate opens from the lock)
    $back = Find-UiaById (Get-DinoWindow) 'ReadingBack'
    if ($back) { Invoke-Uia $back }
    Invoke-Uia (Wait-VisibleId 'GalleryLock' 15 'FAIL the lock is not on the shelf')
    $ring = $null
    try { $ring = Wait-Until { Find-UiaExact (Get-DinoWindow) $HoldRingName } 5 'gate not up' } catch { $ring = $null }
    if (-not $ring) {
        Invoke-Uia (Wait-VisibleId 'GalleryLock' 5 'FAIL the lock vanished before the retry')
        Wait-Until { Find-UiaExact (Get-DinoWindow) $HoldRingName } 15 'FAIL the gate did not open from the lock' | Out-Null
    }
}

function Ensure-Unlocked {
    # Slice 418: reading does not lock the shelf, but the 30 seconds may have run out while a check was slow; the gate is passed again only then.
    param($Package)
    if (Find-UiaVisibleById (Get-DinoWindow) 'LockChip') { return }
    Open-GateFromShelf
    Test-GateByKeyboard $Package
}

function Test-GateByKeyboard {
    param($Package)
    Open-GateFromShelf
    Focus-DinoWindow

    # Letting go early empties the ring: the sum must not appear.
    $startedBefore = Count-Trace $Package 'Dino keyboard: Space used'
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    Hold-Key $VkSpace $ShortHoldMs
    Start-Sleep -Seconds 1
    if (Find-GateSum) { throw "FAIL the sum appeared after holding the Space bar for only $ShortHoldMs ms (letting go must empty the ring)" }
    $ms = Wait-KeyUsed $Package 'Space' $startedBefore $watch
    if ($ms -lt 0) { throw 'FAIL the Space bar never reached the gate (no "Dino keyboard: Space used" in window-trace.log within 10 s): the key hook is not attached or the window has no focus' }
    $script:GateSpaceMs = $ms

    # Holding it for the full 3 s (with key repeats, which must not restart the hold) opens the sum.
    Hold-Key $VkSpace $HoldMs
    $answer = [int](Wait-Until { Find-GateSum } 10 'FAIL the sum did not appear after holding the Space bar for 3.6 s')

    # A wrong answer typed then Enter clears the field and shows a new sum; the gate stays.
    Type-Number ($answer + 1)
    Press-Key $VkEnter
    Start-Sleep -Milliseconds 800
    if (Find-UiaVisibleById (Get-DinoWindow) 'LockChip') { throw 'FAIL a wrong typed answer unlocked the shelf' }
    $answer = [int](Wait-Until { Find-GateSum } 5 'FAIL no sum after a wrong answer')

    Type-Number $answer
    Press-Key $VkEnter
    Wait-VisibleId 'LockChip' 15 'FAIL typing the sum then Enter did not unlock the shelf' | Out-Null
}

function Wait-ToneAfter {
    # An earlier check (dino-settings) already put the title bar through both tones, so only a line beyond $Seen proves this check's change.
    param($Package, [string] $Tone, [int] $Seen, [string] $Failure)
    Wait-Until { (Count-Trace $Package "title bar tone $Tone") -gt $Seen } 10 $Failure | Out-Null
}

function Test-SheetEsc {
    param($Package)
    $tile = Wait-Until {
        foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
            if ($e.Current.AutomationId -eq 'StoryTile' -and -not $e.Current.BoundingRectangle.IsEmpty) { return $e }
        }
    } 15 'FAIL no visible story tile in the grown-ups corner'
    Invoke-Uia $tile
    Wait-VisibleId 'BookSheetClose' 10 'FAIL the book options sheet did not open from a tile in the grown-ups corner' | Out-Null
    Focus-DinoWindow
    # "Read now" has the focus when the sheet opens: Enter reads the book (a focused control must be usable), Esc comes back to the corner.
    Press-Key $VkEnter
    Wait-Until { Find-UiaById (Get-DinoWindow) 'ReadingTransport' } 20 'FAIL Enter on the focused "Read now" did not open the book' | Out-Null
    Focus-DinoWindow
    Press-Key $VkEscape
    # Slice 418 decision 6: "Read now" opens the book without locking (GalleryPage.LeaveAndReadAsync), so Esc returns to the same shelf; the gate is passed again only when the 30 seconds ran out meanwhile.
    Wait-VisibleId 'GalleryLock' 10 'FAIL Esc on the reading page did not come back to the shelf' | Out-Null
    Ensure-Unlocked $Package
    $tile = Wait-Until {
        foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
            if ($e.Current.AutomationId -eq 'StoryTile' -and -not $e.Current.BoundingRectangle.IsEmpty) { return $e }
        }
    } 15 'FAIL no visible story tile after coming back'
    Invoke-Uia $tile
    Wait-VisibleId 'BookSheetClose' 10 'FAIL the book options sheet did not open the second time' | Out-Null
    Focus-DinoWindow
    Press-Key $VkEscape
    Wait-Until { -not (Find-UiaVisibleById (Get-DinoWindow) 'BookSheetClose') } 5 'FAIL Esc did not close the book options sheet' | Out-Null
}

function Test-EscFromGrownUpPage {
    # Slice 418: Esc is Android's Back. On a Settings sub-page it goes up to the Settings list first, then from Settings to Books; the shelf stays unlocked all the way (an Esc is a grown-up key).
    param($Package)
    Ensure-Unlocked $Package
    Invoke-Uia (Wait-VisibleId 'GallerySettings' 5 'FAIL no Settings tab on the unlocked shelf')
    Wait-VisibleId 'SettingsTitle' 15 'FAIL the Settings list did not show' | Out-Null
    $row = Wait-Until { Find-UiaByName -Root (Get-DinoWindow) -Name 'Reading speed: ' -ControlType 'Button' } 5 'FAIL no Reading speed row in Settings'
    Invoke-Uia $row
    Wait-VisibleId 'SettingsTitleReadingSpeedPage' 10 'FAIL the Reading speed row did not open its page inside the tab' | Out-Null
    Focus-DinoWindow
    Press-Key $VkEscape
    Wait-VisibleId 'SettingsTitle' 10 'FAIL Esc on a Settings sub-page did not go back to the Settings list' | Out-Null
    Start-Sleep -Milliseconds 300
    if (Find-UiaVisibleById (Get-DinoWindow) 'SettingsTitleReadingSpeedPage') { throw 'FAIL the Reading speed page is still showing after Esc' }
    if (-not (Find-UiaVisibleById (Get-DinoWindow) 'LockChip')) { throw 'FAIL Esc on a Settings sub-page locked the shelf' }
    Press-Key $VkEscape
    Wait-VisibleId 'StoryTile' 10 'FAIL Esc on the Settings list did not go to the Books tab' | Out-Null
    if (-not (Find-UiaVisibleById (Get-DinoWindow) 'LockChip')) { throw 'FAIL Esc on the Settings list locked the shelf' }
}

function Press-Ctrl {
    # Ctrl held while a key goes down and up (Slice 325 part A: Ctrl+1/2/3 pick the corner's tabs).
    param([byte] $Vk)
    $VkControl = 0x11
    [NativeKeys]::keybd_event($VkControl, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    Press-Key $Vk
    Start-Sleep -Milliseconds 80
    [NativeKeys]::keybd_event($VkControl, 0, [DinoKeyScan]::KeyUp, [UIntPtr]::Zero)
}

function Test-CornerKeys {
    # Slice 418 (frames WL1, WL2): on the unlocked shelf Ctrl+2, Ctrl+3 and Ctrl+1 pick Store, Settings and Books, which show inside the rail's content area (the lock stays in the rail),
    # and Esc on Books asks to close the app (there is no corner to go back from). Proved by the trace line DinoKeyboard writes for a Ctrl tab key and by what each tab shows.
    param($Package)
    Ensure-Unlocked $Package
    Wait-VisibleId 'CornerTabBooks' 5 'FAIL no Books tab on the shelf' | Out-Null
    foreach ($tab in @(@('CornerStore', 0x32, 'GalleryStoreSub'), @('CornerSettings', 0x33, 'SettingsTitle'), @('CornerBooks', 0x31, 'StoryTile'))) {
        Focus-DinoWindow
        $before = Count-Trace $Package ("corner tab " + $tab[0])
        Press-Ctrl ([byte]$tab[1])
        Wait-Until { (Count-Trace $Package ("corner tab " + $tab[0])) -gt $before } 10 "FAIL Ctrl+digit did not pick $($tab[0])" | Out-Null
        Wait-VisibleId $tab[2] 10 "FAIL $($tab[0]) did not show its content ($($tab[2]))" | Out-Null
        Wait-VisibleId 'GalleryLock' 5 "FAIL the lock left the rail on $($tab[0])" | Out-Null
        if (-not (Find-UiaVisibleById (Get-DinoWindow) 'LockChip')) { throw "FAIL the shelf locked while Ctrl+digit picked $($tab[0]) (a key is a grown-up tap)" }
    }
    Focus-DinoWindow
    Press-Key $VkEscape
    Wait-Until { Find-UiaByName (Get-DinoWindow) 'Close Dino: Kids Books & Stories?' } 10 'FAIL Esc on the Books tab did not ask to close the app' | Out-Null
    Press-Key $VkEscape
    Wait-Until { -not (Find-UiaByName (Get-DinoWindow) 'Close Dino: Kids Books & Stories?') } 10 'FAIL Esc on the close question did not keep the shelf' | Out-Null
}

function Test-EscOnShelf {
    # Slice 325 part C4 (Slice 418: the one shelf, locked or not): Esc on the shelf asks "Close Dino: Kids Books & Stories?"; Esc on the question is Stay and keeps the shelf.
    Focus-DinoWindow
    Press-Key $VkEscape
    Wait-Until { Find-UiaByName (Get-DinoWindow) 'Close Dino: Kids Books & Stories?' } 10 'FAIL Esc on the kids'' shelf did not ask to close the app' | Out-Null
    Press-Key $VkEscape
    Wait-Until { -not (Find-UiaByName (Get-DinoWindow) 'Close Dino: Kids Books & Stories?') } 10 'FAIL Esc on the close question did not keep the shelf' | Out-Null
    Wait-VisibleId 'GalleryLock' 5 'FAIL the shelf is gone after Stay' | Out-Null
}

function Test-CtrlL {
    # Slice 418 (mock WL1, WL2): Ctrl+L is the lock. On the locked shelf it opens the gate (GateCard); Esc closes it onto the locked shelf; after the gate is passed it locks at once (the LockChip goes).
    param($Package)
    $lockTrace = 'Dino keyboard: Ctrl\+L used'
    Enter-KidsShelf
    Focus-DinoWindow
    $before = Count-Trace $Package $lockTrace
    Press-Ctrl 0x4C
    Wait-Until { (Count-Trace $Package $lockTrace) -gt $before } 10 'FAIL Ctrl+L on the locked shelf wrote no "Ctrl+L used" trace line' | Out-Null
    Wait-Until { Find-UiaExact (Get-DinoWindow) $HoldRingName } 10 'FAIL Ctrl+L on the locked shelf did not open the gate (no hold ring; found at every window width, unlike GateCard)' | Out-Null
    Focus-DinoWindow
    Press-Key $VkEscape
    Wait-Until { -not (Find-UiaExact (Get-DinoWindow) $HoldRingName) } 10 'FAIL Esc did not close the gate Ctrl+L opened' | Out-Null
    Wait-Until { Test-LockedShelfShown (Get-DinoWindow) } 10 'FAIL the shelf is not locked after Esc closed the gate Ctrl+L opened' | Out-Null
    Test-GateByKeyboard $Package
    Focus-DinoWindow
    $before = Count-Trace $Package $lockTrace
    Press-Ctrl 0x4C
    Wait-Until { (Count-Trace $Package $lockTrace) -gt $before } 10 'FAIL Ctrl+L on the unlocked shelf wrote no "Ctrl+L used" trace line (the unlock may have expired first)' | Out-Null
    Wait-Until { Test-LockedShelfShown (Get-DinoWindow) } 10 'FAIL Ctrl+L on the unlocked shelf did not lock it (the LockChip is still showing)' | Out-Null
    Wait-VisibleId 'GalleryLock' 5 'FAIL the locked shelf is not showing for the reading keys' | Out-Null   # Test-ReadingKeys taps a tile on this locked shelf, the state kids read in
}

function Test-ReadingKeys {
    param($Package)
    Wait-VisibleId 'GalleryLock' 10 'FAIL the shelf is not showing before the reading keys' | Out-Null
    $tile = Wait-Until {
        foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
            if ($e.Current.AutomationId -eq 'StoryTile' -and -not $e.Current.BoundingRectangle.IsEmpty) { return $e }
        }
    } 15 'FAIL no visible story tile on the shelf'
    Invoke-Uia $tile
    Wait-Until { Find-UiaById (Get-DinoWindow) 'ReadingTransport' } 20 'FAIL the reading page did not open' | Out-Null
    Focus-DinoWindow

    $title = (Get-DinoWindow).Current.Name
    if ($title -notmatch ' - ') { throw "FAIL the window title on the reading page is '$title', not '<book> - <app name>'" }

    $script:KeyTimings = @()
    foreach ($key in @(@('Space', $VkSpace), @('Right', $VkRight), @('Left', $VkLeft))) {
        $before = Count-Trace $Package "Dino keyboard: $($key[0]) used"
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        Press-Key ([byte]$key[1])
        $ms = Wait-KeyUsed $Package $key[0] $before $watch
        if ($ms -lt 0) { throw "FAIL the $($key[0]) key was not used on the reading page within 10 s" }
        $script:KeyTimings += "$($key[0]) $ms ms"
    }
    Press-Key $VkSpace   # pause or stop what the first Space started
    Press-Key $VkEscape
    Wait-Until { -not (Find-UiaById (Get-DinoWindow) 'ReadingTransport') } 10 'FAIL Esc did not leave the reading page' | Out-Null
    Wait-VisibleId 'GalleryLock' 10 'FAIL Esc did not return to the shelf' | Out-Null
    return $title
}

Invoke-Check -ResultFile $ResultFile -Body {
    $pkg = Get-InstalledPackage $PackageName
    try {
        $inkSeen = Count-Trace $pkg 'title bar tone Ink'
        Test-GateByKeyboard $pkg
        Wait-ToneAfter $pkg 'Ink' $inkSeen 'FAIL the title bar never took the ink tone in the grown-ups corner'
        Test-SheetEsc $pkg
        Test-EscFromGrownUpPage $pkg
        Test-CornerKeys $pkg
        Test-EscOnShelf
        Test-CtrlL $pkg
        $creamSeen = Count-Trace $pkg 'title bar tone Cream'
        $title = Test-ReadingKeys $pkg
        Wait-ToneAfter $pkg 'Cream' $creamSeen 'FAIL the title bar never went back to cream on the kids'' pages'
        "gate passed with Space held and the sum typed; Esc closed the sheet; Space, Right, Left and Esc worked on the reading page (window title '$title'; trace line after the press: gate Space $script:GateSpaceMs ms, reading page $($script:KeyTimings -join ', ')); title bar went ink in the corner and back to cream"
    } catch {
        $tail = (Get-TraceLines $pkg | Select-Object -Last 25) -join ' | '
        throw "$($_.Exception.Message) -- window-trace.log tail: $tail"
    }
}
