# The window itself (Slice 235 part A): it cannot be made smaller than 800 x 560, and a resized and moved window comes back at the same size and place
# after it is closed and opened again. Runs last: it restores the window from whatever state the earlier checks left, resizes it, closes the app and starts it again.
# The VM runs at 100% scaling, so UIA's pixels are the app's dp. Tolerance covers the window frame (a few px).
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"

$MinWidth = 800
$MinHeight = 560
$TargetWidth = 1000
$TargetHeight = 650
$TargetX = 60
$TargetY = 40
$Tolerance = 12

function Get-DinoWindow {
    $w = Get-AppWindows 'DinoReader.App' | Select-Object -First 1
    if (-not $w) { throw 'Dino Reader is not running' }
    return $w
}

function Get-Bounds {
    $r = (Get-DinoWindow).Current.BoundingRectangle
    return @{ X = [int]$r.X; Y = [int]$r.Y; W = [int]$r.Width; H = [int]$r.Height }
}

function Close-DinoWindow {
    $win = Get-DinoWindow
    $win.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Wait-Until { -not (Get-Process -Name 'DinoReader.App' -ErrorAction SilentlyContinue) } 30 'Dino Reader did not exit after the window was closed' | Out-Null
}

$SmallWidth = 900
$SmallHeight = 600
$MaximisedOverhang = 16   # a maximised frame is 8 px larger than the work area on each side
$OpenWidth = 1100
$OpenHeight = 760
$TraceFileName = 'window-trace.log'
$BaseDpi = 96

function Get-DisplayScale {
    # The VM normally runs at 100% (AppliedDPI absent); a scaled display scales the opening size too.
    $dpi = (Get-ItemProperty -Path 'HKCU:\Control Panel\Desktop\WindowMetrics' -Name AppliedDPI -ErrorAction SilentlyContinue).AppliedDPI
    if (-not $dpi) { return 1.0 }
    return [double]$dpi / $BaseDpi
}

function Get-WindowTrace {
    # The app writes the trace into its LocalState folder (packaged FileSystem.AppDataDirectory).
    param($Package)
    $path = Join-Path $env:LOCALAPPDATA "Packages\$($Package.PackageFamilyName)\LocalState\$TraceFileName"
    if (-not (Test-Path $path)) { return "(no $TraceFileName at ${path}: the frame never traced anything)" }
    return (Get-Content -Path $path -Raw)
}

function Get-WorkArea {
    Add-Type -AssemblyName System.Windows.Forms
    $a = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    return @{ W = [int]$a.Width; H = [int]$a.Height }
}

function Assert-OpeningSize {
    # The app clamps its default size to the work area; a clamped window that fills the work area is maximised by Windows,
    # and a maximised frame overhangs the work area by the invisible border on each side.
    $scale = Get-DisplayScale
    $work = Get-WorkArea
    $wantW = [math]::Min([int][math]::Round($OpenWidth * $scale), $work.W)
    $wantH = [math]::Min([int][math]::Round($OpenHeight * $scale), $work.H)
    $fillsW = $wantW -ge ($work.W - $Tolerance)
    $fillsH = $wantH -ge ($work.H - $Tolerance)
    $b = Get-Bounds
    $maxW = $wantW + $Tolerance + $(if ($fillsW) { $MaximisedOverhang } else { 0 })
    $maxH = $wantH + $Tolerance + $(if ($fillsH) { $MaximisedOverhang } else { 0 })
    if ($b.W -lt ($wantW - $Tolerance) -or $b.W -gt $maxW -or $b.H -lt ($wantH - $Tolerance) -or $b.H -gt $maxH) {
        throw "the window opened at $($b.W) x $($b.H), not about $wantW x $wantH (work area $($work.W) x $($work.H))"
    }
}

function Set-SmallNormalWindow {
    # Leave the window Normal and smaller than the work area, so the later resize steps mean something on a small screen.
    $work = Get-WorkArea
    $w = [math]::Min($SmallWidth, $work.W - 40)
    $h = [math]::Min($SmallHeight, $work.H - 40)
    $win = Get-DinoWindow
    $win.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
    Start-Sleep -Seconds 1
    (Get-DinoWindow).GetCurrentPattern([System.Windows.Automation.TransformPattern]::Pattern).Resize($w, $h)
    Start-Sleep -Seconds 1
    $b = Get-Bounds
    if ($b.W -ge $work.W -or $b.H -ge $work.H) { throw "the window is $($b.W) x $($b.H), not smaller than the work area $($work.W) x $($work.H), after asking for $w x $h" }
}

function Get-FittingTarget {
    # The target size, shrunk to fit the work area but never below the minimum.
    $work = Get-WorkArea
    $w = [math]::Max($MinWidth, [math]::Min($TargetWidth, $work.W - $TargetX - 20))
    $h = [math]::Max($MinHeight, [math]::Min($TargetHeight, $work.H - $TargetY - 20))
    return @{ W = $w; H = $h }
}

function Test-DinoWindow {
    param($pkg)
    Assert-OpeningSize
    Set-SmallNormalWindow

    $transform = (Get-DinoWindow).GetCurrentPattern([System.Windows.Automation.TransformPattern]::Pattern)
    $transform.Resize(400, 300)   # far below the minimum: Windows must stop at 800 x 560
    Start-Sleep -Seconds 1
    $small = Get-Bounds
    if ($small.W -lt ($MinWidth - $Tolerance) -or $small.H -lt ($MinHeight - $Tolerance)) { throw "the window shrank to $($small.W) x $($small.H), below the $MinWidth x $MinHeight minimum" }

    $transform.Move($TargetX, $TargetY)
    $target = Get-FittingTarget
    $transform.Resize($target.W, $target.H)
    Start-Sleep -Seconds 2   # the app saves 500 ms after the window settles
    $before = Get-Bounds

    Close-DinoWindow
    Start-PackagedApp $pkg
    Wait-Until { Get-AppWindows 'DinoReader.App' | Select-Object -First 1 } 30 'Dino Reader window did not open again' | Out-Null
    Start-Sleep -Seconds 2
    $after = Get-Bounds
    foreach ($axis in 'X', 'Y', 'W', 'H') {
        if ([math]::Abs($after[$axis] - $before[$axis]) -gt $Tolerance) { throw "after reopening, $axis is $($after[$axis]) but it was $($before[$axis]) when closed" }
    }
    "minimum held ($($small.W) x $($small.H) after asking for 400 x 300); reopened at $($after.W) x $($after.H) at $($after.X),$($after.Y), as closed"
}

Invoke-Check -ResultFile $ResultFile -Body {
    $pkg = Get-InstalledPackage $PackageName
    try {
        Test-DinoWindow $pkg
    } catch {
        $trace = Get-WindowTrace $pkg
        throw "$($_.Exception.Message) -- window-trace.log: $trace"
    }
}
