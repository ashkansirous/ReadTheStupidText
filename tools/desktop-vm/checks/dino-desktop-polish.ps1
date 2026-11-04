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
$ColumnWidth = 190
$MaxColumns = 6
$MinColumns = 2
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
    return [int][math]::Min($MaxColumns, [math]::Max($MinColumns, [math]::Floor([math]::Min($ContentWidth, 1600) / $ColumnWidth)))
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
    if ($big.Columns -le $small.Columns) { throw "FAIL the shelf did not gain columns from the minimum ($($small.Columns) at $($small.Width)) to the larger window ($($big.Columns) at $($big.Width))" }
    return "shelf $($small.Columns) columns at $($small.Width) dp (grid, not a row), $($big.Columns) at $($big.Width)"
}

function Find-FirstTile {
    return Wait-Until {
        foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
            if ($e.Current.AutomationId -eq 'StoryTile' -and -not $e.Current.BoundingRectangle.IsEmpty) { return $e }
        }
    } 15 'FAIL no visible story tile'
}

function Test-Hover {
    param($Package)
    $tile = Find-FirstTile
    $r = $tile.Current.BoundingRectangle
    $before = Count-Trace $Package 'Dino shelf: a book is hovered'
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(([int]($r.X + 5)), ([int]($r.Y + 5)))
    Start-Sleep -Milliseconds 200
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(([int]($r.X + $r.Width / 2)), ([int]($r.Y + $r.Height / 2)))
    Wait-TraceAfter $Package 'Dino shelf: a book is hovered' $before 5 'FAIL moving the mouse over a book did not lift it (no "a book is hovered" trace line)' | Out-Null
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(5, 5)
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
    Wait-VisibleId 'GalleryGrownUps' 10 'FAIL Esc did not come back to the kids'' shelf' | Out-Null
    return $tabs
}

function Pass-Gate {
    Invoke-Uia (Wait-VisibleId 'GalleryGrownUps' 15 'FAIL the "For grown-ups" button is not on the kids'' shelf')
    Invoke-PressAndHold (Wait-VisibleName $HoldRingName 15 'FAIL no hold ring') $HoldMs
    $sum = $null
    $sum = Wait-Until {
        foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
            if ($e.Current.Name -match 'What is (\d+) plus (\d+)') { return [int]$Matches[1] + [int]$Matches[2] }
        }
    } 10 'FAIL the gate sum did not appear after the hold'
    foreach ($ch in ([string]$sum).ToCharArray()) {
        Invoke-Uia (Wait-Until { Find-UiaExact (Get-DinoWindow) ([string]$ch) 'Button' } 5 "FAIL no keypad key $ch")
        Start-Sleep -Milliseconds 300
    }
    Invoke-Uia (Wait-Until { Find-UiaExact (Get-DinoWindow) 'OK' 'Button' } 5 'FAIL no OK key')
    Wait-VisibleId 'GalleryDone' 15 'FAIL grown-up mode did not open after the gate' | Out-Null
}

function Right-Click {
    param([int] $X, [int] $Y)
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point($X, $Y)
    Start-Sleep -Milliseconds 300
    [Win32.Mouse]::mouse_event(8, 0, 0, 0, [UIntPtr]::Zero)    # MOUSEEVENTF_RIGHTDOWN
    Start-Sleep -Milliseconds 80
    [Win32.Mouse]::mouse_event(16, 0, 0, 0, [UIntPtr]::Zero)   # MOUSEEVENTF_RIGHTUP
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
    $back = Count-Trace $Package 'the book has the keyboard back after the sheet closed'
    Press-Key $VkEscape
    Wait-Until { -not (Find-UiaVisibleById (Get-DinoWindow) 'BookSheetClose') } 5 'FAIL Esc did not close the book sheet' | Out-Null
    $line = Wait-TraceAfter $Package 'the book has the keyboard back after the sheet closed' $back 5 'FAIL the keyboard did not return to the book after the sheet closed (no trace line)'
    if ($line -notmatch ': True') { throw "FAIL the book refused the keyboard after the sheet closed: $line" }
}

function Get-FocusedKey {
    $f = $script:AutomationElement::FocusedElement
    if (-not $f) { return '' }
    return "$($f.Current.ControlType.ProgrammaticName)|$($f.Current.Name)|$($f.Current.AutomationId)"
}

function Test-SettingsArrows {
    Invoke-Uia (Wait-VisibleId 'GallerySettings' 5 'FAIL no gear in grown-up mode')
    Wait-VisibleId 'SettingsTitle' 15 'FAIL Settings did not open from the gear' | Out-Null
    Focus-DinoWindow
    Press-Key $VkTab   # the first Tab lands on the first row or button
    $first = Get-FocusedKey
    Press-Key $VkDown
    $second = Get-FocusedKey
    if (-not $first -or $first -eq $second) { throw "FAIL the Down arrow did not move the focus in Settings (before '$first', after '$second')" }
    Press-Key $VkEscape
    Wait-VisibleId 'GalleryDone' 10 'FAIL Esc on Settings did not go back to the grown-ups corner' | Out-Null
    return "Down moved the focus from '$first' to '$second'"
}

Invoke-Check -ResultFile $ResultFile -Body {
    $pkg = Get-InstalledPackage $PackageName
    try {
        $columns = Test-ShelfColumns $pkg
        Test-Hover $pkg
        $tabs = Test-TabAndEnter $pkg
        Pass-Gate
        Test-RightClickAndFocusBack $pkg
        $arrows = Test-SettingsArrows
        Invoke-Uia (Wait-VisibleId 'GalleryDone' 5 'FAIL no Done button in the grown-ups corner')
        Wait-VisibleId 'GalleryGrownUps' 10 'FAIL Done did not return to the kids'' shelf' | Out-Null
        "$columns; a hovered book lifted; Tab reached a book in $tabs presses and Enter opened it; right-click opened the sheet and Esc gave the book the keyboard back; $arrows"
    } catch {
        $tail = (Get-TraceLines $pkg | Select-Object -Last 25) -join ' | '
        throw "$($_.Exception.Message) -- window-trace.log tail: $tail"
    }
}
