# after: dino-launch
# Shelf reorder mode on the Windows head (Slice 264). Runs after dino-launch (shelf with the two test books picked there, kids' mode).
# Stages, each recorded as PASS/FAIL and the next one still runs (a failed stage ends with Esc to leave any open reorder layer):
#   1 grown-up gate -> tap a cover -> book sheet -> "Reorder" row -> reorder layer opens, the app is alive 5 s later and window-trace.log has no new LayoutCycle line
#   2 Esc = Cancel closes the layer
#   3 a 1 s mouse hold on a cover opens the layer
#   4 drag the 2nd tile before the 1st, Enter = Done: the shelf order changed, and it survives an app restart (kill + relaunch)
#   5 drag again, Cancel button: the shelf order is unchanged
# Order is read from the StoryTileTitle labels sorted by position. Only two test books are picked in dino-launch, so the brief's 6-book order (1,2,5,3,4,6) is a 2-book swap here.
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"

$HoldRingName = 'Press and hold for 3 seconds'
$HoldMs = 3500
$CoverHoldMs = 1300
$TraceFileName = 'window-trace.log'
$VkEnter = 0x0D; $VkEscape = 0x1B
$script:Results = @()

function Get-DinoWindow {
    $w = Get-AppWindows 'DinoReader.App' | Select-Object -First 1
    if (-not $w) { throw 'Dino Reader is not running' }
    return $w
}

function Get-TracePath { return (Join-Path $env:LOCALAPPDATA "Packages\$((Get-InstalledPackage $PackageName).PackageFamilyName)\LocalState\$TraceFileName") }

function Get-TraceLines {
    $p = Get-TracePath
    if (-not (Test-Path $p)) { return @() }
    return @(Get-Content -Path $p)
}

function Focus-DinoWindow {
    $w = Get-DinoWindow
    [NativeKeys]::SetForegroundWindow([IntPtr]$w.Current.NativeWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 400
}

function Press-Key {
    param([byte] $Vk)
    Focus-DinoWindow
    [NativeKeys]::keybd_event($Vk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [NativeKeys]::keybd_event($Vk, 0, [NativeKeys]::KeyUp, [UIntPtr]::Zero)
}

function Find-Visible {
    param([string] $Id)
    try { return Find-UiaVisibleById (Get-DinoWindow) $Id } catch { return $null }
}

function Get-ShelfOrder {
    # Titles of the shelf tiles (or the reorder tiles) in reading order: rows by Y, then X.
    param([string] $Id = 'StoryTileTitle')
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:AutomationElement::AutomationIdProperty, $Id)
    $items = foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, $cond)) {
        if (-not $e.Current.BoundingRectangle.IsEmpty) { [pscustomobject]@{ T = $e.Current.Name; X = $e.Current.BoundingRectangle.X; Y = [int]($e.Current.BoundingRectangle.Y / 20) } }
    }
    return @($items | Sort-Object Y, X | ForEach-Object { $_.T })
}

function Get-FirstTiles {
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:AutomationElement::AutomationIdProperty, 'StoryTile')
    $tiles = foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, $cond)) { if (-not $e.Current.BoundingRectangle.IsEmpty) { $e } }
    return @($tiles | Sort-Object { [int]($_.Current.BoundingRectangle.Y / 20) }, { $_.Current.BoundingRectangle.X })
}

function Enter-GrownUpMode {
    if (Find-Visible 'GalleryDone') { return }   # dino-launch ends in the grown-ups' corner
    Invoke-Uia (Wait-Until { Find-Visible 'GalleryGrownUps' } 15 'no For grown-ups button')
    $ring = Wait-Until { Find-UiaExact (Get-DinoWindow) $HoldRingName } 15 'gate did not open'
    Invoke-PressAndHold $ring $HoldMs
    $sum = Wait-Until {
        foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
            if ($e.Current.Name -match 'What is (\d+) plus (\d+)') { return [int]$Matches[1] + [int]$Matches[2] }
        }
    } 10 'gate sum did not appear'
    foreach ($ch in ([string]$sum).ToCharArray()) {
        Invoke-Uia (Wait-Until { Find-UiaExact (Get-DinoWindow) ([string]$ch) 'Button' } 5 "no keypad key $ch")
        Start-Sleep -Milliseconds 300
    }
    Invoke-Uia (Wait-Until { Find-UiaExact (Get-DinoWindow) 'OK' 'Button' } 5 'no OK key')
    Wait-Until { Find-Visible 'GalleryDone' } 15 'grown-up mode did not open' | Out-Null
}

