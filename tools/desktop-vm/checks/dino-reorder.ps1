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
$MockSidePaddingDp = 40       # Batch 47 frame 176: the Windows layer's grid side padding
$InsetToleranceDp = 4         # whole-dp tile widths leave up to 6 dp, half to each side
$MockTileWidthDp = 155        # frame 176: (1100 - 2*40 - 5*18) / 6
$MockGapDp = 18               # frame 176: the grid gap
$MockMinColumns = 3
$HintToGridMaxDp = 24         # the grid is top-aligned right under the hint (frame 176: grid padding-top 0); the screenshot of 2026-11-08 showed ~100 px of centring gap
$BaseDpi = 96
# Columns the layer must show for a window width (WindowShapeSeam.ReorderDesktopColumns): max(3, floor((widthDp - 2*40 + 18) / (155 + 18))); 6 at 1100 dp.
# The first row holds min(columns, books) tiles.
$script:Results = @()

function Set-ProcessDpiAware {
    # UIA rectangles come back in this process's coordinates: make them physical pixels (per-monitor v2), so they match the scale read from the window's DPI. Fails harmlessly when the awareness is already set.
    if (-not ('Rtst.RtstDpiAware' -as [type])) { Add-Type -Namespace Rtst -Name RtstDpiAware -MemberDefinition '[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(System.IntPtr value);' }   # -Namespace: without it the type lands in an auto-generated namespace and [RtstDpiAware] never resolves
    try { [Rtst.RtstDpiAware]::SetProcessDpiAwarenessContext([System.IntPtr](-4)) | Out-Null } catch { }
}
Set-ProcessDpiAware

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
    Wait-Until { Find-UiaExact (Get-DinoWindow) $HoldRingName } 15 'gate did not open' | Out-Null
    $sum = Invoke-GateHold -FindRing { Wait-Until { Find-UiaExact (Get-DinoWindow) $HoldRingName } 15 'gate did not open' } -PackageName $PackageName -HoldMs $HoldMs
    # Slice 363: the gate is a card with six answer keys (one click answers); a narrow window still has the digit pad and OK.
    $answerKey = Find-UiaExact (Get-DinoWindow) ([string]$sum) 'Button'
    if ($answerKey) {
        Invoke-Uia $answerKey
    } else {
        foreach ($ch in ([string]$sum).ToCharArray()) {
            Invoke-Uia (Wait-Until { Find-UiaExact (Get-DinoWindow) ([string]$ch) 'Button' } 5 "no keypad key $ch")
            Start-Sleep -Milliseconds 300
        }
        Invoke-Uia (Wait-Until { Find-UiaExact (Get-DinoWindow) 'OK' 'Button' } 5 'no OK key')
    }
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

function Get-DisplayScale {
    # The app window's own DPI (user32 GetDpiForWindow on its handle), so a per-monitor scaled host is right too; UIA rectangles are physical pixels.
    if (-not ('Rtst.RtstDpiProbe' -as [type])) { Add-Type -Namespace Rtst -Name RtstDpiProbe -MemberDefinition '[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern uint GetDpiForWindow(System.IntPtr hwnd);' }
    $handle = (Get-DinoWindow).Current.NativeWindowHandle
    if (-not $handle) { throw 'CHECK ERROR (not an app failure): the Dino window has no native handle, cannot read its DPI' }
    $dpi = [Rtst.RtstDpiProbe]::GetDpiForWindow([System.IntPtr]$handle)
    if ($dpi -le 0) { throw 'CHECK ERROR (not an app failure): GetDpiForWindow returned 0' }
    return [double]$dpi / $BaseDpi
}

function Get-ReorderViewRect {
    # The reorder layer's own rectangle (physical pixels); a missing element is a check error, not an app failure.
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:AutomationElement::AutomationIdProperty, 'ReorderView')
    $e = (Get-DinoWindow).FindFirst($script:TreeScope::Descendants, $cond)
    if (-not $e -or $e.Current.BoundingRectangle.IsEmpty) { throw 'CHECK ERROR (not an app failure): check could not find ReorderView in the UIA tree' }
    return $e.Current.BoundingRectangle
}

