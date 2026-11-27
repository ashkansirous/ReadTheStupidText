# The Windows window's layout and mouse and keyboard polish (Slice 235 part C, mock Batch 46 frames 151-159): the shelf's columns follow the window width
# (a phone-landscape window gets a grid, not the sideways row), a book is a Tab stop that Enter opens, a hovered book lifts, a right-click in the grown-ups' corner opens
# the book's sheet and Esc closes it with the keyboard back on the book, and the arrow keys move through Settings. The Release VM build shows no Debug.WriteLine, so each step is
# proved by a window-trace.log line (GalleryPage.xaml.cs ApplyShape, GalleryPage.Desktop.cs, DinoFocusRing.cs). Runs after dino-keyboard (which ends on the kids' shelf) and before
# dino-window (which closes the app). The mouse wheel is native WinUI scrolling and is not scripted (no trace line to prove it).
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"

$HoldRingName = 'Press and hold for 3 seconds'
$HoldMs = 3500
$TraceFileName = 'window-trace.log'
$VkTab = 0x09; $VkEnter = 0x0D; $VkEscape = 0x1B; $VkDown = 0x28
$MinWidth = 800
$MinHeight = 560
$OpenWidth = 1100
$OpenHeight = 760
# Slice 325 part C (ShelfTitleRules.Columns on Windows): the kids' shelf sits right of a 270 dp rail with 32 + 40 dp sides and 18 dp gaps; 190 dp covers, 4 to 6 columns.
$ColumnWidth = 190
$ColumnGap = 18
$RailAndSides = 270 + 32 + 40
$MaxColumns = 6
$MinColumns = 4
$MaxTabs = 25

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

function Wait-TraceAfter {
    # Waits for a trace line matching $Pattern beyond the first $Before matches and returns the newest one.
    param($Package, [string] $Pattern, [int] $Before, [int] $TimeoutSec, [string] $Failure)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        $lines = @(Get-TraceLines $Package | Where-Object { $_ -match $Pattern })
        if ($lines.Count -gt $Before) { return $lines[-1] }
        Start-Sleep -Milliseconds 200
    }
    throw $Failure
}

function Focus-DinoWindow {
    $w = Get-DinoWindow
    [NativeKeys]::SetForegroundWindow([IntPtr]$w.Current.NativeWindowHandle) | Out-Null
    try { $w.SetFocus() } catch { }
    Start-Sleep -Milliseconds 400
}