function Drag-Tile {
    # Mouse press on the centre of $From, moves in small steps to the left edge of $To, release.
    param($From, $To)
    $x0 = $From.X; $y0 = $From.Y; $x1 = $To.X - 60; $y1 = $To.Y
    Move-MouseTo $x0 $y0   # real mouse_event moves, never Cursor.Position (no WinUI pointer move; Slice 269)
    Start-Sleep -Milliseconds 300
    [Win32.Mouse]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 700   # a drag lifts the tile on press in the reorder layer
    foreach ($i in 1..20) {
        Move-MouseTo ($x0 + [int](($x1 - $x0) * $i / 20)) ($y0 + [int](($y1 - $y0) * $i / 20))
        Start-Sleep -Milliseconds 60
    }
    Start-Sleep -Milliseconds 400
    [Win32.Mouse]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 500
}

function Add-Result {
    param([string] $Name, [scriptblock] $Body)
    try { $detail = & $Body; $script:Results += "PASS $Name $detail" }
    catch {
        $ui = ''
        try { $ui = (Get-VisibleNames (Get-DinoWindow) 25) -join ' / ' } catch { $ui = 'no window' }
        $script:Results += "FAIL $Name $($_.Exception.Message) UI: $ui"
        try { if (Get-Hint) { Press-Key $VkEscape } } catch { }
    }
}

function Get-Hint { try { return Find-UiaByName -Root (Get-DinoWindow) -Name 'then drag it to a new place' } catch { return $null } }
function Wait-ReorderOpen { Wait-Until { Get-Hint } 10 'reorder layer did not open' | Out-Null }
function Wait-ReorderClosed { Wait-Until { -not (Get-Hint) } 10 'reorder layer did not close' | Out-Null }
function Get-LayerTiles {
    # Centres of the reorder tiles in reading order, from the UIA bounding rectangles of their titles (physical pixels, so any DPI scale, window width or font scale works).
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:AutomationElement::AutomationIdProperty, 'ReorderTileTitle')
    $found = foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, $cond)) {
        $r = $e.Current.BoundingRectangle
        if (-not $r.IsEmpty) { [pscustomobject]@{ X = [int]($r.X + $r.Width / 2); Y = [int]($r.Y + $r.Height / 2); Row = [int]($r.Y / 20) } }
    }
    $tiles = @($found | Sort-Object Row, X)
    if ($tiles.Count -lt 2) { throw "the reorder layer shows $($tiles.Count) tile title(s) in the UIA tree, need two" }
    return $tiles
}

function Initialize-Shelf {
    # The check can follow flows that leave the reader open (dino-open-book, dino-page-turn): a restart lands on the shelf, and the reorder needs two books on it.
    if (-not (Find-Visible 'GalleryTitle')) {
        Get-Process DinoReader.App -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Milliseconds 1500
        Start-PackagedApp (Get-InstalledPackage $PackageName)
        Wait-Until { Get-AppWindows 'DinoReader.App' | Select-Object -First 1 } 30 'app window did not open' | Out-Null
        Wait-Until { Find-Visible 'GalleryTitle' } 30 'shelf did not show' | Out-Null
    }
}

function Open-Reorder-FromSheet {
    $tile = (Get-FirstTiles)[0]
    if (-not $tile) { throw 'no story tile in grown-up mode' }
    Invoke-Uia $tile
    Invoke-Uia (Wait-Until { Find-Visible 'BookSheetReorder' } 10 'no Reorder row in the book sheet')
}

