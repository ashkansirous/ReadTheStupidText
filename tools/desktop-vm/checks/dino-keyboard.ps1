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
    $back = Find-UiaById (Get-DinoWindow) 'ReadingBack'
    if ($back) { Invoke-Uia $back }
    Invoke-Uia (Wait-VisibleId 'GalleryGrownUps' 15 'FAIL the "For grown-ups" button is not on the kids'' shelf')
    $ring = $null
    try { $ring = Wait-Until { Find-UiaExact (Get-DinoWindow) $HoldRingName } 5 'gate not up' } catch { $ring = $null }
    if (-not $ring) {
        Invoke-Uia (Wait-VisibleId 'GalleryGrownUps' 5 'FAIL the "For grown-ups" button vanished before the retry')
        Wait-Until { Find-UiaExact (Get-DinoWindow) $HoldRingName } 15 'FAIL the gate did not open from the "For grown-ups" button' | Out-Null
    }
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
    if (Find-UiaVisibleById (Get-DinoWindow) 'GalleryDone') { throw 'FAIL a wrong typed answer opened grown-up mode' }
    $answer = [int](Wait-Until { Find-GateSum } 5 'FAIL no sum after a wrong answer')

    Type-Number $answer
    Press-Key $VkEnter
    Wait-VisibleId 'GalleryDone' 15 'FAIL typing the sum then Enter did not open grown-up mode' | Out-Null
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
    # "Read now" leaves the grown-ups corner by design (GalleryPage.LeaveAndReadAsync), so Esc, like Back, returns to the kids' shelf; the gate is passed again for the rest of the check.
    Wait-VisibleId 'GalleryGrownUps' 10 'FAIL Esc on the reading page did not come back to the kids'' shelf' | Out-Null
    Test-GateByKeyboard $Package
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
    # Esc on a grown-ups page (Settings is not a key target) goes back one page, to the corner.
    Invoke-Uia (Wait-VisibleId 'GallerySettings' 5 'FAIL no gear in grown-up mode')
    Wait-VisibleId 'SettingsTitle' 15 'FAIL Settings did not open from the gear' | Out-Null
    Focus-DinoWindow
    Press-Key $VkEscape
    Wait-VisibleId 'GalleryDone' 10 'FAIL Esc on Settings did not go back to the grown-ups corner' | Out-Null
    Start-Sleep -Milliseconds 300
    if (Find-UiaVisibleById (Get-DinoWindow) 'SettingsTitle') { throw 'FAIL Settings is still showing after Esc' }
}

function Test-ReadingKeys {
    param($Package)
    Invoke-Uia (Wait-VisibleId 'GalleryDone' 5 'FAIL no Done button in the grown-ups corner')
    Wait-VisibleId 'GalleryGrownUps' 10 'FAIL Done did not return to the kids'' shelf' | Out-Null
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
    Wait-VisibleId 'GalleryGrownUps' 10 'FAIL Esc did not return to the kids'' shelf' | Out-Null
    return $title
}

Invoke-Check -ResultFile $ResultFile -Body {
    $pkg = Get-InstalledPackage $PackageName
    try {
        $inkSeen = Count-Trace $pkg 'title bar tone Ink'
        Test-GateByKeyboard $pkg
        Wait-ToneAfter $pkg 'Ink' $inkSeen 'FAIL the title bar never took the ink tone in the grown-ups corner'
        Test-SheetEsc $pkg
        Test-EscFromGrownUpPage
        $creamSeen = Count-Trace $pkg 'title bar tone Cream'
        $title = Test-ReadingKeys $pkg
        Wait-ToneAfter $pkg 'Cream' $creamSeen 'FAIL the title bar never went back to cream on the kids'' pages'
        "gate passed with Space held and the sum typed; Esc closed the sheet; Space, Right, Left and Esc worked on the reading page (window title '$title'; trace line after the press: gate Space $script:GateSpaceMs ms, reading page $($script:KeyTimings -join ', ')); title bar went ink in the corner and back to cream"
    } catch {
        $tail = (Get-TraceLines $pkg | Select-Object -Last 25) -join ' | '
        throw "$($_.Exception.Message) -- window-trace.log tail: $tail"
    }
}