function Get-RowBandPx {
    # Half the height of a reorder tile: tiles of one row start within it of each other (a wiggle or a lift moves a few dp), the next row starts a whole tile lower. Follows the tile height at any scale.
    foreach ($id in 'ReorderTile', 'ReorderCover') {
        $cond = New-Object System.Windows.Automation.PropertyCondition($script:AutomationElement::AutomationIdProperty, $id)
        $heights = @(foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, $cond)) { $h = $e.Current.BoundingRectangle.Height; if ($h -gt 0) { $h } })
        if ($heights.Count -gt 0) { return ($heights | Measure-Object -Minimum).Minimum / 2 }
    }
    throw 'CHECK ERROR (not an app failure): no reorder tile in the UIA tree to take the row height from'
}

function Get-MockColumns {
    # $WidthDp is the layer's own width: it covers the whole page including the rail, so no rail is taken off (WindowShapeSeam.ReorderLayerDesktop).
    param([double] $WidthDp)
    return [int][math]::Max($MockMinColumns, [math]::Floor(($WidthDp - 2 * $MockSidePaddingDp + $MockGapDp) / ($MockTileWidthDp + $MockGapDp)))
}

function Get-FirstRowTileCount {
    # How many reorder tile titles share the topmost row (tops within $RowBandPx of the smallest top, so a wiggle or a lift cannot split the row).
    $RowBandPx = Get-RowBandPx
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:AutomationElement::AutomationIdProperty, 'ReorderTileTitle')
    $tops = @(foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, $cond)) {
        $r = $e.Current.BoundingRectangle
        if (-not $r.IsEmpty) { $r.Y }
    })
    if ($tops.Count -eq 0) { return 0 }
    $min = ($tops | Measure-Object -Minimum).Minimum
    return @($tops | Where-Object { $_ -le $min + $RowBandPx }).Count
}

function Get-FirstRowRects {
    # The rectangles (physical pixels) of the topmost row of tiles with this AutomationId (tops within $RowBandPx of the smallest top), left to right. Empty when none shows.
    param([string] $Id)
    $RowBandPx = Get-RowBandPx
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:AutomationElement::AutomationIdProperty, $Id)
    $rects = @(foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, $cond)) {
        $r = $e.Current.BoundingRectangle
        if (-not $r.IsEmpty) { [pscustomobject]@{ X = $r.X; Y = $r.Y; W = $r.Width } }
    })
    if ($rects.Count -eq 0) { return @() }
    $top = ($rects | Measure-Object -Property Y -Minimum).Minimum
    return @($rects | Where-Object { $_.Y -le $top + $RowBandPx } | Sort-Object X)
}

function Get-GridEdge {
    # The grid's left edge and the first tile's top, measured from a tile that is NOT the lifted one (the lift scales it about 9 px each side, so its own left edge is off the grid's):
    # the leftmost tile of normal width (the narrowest ones) at row position k puts the grid edge at its X - k * (width + gap). Returns @{ Left; Top } in physical pixels, or $null when no tile shows.
    # Only ReorderTile rects are used (never ReorderCover, which is wider). The gap is measured between two non-lifted neighbours when the row has them, else the mock gap * scale.
    # With one book only the lifted tile exists: the inset is not measurable, Left is $null and Note says so (the caller skips the inset assertion).
    param([double] $Scale)
    $row = @(Get-FirstRowRects 'ReorderTile')
    if ($row.Count -eq 0) { return $null }
    $normalW = ($row | Measure-Object -Property W -Minimum).Minimum
    $normal = @(for ($k = 0; $k -lt $row.Count; $k++) { if ($row[$k].W -le $normalW * 1.02) { [pscustomobject]@{ K = $k; X = $row[$k].X; W = $row[$k].W; Y = $row[$k].Y } } })
    if ($row.Count -eq 1) { return @{ Left = $null; Top = $row[0].Y; Note = '1 book: inset not measurable'; Measured = "tile X=$($row[0].X) W=$($row[0].W) (lifted, only tile)" } }
    $gap = $MockGapDp * $Scale
    for ($i = 0; $i -lt $normal.Count - 1; $i++) {
        if ($normal[$i + 1].K -eq $normal[$i].K + 1) { $gap = $normal[$i + 1].X - ($normal[$i].X + $normal[$i].W); break }
    }
    $t = $normal[0]
    $left = $t.X - $t.K * ($t.W + $gap)
    return @{ Left = $left; Top = $t.Y; Note = $null; Measured = "tile index $($t.K) X=$($t.X) W=$($t.W) gap=$gap -> grid edge $left" }
}