function Press-Key {
    param([byte] $Vk, [int] $DownMs = 120)
    [NativeKeys]::keybd_event($Vk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $DownMs
    [NativeKeys]::keybd_event($Vk, 0, [NativeKeys]::KeyUp, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
}

function Press-ShiftF10 {
    # Shift held while F10 goes down and up: the Windows context-menu key chord (VK_SHIFT 0x10, VK_F10 0x79).
    [NativeKeys]::keybd_event(0x10, 0, 0, [UIntPtr]::Zero)
    try {
        Start-Sleep -Milliseconds 80
        Press-Key 0x79
    } finally { [NativeKeys]::keybd_event(0x10, 0, [NativeKeys]::KeyUp, [UIntPtr]::Zero) }   # Shift is always released, or every later key in the VM is shifted
    Start-Sleep -Milliseconds 300
}

function Wait-VisibleId {
    param([string] $Id, [int] $TimeoutSec, [string] $Failure)
    return Wait-Until { Find-UiaVisibleById (Get-DinoWindow) $Id } $TimeoutSec $Failure
}

function Wait-VisibleName {
    param([string] $Name, [int] $TimeoutSec, [string] $Failure)
    return Wait-Until { Find-UiaExact (Get-DinoWindow) $Name } $TimeoutSec $Failure
}

function Get-WorkArea {
    $a = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    return @{ W = [int]$a.Width; H = [int]$a.Height }
}

function Get-ShelfLine {
    # The newest "Dino shelf: columns=N sideways=B ... content=WxH" line.
    param($Package)
    $line = @(Get-TraceLines $Package | Where-Object { $_ -match 'Dino shelf: columns=' }) | Select-Object -Last 1
    if (-not $line) { throw 'FAIL the shelf never traced its shape (no "Dino shelf: columns=" line)' }
    if ($line -notmatch 'columns=(\d+) sideways=(\w+) rail=(\w+) grownUp=(\w+) content=(\d+)x(\d+)') { throw "FAIL unreadable shelf line: $line" }
    return @{ Columns = [int]$Matches[1]; Sideways = $Matches[2]; GrownUp = $Matches[4]; Width = [int]$Matches[5]; Height = [int]$Matches[6] }
}

function Resize-DinoWindow {
    param([int] $Width, [int] $Height)
    $win = Get-DinoWindow
    $win.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
    Start-Sleep -Milliseconds 800
    (Get-DinoWindow).GetCurrentPattern([System.Windows.Automation.TransformPattern]::Pattern).Resize($Width, $Height)
    Start-Sleep -Seconds 2
}

function Expected-Columns {
    param([int] $ContentWidth)
    $room = [math]::Max(0, $ContentWidth - $RailAndSides)
    return [int][math]::Min($MaxColumns, [math]::Max($MinColumns, [math]::Floor(($room + $ColumnGap) / ($ColumnWidth + $ColumnGap))))
}

function Test-ShelfColumns {
    param($Package)
    $work = Get-WorkArea
    Resize-DinoWindow $MinWidth $MinHeight
    $small = Get-ShelfLine $Package
    if ($small.GrownUp -ne 'False') { throw 'FAIL the check expected the kids'' shelf' }
    if ($small.Sideways -ne 'False') { throw "FAIL at the minimum window ($($small.Width)x$($small.Height)) the shelf is still the phone's sideways row: $($small.Sideways)" }
    if ($small.Columns -ne (Expected-Columns $small.Width)) { throw "FAIL the minimum window ($($small.Width) wide) has $($small.Columns) shelf columns, expected $(Expected-Columns $small.Width)" }

    $bigW = [math]::Min($OpenWidth, $work.W - 20)
    $bigH = [math]::Min($OpenHeight, $work.H - 20)
    Resize-DinoWindow $bigW $bigH
    $big = Get-ShelfLine $Package
    if ($big.Columns -ne (Expected-Columns $big.Width)) { throw "FAIL the larger window ($($big.Width) wide) has $($big.Columns) shelf columns, expected $(Expected-Columns $big.Width)" }
    if ($big.Columns -lt $small.Columns) { throw "FAIL the shelf lost columns from the minimum ($($small.Columns) at $($small.Width)) to the larger window ($($big.Columns) at $($big.Width))" }
    return "shelf $($small.Columns) columns at $($small.Width) dp (grid, not a row), $($big.Columns) at $($big.Width)"
}

function Find-FirstTile {
    return Wait-Until {
        foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
            if ($e.Current.AutomationId -eq 'StoryTile' -and -not $e.Current.BoundingRectangle.IsEmpty) { return $e }
        }
    } 15 'FAIL no visible story tile'
}

Add-Type -Namespace Win32 -Name Fore -MemberDefinition '[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow(); [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);' -ErrorAction SilentlyContinue

function Get-HoverProbe {
    # One HOVERPROBE line: tile rect, window rect, foreground window title and the UIA element under the cursor (diagnosis, Slice 235c).
    param($TileRect, [int] $Attempt)
    $title = New-Object System.Text.StringBuilder 256
    [Win32.Fore]::GetWindowText([Win32.Fore]::GetForegroundWindow(), $title, 256) | Out-Null
    $under = 'none'
    try {
        $pos = [System.Windows.Forms.Cursor]::Position
        $e = [System.Windows.Automation.AutomationElement]::FromPoint((New-Object System.Windows.Point($pos.X, $pos.Y)))
        $under = "type=$($e.Current.ControlType.ProgrammaticName) name='$($e.Current.Name)' id='$($e.Current.AutomationId)'"
    } catch { $under = "error $($_.Exception.Message)" }
    return "HOVERPROBE attempt=$Attempt tile=$($TileRect.ToString()) window=$((Get-DinoWindow).Current.BoundingRectangle.ToString()) cursor=$([System.Windows.Forms.Cursor]::Position.ToString()) foreground='$($title.ToString())' under=[$under]"
}

function Test-Hover {
    param($Package)
    # After a resize the shelf relays out and the cursor may already sit where a tile now is, so: park the cursor off the shelf, let the layout settle, re-find the tile and walk the cursor onto it in steps (real moves raise PointerEntered; a jump may not).
    $before = Count-Trace $Package 'Dino shelf: a book is hovered'
    $hovered = $false
    $probe = 'HOVERPROBE none'; $script:HoverProbes = @()
    foreach ($attempt in 1..3) {
        Focus-DinoWindow
        [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(5, 5)
        Start-Sleep -Milliseconds 700
        $r = (Find-FirstTile).Current.BoundingRectangle
        $cx = [int]($r.X + $r.Width / 2); $cy = [int]($r.Y + $r.Height / 2)
        foreach ($step in 1..8) {
            [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(([int](5 + ($cx - 5) * $step / 8)), ([int](5 + ($cy - 5) * $step / 8)))
            # Setting Cursor.Position alone raised no WinUI pointer move on the VM (runs 1-3: every "a book is hovered" line came with a mouse_event click), so each step also sends a real
            # one-pixel MOUSEEVENTF_MOVE (1) through the input queue.
            [Win32.Mouse]::mouse_event(1, 1, 0, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 60
        }
        $probe = Get-HoverProbe $r $attempt
        Write-Host $probe; $script:HoverProbes += $probe
        try { Wait-TraceAfter $Package 'Dino shelf: a book is hovered' $before 3 'retry' | Out-Null; $hovered = $true; break } catch { }
    }
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(5, 5)
    if (-not $hovered) { throw 'FAIL moving the mouse over a book did not lift it (no "a book is hovered" trace line after 3 tries) ' + $probe }
}

function Test-TabAndEnter {
    param($Package)
    Focus-DinoWindow
    $before = Count-Trace $Package 'Dino shelf: a book has the keyboard'
    $tabs = 0
    while ((Count-Trace $Package 'Dino shelf: a book has the keyboard') -le $before) {
        if ($tabs -ge $MaxTabs) { throw "FAIL Tab never reached a book on the shelf within $MaxTabs presses" }
        Press-Key $VkTab
        $tabs++
    }
    $opened = Count-Trace $Package 'Dino shelf: Enter or Space opened a book'
    Press-Key $VkEnter
    Wait-TraceAfter $Package 'Dino shelf: Enter or Space opened a book' $opened 10 'FAIL Enter on the focused book did not open it (no trace line)' | Out-Null
    Wait-Until { Find-UiaById (Get-DinoWindow) 'ReadingTransport' } 20 'FAIL the reading page did not open after Enter on a book' | Out-Null
    Focus-DinoWindow
    Press-Key $VkEscape
    Wait-VisibleId 'GalleryLock' 10 'FAIL Esc did not come back to the shelf' | Out-Null
    return $tabs
}

function Pass-Gate {
    Invoke-Uia (Wait-VisibleId 'GalleryLock' 15 'FAIL the lock is not on the shelf')   # Slice 418: the gate opens from the lock of the locked shelf
    $sum = Invoke-GateHold -FindRing { Wait-VisibleName $HoldRingName 15 'FAIL no hold ring' } -PackageName $PackageName -HoldMs $HoldMs
    # Slice 363: the gate is a card with six answer keys (one click answers); a narrow window still has the digit pad and OK.
    $answerKey = Find-UiaExact (Get-DinoWindow) ([string]$sum) 'Button'
    if ($answerKey) {
        Invoke-Uia $answerKey
    } else {
        foreach ($ch in ([string]$sum).ToCharArray()) {
            Invoke-Uia (Wait-Until { Find-UiaExact (Get-DinoWindow) ([string]$ch) 'Button' } 5 "FAIL no keypad key $ch")
            Start-Sleep -Milliseconds 300
        }
        Invoke-Uia (Wait-Until { Find-UiaExact (Get-DinoWindow) 'OK' 'Button' } 5 'FAIL no OK key')
    }
    Wait-VisibleId 'LockChip' 15 'FAIL the shelf did not unlock after the gate' | Out-Null
}

function Right-Click {
    param([int] $X, [int] $Y)
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point($X, $Y)
    Start-Sleep -Milliseconds 300
    [Win32.Mouse]::mouse_event(8, 0, 0, 0, [UIntPtr]::Zero)    # MOUSEEVENTF_RIGHTDOWN
    Start-Sleep -Milliseconds 80
    [Win32.Mouse]::mouse_event(16, 0, 0, 0, [UIntPtr]::Zero)   # MOUSEEVENTF_RIGHTUP
}

function Left-Click {
    param([int] $X, [int] $Y)
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point($X, $Y)
    Start-Sleep -Milliseconds 300
    [Win32.Mouse]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)    # MOUSEEVENTF_LEFTDOWN
    Start-Sleep -Milliseconds 80
    [Win32.Mouse]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)    # MOUSEEVENTF_LEFTUP
}

function Get-ShelfStateAfterBack {
    # Names which of the three states the app is in, so a failed Back says whether it never left the book, auto-locked (Slice 418 decisions 13-14: 30 s after the last grown-up tap, reading does not reset it) or is unlocked without the chip.
    $win = Get-DinoWindow
    if (Find-UiaVisibleById $win 'ReadingTransport') { return 'the reading page is still open (Back was not taken)' }
    if (Test-LockedShelfShown $win) { return 'the shelf is back but locked (the 30 s auto-lock ran out while reading)' }
    return 'the shelf is showing without the lock chip and without the locked look'
}

function Test-UnlockedCoverReads {
    # Slice 420 (regression for Slice 418 decision 6): on the UNLOCKED shelf a mouse click on a cover reads the book; the sheet opens only from the dots (TileMore) or the context key. 1.136.0 shipped with the
    # click opening the sheet. A real click at the cover's left-middle, clear of the dots in its top right corner; then the reading page shows, no sheet does, and Back returns to the same unlocked shelf.
    $tile = Find-FirstTile
    $r = $tile.Current.BoundingRectangle
    Left-Click ([int]($r.X + $r.Width / 3)) ([int]($r.Y + $r.Height / 2))
    Wait-Until { Find-UiaById (Get-DinoWindow) 'ReadingTransport' } 15 'FAIL a click on a cover on the unlocked shelf did not open the reading page' | Out-Null
    if (Find-UiaVisibleById (Get-DinoWindow) 'BookSheetClose') { throw 'FAIL a click on a cover on the unlocked shelf opened the book sheet as well as, or instead of, reading' }
    $back = Wait-VisibleId 'ReadingBack' 5 'FAIL no Back button on the reading page'
    Start-Sleep -Milliseconds 1500   # fixed-wait: the page is still being pushed when its Back button first shows, and a Back pressed mid-push is dropped (the 2026-11-20 run: no shelf line in the trace after the reading page opened)
    Invoke-Uia $back
    try { Wait-Until { Find-UiaVisibleById (Get-DinoWindow) 'LockChip' } 10 'back not on the unlocked shelf' | Out-Null } catch { throw "FAIL Back from the reading page did not return to the unlocked shelf (waited 10 s): $(Get-ShelfStateAfterBack)" }
    return 'a click on an unlocked cover read the book (no sheet), Back returned to the unlocked shelf'
}

function Test-RailWidth {
    # Slice 420 item 3 (mock WL1, WL2): the unlocked Windows shelf has a 270 dp rail (CornerLayout.WindowsRail; it was 280 before review pass 1 of Slice 418) with padding 28 left and 20 right, so its rows are 222 dp wide,
    # and the books start 32 dp right of it. Measured from the UIA rectangles in dp (physical pixels / the window's DPI scale), from the window's left edge, with a few dp of slack for the window frame.
    $win = Get-DinoWindow
    if (-not ('Rtst.RtstDpiProbe' -as [type])) { Add-Type -Namespace Rtst -Name RtstDpiProbe -MemberDefinition '[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern uint GetDpiForWindow(System.IntPtr hwnd);' }
    $dpi = [Rtst.RtstDpiProbe]::GetDpiForWindow([System.IntPtr]$win.Current.NativeWindowHandle)
    if ($dpi -le 0) { throw 'CHECK ERROR (not an app failure): GetDpiForWindow returned 0' }
    $scale = [double]$dpi / 96
    $left = $win.Current.BoundingRectangle.X
    $slack = 8
    $rows = foreach ($id in 'GalleryLock', 'CornerTabBooks', 'GalleryStore', 'GallerySettings') { $e = Find-UiaVisibleById $win $id; if ($e) { $e.Current.BoundingRectangle } }
    if (-not $rows) { throw 'FAIL no rail row (lock or tab) is showing on the unlocked shelf, so the rail width cannot be read' }
    $widest = ($rows | ForEach-Object { $_.Width / $scale } | Measure-Object -Maximum).Maximum
    $railRight = ($rows | ForEach-Object { ($_.X + $_.Width - $left) / $scale } | Measure-Object -Maximum).Maximum
    if ($widest -gt 222 + $slack -or $railRight -gt 250 + $slack) { throw "FAIL the rail is wider than the mock's 270 dp (WL1/WL2): widest row $([int]$widest) dp (mock 222), rail rows end at $([int]$railRight) dp from the window's left edge (mock at most 250)" }
    $tileLeft = ((Find-FirstTile).Current.BoundingRectangle.X - $left) / $scale
    if ($tileLeft -lt 270 + 32 - $slack) { throw "FAIL the first book starts $([int]$tileLeft) dp from the window's left edge, the mock's 270 dp rail plus 32 dp puts it at 302 or more" }
    $measured = "rail 270 dp: widest rail row $([int]$widest) dp, rows end at $([int]$railRight) dp, first book at $([int]$tileLeft) dp"
    Write-Host "RAILWIDTH $measured (dpi $dpi, scale $scale)"   # printed on a pass too, so a run shows the numbers
    return $measured
}

function Test-RightClickAndFocusBack {
    param($Package)
    $tile = Find-FirstTile
    $r = $tile.Current.BoundingRectangle
    $clicked = Count-Trace $Package 'right-click opened the book sheet'
    # Aim at the cover's upper left, clear of the dots button in its top right corner.
    Right-Click ([int]($r.X + $r.Width / 3)) ([int]($r.Y + $r.Height / 2))
    Wait-TraceAfter $Package 'right-click opened the book sheet' $clicked 10 'FAIL a right-click on a book in the grown-ups'' corner did not open its sheet (no trace line)' | Out-Null
    Wait-VisibleId 'BookSheetClose' 10 'FAIL the book sheet is not showing after the right-click' | Out-Null
    Focus-DinoWindow
    Press-Key $VkEscape
    Wait-Until { -not (Find-UiaVisibleById (Get-DinoWindow) 'BookSheetClose') } 5 'FAIL Esc did not close the book sheet' | Out-Null
    # The keyboard returns to the book only when the keyboard opened the sheet (high review fix 3: a mouse open never moves the focus ring), so open it again with Tab and Enter.
    $before = Count-Trace $Package 'Dino shelf: a book has the keyboard'
    $tabs = 0
    while ((Count-Trace $Package 'Dino shelf: a book has the keyboard') -le $before) {
        if ($tabs -ge $MaxTabs) { throw "FAIL Tab never reached a book in the grown-ups' corner within $MaxTabs presses" }
        Press-Key $VkTab
        $tabs++
    }
    # Slice 420: Enter on an unlocked book reads it (Slice 418 decision 6, same as a cover tap); the keyboard's way to the sheet is Shift+F10 (the context-menu key), GalleryPage.Desktop.cs OnListContextRequested.
    $opened = Count-Trace $Package 'Dino shelf: context key opened the book sheet'
    Press-ShiftF10
    Wait-TraceAfter $Package 'Dino shelf: context key opened the book sheet' $opened 10 'FAIL Shift+F10 on the focused book in the unlocked shelf wrote no "context key opened the book sheet" trace line' | Out-Null
    Wait-VisibleId 'BookSheetClose' 10 'FAIL Shift+F10 on the focused book in the unlocked shelf did not open its sheet' | Out-Null
    Focus-DinoWindow
    $back = Count-Trace $Package 'the book has the keyboard back after the sheet closed'
    Press-Key $VkEscape
    Wait-Until { -not (Find-UiaVisibleById (Get-DinoWindow) 'BookSheetClose') } 5 'FAIL Esc did not close the keyboard-opened book sheet' | Out-Null
    $line = Wait-TraceAfter $Package 'the book has the keyboard back after the sheet closed' $back 5 'FAIL the keyboard did not return to the book after the sheet closed (no trace line)'
    if ($line -notmatch ': True') { throw "FAIL the book refused the keyboard after the sheet closed: $line" }
}

function Get-FocusedKey {
    $f = $script:AutomationElement::FocusedElement
    if (-not $f) { return '' }
    $r = $f.Current.BoundingRectangle
    return "$($f.Current.ControlType.ProgrammaticName)|$($f.Current.Name)|$($f.Current.AutomationId)|at $([int]$r.X),$([int]$r.Y)"   # the place too: two rows with one name (a header button and a row) must not read as one focus
}

function Test-SettingsArrows {
    Invoke-Uia (Wait-VisibleId 'GallerySettings' 5 'FAIL no Settings tab in the grown-ups corner')
    Wait-VisibleId 'SettingsTitle' 15 'FAIL the Settings list did not show' | Out-Null
    Focus-DinoWindow
    # Slice 325 part B: Settings is a tab beside the rail, so the first Tabs land on the rail (the coral button, the tabs) before a Settings row: Tab until a row of the list has the focus.
    $first = $null
    foreach ($i in 1..12) {
        Press-Key $VkTab
        $first = Get-FocusedKey
        if ($first -match 'Button\|(Language|Story languages|Reading speed|Animal sounds)') { break }
    }
    if (-not $first -or $first -notmatch 'Button\|(Language|Story languages|Reading speed|Animal sounds)') { throw "FAIL Tab never reached a Settings row (focus '$first')" }
    Press-Key $VkDown
    $second = Get-FocusedKey
    if (-not $first -or $first -eq $second) { throw "FAIL the Down arrow did not move the focus in Settings (before '$first', after '$second')" }
    # Esc on a Settings list goes to Books and then asks to close (Slice 418), so the Body ends this tab by a tap on the lock instead.
    return "Down moved the focus from '$first' to '$second'"
}

Invoke-Check -ResultFile $ResultFile -Body {
    $pkg = Get-InstalledPackage $PackageName
    $script:StagesPassed = @()
    try {
        Enter-KidsShelf   # a tap on the lock locks an unlocked shelf (Slice 418)
        $columns = Test-ShelfColumns $pkg
        $script:StagesPassed += "columns: $columns"
        Test-Hover $pkg
        $script:StagesPassed += 'hover'
        $tabs = Test-TabAndEnter $pkg
        $script:StagesPassed += "tab and Enter ($tabs presses)"
        Pass-Gate
        $rail = Test-RailWidth
        $script:StagesPassed += $rail
        $cover = Test-UnlockedCoverReads
        $script:StagesPassed += $cover
        Test-RightClickAndFocusBack $pkg
        $script:StagesPassed += 'right-click and Shift+F10 sheet, keyboard back on the book'
        $arrows = Test-SettingsArrows
        Invoke-Uia (Wait-VisibleId 'GalleryLock' 5 'FAIL no lock on the unlocked shelf')
        Wait-Until { Test-LockedShelfShown (Get-DinoWindow) } 10 'FAIL a tap on the lock did not lock the shelf' | Out-Null
        "$columns; a hovered book lifted; Tab reached a book in $tabs presses and Enter read it on the locked shelf; right-click opened the sheet, then Shift+F10 opened it from the keyboard and Esc gave the book the keyboard back; $rail; $cover; $arrows; $($script:HoverProbes[-1])"
    } catch {
        $tail = (Get-TraceLines $pkg | Select-Object -Last 25) -join ' | '
        throw "$($_.Exception.Message) -- stages passed before it: $($script:StagesPassed -join '; ') -- window-trace.log tail: $tail"   # a failure keeps each earlier stage's measured result
    }
}