Invoke-Check -ResultFile $ResultFile -Body {
    $before = (Get-TraceLines).Count
    $cycleBefore = @(Get-TraceLines | Where-Object { $_ -match 'LayoutCycle|layout cycle' }).Count
    Add-Result '0-shelf' {
        Initialize-Shelf
        Wait-Until { (Get-ShelfOrder).Count -ge 2 } 15 'SETUP FAILURE (not an app failure): need two books on the shelf (dino-launch picks them), found fewer' | Out-Null
        'shelf shown with ' + ((Get-ShelfOrder) -join ' | ')
    }
    Add-Result '1-gate' { Enter-GrownUpMode; 'grown-up mode open, shelf ' + ((Get-ShelfOrder) -join ' | ') }
    $order0 = Get-ShelfOrder
    if ($order0.Count -lt 2) { throw "SETUP FAILURE (not an app failure): need two books on the shelf, found $($order0.Count): $($script:Results -join ' // ')" }
    Add-Result '1-sheet-reorder' {
        Open-Reorder-FromSheet
        Wait-ReorderOpen
        Start-Sleep -Seconds 5   # fixed-wait: the brief asks for 5 s after open before reading the trace
        if (-not (Get-Process DinoReader.App -ErrorAction SilentlyContinue)) { throw 'the app died after the reorder layer opened' }
        $cyc = @(Get-TraceLines | Where-Object { $_ -match 'LayoutCycle|layout cycle' }).Count - $cycleBefore
        if ($cyc -gt 0) { throw "$cyc layout-cycle trace lines after open" }
        'layer open, app alive 5 s, 0 layout-cycle lines, tiles ' + ((Get-ShelfOrder 'ReorderTileTitle') -join ' | ')
    }
    Add-Result '5a-esc-cancels' { Press-Key $VkEscape; Wait-ReorderClosed; if (((Get-ShelfOrder) -join '|') -ne ($order0 -join '|')) { throw 'order changed' }; 'Esc closed the layer' }
    Add-Result '2-hold-1s' {
        $tile = (Get-FirstTiles)[0]
        Invoke-PressAndHold $tile $CoverHoldMs
        Wait-ReorderOpen
        'a 1.3 s mouse hold opened the layer'
    }
    Add-Result '3-drag-done-enter' {
        $t = Get-LayerTiles
        Drag-Tile $t[1] $t[0]
        $inLayer = (Get-ShelfOrder 'ReorderTileTitle') -join ' | '
        Press-Key $VkEnter
        Wait-ReorderClosed
        $after = Get-ShelfOrder
        if (($after -join '|') -ne (@($order0[1], $order0[0]) -join '|')) { throw "order after Done is '$($after -join ' | ')' (in layer: $inLayer), wanted '$($order0[1]) | $($order0[0])'" }
        'Enter=Done saved ' + ($after -join ' | ')
    }
    $order1 = Get-ShelfOrder
    Add-Result '3-survives-restart' {
        Get-Process DinoReader.App | Stop-Process -Force
        Start-Sleep -Milliseconds 1500
        Start-PackagedApp (Get-InstalledPackage $PackageName)
        Wait-Until { Get-AppWindows 'DinoReader.App' | Select-Object -First 1 } 30 'app window did not open after restart' | Out-Null
        Wait-Until { Find-Visible 'GalleryTitle' } 30 'shelf did not show after restart' | Out-Null
        Wait-Until { (Get-ShelfOrder).Count -ge 2 } 15 'tiles not shown after restart' | Out-Null
        $now = Get-ShelfOrder
        if (($now -join '|') -ne ($order1 -join '|')) { throw "after restart '$($now -join ' | ')', wanted '$($order1 -join ' | ')'" }
        'restart kept ' + ($now -join ' | ')
    }
    Add-Result '4-cancel-button' {
        Enter-GrownUpMode
        Open-Reorder-FromSheet
        Wait-ReorderOpen
        $t = Get-LayerTiles
        Drag-Tile $t[1] $t[0]
        Invoke-Uia (Wait-Until { Find-UiaExact (Get-DinoWindow) 'Cancel' } 5 'no Cancel control')
        Wait-ReorderClosed
        $now = Get-ShelfOrder
        if (($now -join '|') -ne ($order1 -join '|')) { throw "after Cancel '$($now -join ' | ')', wanted '$($order1 -join ' | ')'" }
        'Cancel restored ' + ($now -join ' | ')
    }
    $tail = @(Get-TraceLines | Select-Object -Last 25) -join ' // '
    $text = ($script:Results -join "`n") + "`nTRACE-TAIL: $tail"
    if ($script:Results | Where-Object { $_ -like 'FAIL*' }) { throw $text }
    $text
}