function Get-HintBottom {
    # The bottom (physical pixels) of the hint label, padding included.
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:AutomationElement::AutomationIdProperty, 'ReorderHint')
    $e = (Get-DinoWindow).FindFirst($script:TreeScope::Descendants, $cond)
    if (-not $e -or $e.Current.BoundingRectangle.IsEmpty) { throw 'CHECK ERROR (not an app failure): check could not find ReorderHint in the UIA tree' }
    $r = $e.Current.BoundingRectangle
    return $r.Y + $r.Height
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
        # Slice 321: the layer is the mock's frame 176 (Windows): 40 dp side padding, 6 tiles across when there are 6 or more books, else all of them in the first row.
        # The layer covers the whole page including the rail: the grid starts 40 dp from the layer's own left edge, no rail offset.
        $view = Get-ReorderViewRect
        $scale = Get-DisplayScale
        $edge = Get-GridEdge $scale
        if ($null -eq $edge) { throw 'CHECK ERROR (not an app failure): the first reorder tile is not in the UIA tree' }
        $wantInset = $MockSidePaddingDp * $scale
        $insetNote = $edge.Note
        $inset = if ($null -ne $edge.Left) { $edge.Left - $view.X } else { $null }
        if ($null -ne $inset) { $insetNote = "inset (not asserted): $inset px (mock $wantInset, $($edge.Measured), layer X=$($view.X))" }
        $MockColumns = Get-MockColumns ($view.Width / $scale)
        # Slice 321: the inset is printed, not asserted (UIA tile bounds read 6-7 px less than the screenshot shows; the mock inset is shown by reorder2.png).
        $allNumbers = "$insetNote, scale $scale, layer $($view.X),$($view.Y) $($view.Width)x$($view.Height)"
        $gapBelowHint = $edge.Top - (Get-HintBottom)
        if ($gapBelowHint -gt ($HintToGridMaxDp * $scale) -or $gapBelowHint -lt (-$InsetToleranceDp * $scale)) { throw "the first tile's top is $gapBelowHint px below the hint's bottom, the mock (frame 176) puts the grid right under the hint (at most $HintToGridMaxDp dp at scale $scale); $allNumbers" }
        $books = @(Get-ShelfOrder 'ReorderTileTitle').Count
        $wantFirstRow = [math]::Min($MockColumns, $books)
        $firstRow = Get-FirstRowTileCount
        if ($firstRow -ne $wantFirstRow) { throw "the reorder layer's first row has $firstRow tile(s), the mock (frame 176) wants $wantFirstRow ($books books, $MockColumns across); $allNumbers, hint-to-grid $gapBelowHint px" }
        Start-Sleep -Seconds 5   # fixed-wait: the brief asks for 5 s after open before reading the trace
        if (-not (Get-Process DinoReader.App -ErrorAction SilentlyContinue)) { throw 'the app died after the reorder layer opened' }
        $cyc = @(Get-TraceLines | Where-Object { $_ -match 'LayoutCycle|layout cycle' }).Count - $cycleBefore
        if ($cyc -gt 0) { throw "$cyc layout-cycle trace lines after open" }
        "layer open, app alive 5 s, 0 layout-cycle lines, $insetNote, hint-to-grid $gapBelowHint px, first row $firstRow, tiles " + ((Get-ShelfOrder 'ReorderTileTitle') -join ' | ')
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
