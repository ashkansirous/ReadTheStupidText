# after: dino-onboarding-skip
# Slice 434: the Microsoft Store desktop shots of Dino Reader, and the landscape footage for the store trailer. Opt-in (not in the default Dino list). The VM is offline by design, so nothing here uses the network: all eight added books are imported from packages the host staged
# (fetch-shelf-packages.mjs <folder> --include-free), and the Store tab (shot 06) is the test store the DINO_TEST_BOOKS package compiles in (its four books carry their age ranges):
#   .\Invoke-DesktopTest.ps1 -App dino -Check dino-store-shots -WithShelfPackages <folder from fetch-shelf-packages.mjs --include-free> -CopyOut <host folder>
# 1. Sets the guest display to exactly 1920 x 1080 (Set-ScreenResolution; logs the ChangeDisplaySettings return code, and on a refusal the modes the adapter offers) at 100% scale. The VM's video adapter tops out at 1920 x 1200, so no larger mode exists.
# 2. Asks the app for the 12-book shelf (the full-shelf hook, through the request file the Release test package watches) and waits for its "HOOK full-shelf ok" line.
# 3. Puts the Dino window in the FullScreen presenter (the fullscreen hook, through fullscreen.request) so its CLIENT area is the whole 1920 x 1080 display, and captures that area only (never the desktop), printing every size; any other size throws.
# 4. Leaves store-<nn>-<name>.png (the shots) and footage-<clip>-<nnnn>.png (10 frames per second, the same 1920 x 1080 area) in the results folder; the runner's -CopyOut copies the shots and
#    encodes each clip's frames to <clip>.mp4 on the host one clip at a time (ffmpeg), deleting the frames.
# Shots: 01 book page, 02 reading with the highlighted line, 03 the recording screen, 04 the same page in English, Spanish and Japanese (store-04-lang-en|es|ja.png; the story-language hook through its
# request file), 05 the 12-book shelf, 06 the Store tab of the grown-ups' corner, where each book shows its "Ages x-y" (through the real gate). Clips: shelf, page, voices, record, languages.
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"

$TargetWidth = 1920
$TargetHeight = 1080
$TraceFileName = 'window-trace.log'
$RequestFileName = 'full-shelf.request'
$FullScreenRequestFileName = 'fullscreen.request'   # FullShelfRequest.FullScreenFileName on the app side
$LanguageRequestFileName = 'story-language.request'   # FullShelfRequest.LanguageFileName on the app side
$OpenBookRequestFileName = 'open-book.request'   # FullShelfRequest.OpenBookFileName on the app side
$ThreeLittlePigsId = '966400017c494fb5b045786f53852c9e'   # "id" in content/dino/three-little-pigs/book.json
$FootageFps = 10
$HookOk = 'HOOK full-shelf ok'
$HookFailed = 'HOOK full-shelf failed'
$HookFault = 'HOOK full-shelf fault'
$ShelfWaitSeconds = 300
$ShotTries = 10
$MinShotSpread = 8.0   # brightness standard deviation (0-255) under which a shot counts as blank
# What each story language must show on the reading page (the book's title, from ShippedTitles): English, Spanish, Japanese (the three kana ko-bu-ta, written as code points so this file stays ASCII).
$LanguageTitleFragments = @{ en = 'Three Little Pigs'; es = 'cerditos'; ja = (-join [char[]](0x3053, 0x3076, 0x305F)) }
$ResultsDir = Split-Path $ResultFile

function Get-DinoWindow {
    $w = Get-AppWindows 'DinoReader.App' | Select-Object -First 1
    if (-not $w) { throw 'Dino Reader is not running' }
    return $w
}

function Get-LocalState {
    param($Package)
    return (Join-Path $env:LOCALAPPDATA "Packages\$($Package.PackageFamilyName)\LocalState")
}

function Assert-StoreScale {
    # Run after the display mode switch: the app window's own DPI is what the capture sees (the logon-time registry value does not follow a mode switch).
    Initialize-NativeWindow
    $dpi = [NativeWindow]::Dpi([IntPtr](Get-DinoWindow).Current.NativeWindowHandle)
    if ($dpi -ne 96) { throw "FAIL the Dino window's scale is $([int]($dpi / 96 * 100))% (DPI $dpi) after the mode switch, the 1920 x 1080 capture needs 100% (the guest has no scale setter; the clean image is 100%)" }
    return 'scale 100%'
}

function Request-FullShelf {
    # Writes the request file, then waits for the hook's outcome line in window-trace.log (it imports eight packages from disk, no download).
    param($Package)
    $state = Get-LocalState $Package
    $trace = Join-Path $state $TraceFileName
    # Trace lines start with HH:mm:ss.fff (guest clock); the app trims the file to its last 200 lines, so a line count would hide new lines. Only outcome lines stamped after the request count.
    $sentAt = (Get-Date).TimeOfDay
    Set-Content -Path (Join-Path $state $RequestFileName) -Value 'full-shelf'
    $line = Wait-Until {
        if (-not (Test-Path $trace)) { return $null }
        @(Get-Content $trace) | Where-Object { $_ -match '^(\d\d):(\d\d):(\d\d)\.(\d+) HOOK full-shelf (ok|failed|fault)' -and ([TimeSpan]::new(0, [int]$Matches[1], [int]$Matches[2], [int]$Matches[3], [int]$Matches[4].PadRight(3, '0').Substring(0, 3)) -ge $sentAt) } | Select-Object -Last 1
    } $ShelfWaitSeconds 'the full-shelf hook wrote no outcome line (is the package a -p:DinoTestBooks=true build, and were the eight packages staged with -WithShelfPackages?)'
    if ($line -match [regex]::Escape($HookFailed) -or $line -match [regex]::Escape($HookFault)) { throw "FAIL $line" }
    return ($line -replace '^.*(HOOK full-shelf)', '$1')
}

function Set-GuestDisplay {
    # Slice 434 redesign: the guest display is set to exactly 1920 x 1080 (the VM's video adapter tops out at 1920 x 1200, so no larger mode exists to hold a framed window). Logs the ChangeDisplaySettings return code;
    # on a refusal it also lists the modes EnumDisplaySettings offers, then throws. Returns the note for the result line.
    try { $code = Set-ScreenResolution $TargetWidth $TargetHeight } catch { throw "FAIL $($_.Exception.Message) Modes the adapter offers: $(Get-DisplayModes)" }
    Write-Host "DISPLAY ${TargetWidth}x${TargetHeight} set (ChangeDisplaySettings returned $code)"
    return "display ${TargetWidth}x${TargetHeight} (ChangeDisplaySettings returned $code)"
}

function Get-DisplayModes {
    # Every distinct width x height EnumDisplaySettings lists for the primary display.
    Initialize-NativeDisplay
    $seen = @{}
    $dm = New-Object NativeDisplay+DEVMODE
    $dm.dmSize = [int16][System.Runtime.InteropServices.Marshal]::SizeOf([type]'NativeDisplay+DEVMODE')
    for ($i = 0; [NativeDisplay]::EnumDisplaySettings($null, $i, [ref]$dm) -ne 0; $i++) { $seen["$($dm.dmPelsWidth)x$($dm.dmPelsHeight)"] = $true }
    return (@($seen.Keys | Sort-Object) -join ', ')
}

function Set-StoreWindow {
    # Slice 434 redesign: the full-screen hook (fullscreen.request, same pattern as full-shelf) puts the Dino window in the FullScreen presenter, so its client area is the whole 1920 x 1080 display with no frame or
    # title bar; waits for the hook's outcome line and checks the client area. Returns "<w>x<h> at <x>,<y>".
    param($Package)
    Initialize-NativeWindow
    $state = Get-LocalState $Package
    $trace = Join-Path $state $TraceFileName
    $sentAt = (Get-Date).TimeOfDay   # the app trims the trace to its last lines, so only lines stamped after the request count
    Set-Content -Path (Join-Path $state $FullScreenRequestFileName) -Value 'fullscreen'
    $line = Wait-Until {
        if (-not (Test-Path $trace)) { return $null }
        @(Get-Content $trace) | Where-Object { $_ -match '^(\d\d):(\d\d):(\d\d)\.(\d+) HOOK fullscreen (ok|failed)' -and ([TimeSpan]::new(0, [int]$Matches[1], [int]$Matches[2], [int]$Matches[3], [int]$Matches[4].PadRight(3, '0').Substring(0, 3)) -ge $sentAt) } | Select-Object -Last 1
    } 30 'the fullscreen hook wrote no outcome line (is the package a -p:DinoTestBooks=true build?)'
    if ($line -match 'HOOK fullscreen failed') { throw "FAIL $line" }
    if ($line -notmatch "HOOK fullscreen ok ${TargetWidth}x${TargetHeight} zoom 1\.5\s*$") { throw "FAIL the fullscreen hook did not report 'ok ${TargetWidth}x${TargetHeight} zoom 1.5': $line" }   # the 150% look: root laid out at 1280 x 720, drawn 1.5x
    $handle = [IntPtr](Get-DinoWindow).Current.NativeWindowHandle
    [NativeKeys]::SetForegroundWindow($handle) | Out-Null
    $c = Wait-Until { $now = [NativeWindow]::Client($handle); if ($now[2] -eq $TargetWidth -and $now[3] -eq $TargetHeight) { return , $now } } 15 "FAIL the window client area is not ${TargetWidth}x${TargetHeight} after the fullscreen hook ($(([NativeWindow]::Client($handle)) -join ','))"
    return "$($c[2])x$($c[3]) at $($c[0]),$($c[1]) ($($line -replace '^.*(HOOK fullscreen)', '$1'))"
}

function Get-ShotSpread {
    # The standard deviation of the brightness of a PNG, sampled on a 24 px grid (3600 points): a flat or blank frame is near 0, a drawn page or shelf is far above $MinShotSpread.
    param([string] $Path)
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap $Path
    try {
        $sum = 0.0; $sumSq = 0.0; $n = 0
        for ($y = 0; $y -lt $bmp.Height; $y += 24) {
            for ($x = 0; $x -lt $bmp.Width; $x += 24) {
                $p = $bmp.GetPixel($x, $y)
                $l = 0.299 * $p.R + 0.587 * $p.G + 0.114 * $p.B
                $sum += $l; $sumSq += $l * $l; $n++
            }
        }
        $mean = $sum / $n
        return [math]::Sqrt([math]::Max(0, $sumSq / $n - $mean * $mean))
    } finally { $bmp.Dispose() }
}

function Test-InertPoint {
    # True when the UIA element at the screen point, and every ancestor, can neither be pressed nor selected nor edited, so a click there cannot do anything.
    param([int] $X, [int] $Y)
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $active = @([System.Windows.Automation.InvokePattern]::Pattern, [System.Windows.Automation.SelectionItemPattern]::Pattern, [System.Windows.Automation.TogglePattern]::Pattern,
        [System.Windows.Automation.ExpandCollapsePattern]::Pattern, [System.Windows.Automation.ValuePattern]::Pattern)
    $node = [System.Windows.Automation.AutomationElement]::FromPoint((New-Object System.Windows.Point($X, $Y)))
    $window = (Get-DinoWindow).Current.NativeWindowHandle
    while ($node -and $node.Current.NativeWindowHandle -ne $window) {
        if (@($node.GetSupportedPatterns() | Where-Object { $active -contains $_ }).Count) { return $false }
        $node = $walker.GetParent($node)
    }
    return [bool]$node
}

function Clear-FocusVisual {
    # Shots 03 and 06 showed a keyboard focus ring (the Store tab, the Record button): the keys that got there left focus on a control. WinUI draws that ring only for keyboard focus (FocusState.Keyboard),
    # so a real mouse click on a spot where nothing can be pressed (the app stays as it is, nothing is hidden in it) makes the last input a pointer's and the ring goes. Returns a note naming what happened.
    $handle = [IntPtr](Get-DinoWindow).Current.NativeWindowHandle
    $c = [NativeWindow]::Client($handle)
    foreach ($fy in 0.96, 0.9, 0.5, 0.1) {
        foreach ($fx in 0.99, 0.01, 0.5, 0.9) {
            $x = [int]($c[0] + $c[2] * $fx); $y = [int]($c[1] + $c[3] * $fy)
            if (-not (Test-InertPoint $x $y)) { continue }
            Move-MouseTo $x $y
            Start-Sleep -Milliseconds 300   # fixed-wait: the pointer move lands before the press
            [Win32.Mouse]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); [Win32.Mouse]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 500   # fixed-wait: the ring is redrawn without
            return "focus cleared by a click at $x,$y"
        }
    }
    return 'focus not cleared: no inert spot found'
}

function Save-Shot {
    # Captures the client area; a near-uniform frame (the page had not drawn yet) is retaken once a second for up to $ShotTries tries, and a frame still blank after that fails the check, so a blank shot can never pass.
    param([string] $Name)
    Write-Host "SHOT $Name $(Clear-FocusVisual)"
    $path = Join-Path $ResultsDir $Name
    for ($try = 1; $try -le $ShotTries; $try++) {
        $size = Save-ClientScreenshot -Window (Get-DinoWindow) -Path $path -Width $TargetWidth -Height $TargetHeight
        $spread = Get-ShotSpread $path
        if ($spread -ge $MinShotSpread) {
            Write-Host "SHOT $Name $size spread $([int]$spread) (try $try)"
            return "$Name $size"
        }
        Write-Host "SHOT $Name is near-uniform (spread $([math]::Round($spread, 1)), try $try of $ShotTries), retaking"
        Start-Sleep -Seconds 1   # fixed-wait: the page draws
    }
    throw "FAIL $Name is a blank or near-uniform frame (brightness spread $([math]::Round($spread, 1)) is under $MinShotSpread after $ShotTries tries): the page had not rendered"
}

function Wait-VisibleText {
    # Waits for a visible UIA element whose Name contains $Fragment (the page's title in the language shown); on a timeout the failure names what was on screen.
    param([string] $Fragment, [string] $What)
    try {
        Wait-Until {
            $all = (Get-DinoWindow).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
            foreach ($e in $all) { if ($e.Current.Name -and $e.Current.Name.Contains($Fragment) -and -not $e.Current.BoundingRectangle.IsEmpty) { return $e } }
        } 25 "FAIL no visible text with '$Fragment' ($What)" | Out-Null
    } catch { throw "$($_.Exception.Message). On screen: $((Get-VisibleNames (Get-DinoWindow) 30) -join ' / ')" }
}

function Save-Clip {
    # Captures $Seconds of the same area at $FootageFps frames per second as footage-<clip>-<nnnn>.png; $Actions maps a second (as a number) to the scriptblock to run at that moment.
    # Frames the capture was too slow for repeat the previous one, so the clip keeps its real length. Returns "<clip> <frames> frames".
    param([string] $Clip, [double] $Seconds, [hashtable] $Actions = @{})
    $total = [int]($Seconds * $FootageFps)
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $due = @($Actions.Keys | Sort-Object)
    $last = $null
    $slow = 0
    for ($i = 1; $i -le $total; $i++) {
        while ($due.Count -gt 0 -and $watch.Elapsed.TotalSeconds -ge $due[0]) { & $Actions[$due[0]]; $due = @($due | Select-Object -Skip 1) }
        $path = Join-Path $ResultsDir ('footage-{0}-{1:0000}.png' -f $Clip, $i)
        if ($last -and $watch.Elapsed.TotalSeconds -gt $i / $FootageFps) { Copy-Item $last $path; $slow++ }
        else {
            while ($watch.Elapsed.TotalSeconds -lt ($i - 1) / $FootageFps) { Start-Sleep -Milliseconds 5 }   # fixed-wait: pacing the frame clock, 5 ms
            Save-ClientScreenshot -Window (Get-DinoWindow) -Path $path -Width $TargetWidth -Height $TargetHeight | Out-Null
            $last = $path
        }
    }
    return "$Clip $total frames ($slow repeated for speed)"
}

function Open-FirstBook {
    # Opens The Three Little Pigs on the reading page through the open-book hook (its request file holds the story id), never through the shelf tiles: the zoomed window shows one shelf row, so the titles of the second read empty.
    # Waits for the hook's outcome line, then for the visible title in $Language on the reading page (the language shots reopen the book in es and ja: run 8 log).
    param($Package, [string] $Language = 'en')
    $window = { Get-DinoWindow }
    Enter-KidsShelf
    $state = Get-LocalState $Package
    $trace = Join-Path $state $TraceFileName
    $sentAt = (Get-Date).TimeOfDay   # only lines stamped after the request count (the app trims the trace, so a line count would hide new lines)
    Set-Content -Path (Join-Path $state $OpenBookRequestFileName) -Value $ThreeLittlePigsId
    $line = Wait-Until {
        if (-not (Test-Path $trace)) { return $null }
        @(Get-Content $trace) | Where-Object { $_ -match '^(\d\d):(\d\d):(\d\d)\.(\d+) HOOK open-book' -and ([TimeSpan]::new(0, [int]$Matches[1], [int]$Matches[2], [int]$Matches[3], [int]$Matches[4].PadRight(3, '0').Substring(0, 3)) -ge $sentAt) } | Select-Object -Last 1
    } 40 'the open-book hook wrote no line'
    Write-Host "TRACE $line"
    # The request names the package id (book.json); the hook answers with the runtime story id, which differs (run 6 log), so only "ok" is required here.
    if ($line -notmatch 'HOOK open-book ok ') { throw "FAIL $line" }
    Wait-Until { Find-UiaById (& $window) 'ReadingTransport' } 20 'the reading page did not open' | Out-Null
    # ReadingSurface (a Border subclass) is not exposed to UI Automation (run 7 log); the visible title is, and the other Dino checks wait the same way.
    Wait-VisibleText $LanguageTitleFragments[$Language] "the reading page of The Three Little Pigs in $Language after open-book"
}

function Close-ReadingPage {
    $back = Find-UiaById (Get-DinoWindow) 'ReadingBack'
    if ($back) { Invoke-Uia $back }
    Wait-Until { Find-UiaVisibleById (Get-DinoWindow) 'GalleryTitle' } 20 'the shelf did not come back from the reading page' | Out-Null
}

function Set-StoryLanguage {
    # The story-language hook through its request file; waits for its outcome line in window-trace.log.
    param($Package, [string] $Code)
    $state = Get-LocalState $Package
    $trace = Join-Path $state $TraceFileName
    $sentAt = (Get-Date).TimeOfDay   # the app trims the trace to its last lines, so a line count would hide new lines: only lines stamped after the request count
    Set-Content -Path (Join-Path $state $LanguageRequestFileName) -Value $Code
    $line = Wait-Until {
        if (-not (Test-Path $trace)) { return $null }
        @(Get-Content $trace) | Where-Object { $_ -match '^(\d\d):(\d\d):(\d\d)\.(\d+) HOOK story-language' -and ([TimeSpan]::new(0, [int]$Matches[1], [int]$Matches[2], [int]$Matches[3], [int]$Matches[4].PadRight(3, '0').Substring(0, 3)) -ge $sentAt) } | Select-Object -Last 1
    } 20 "the story-language hook wrote no line for $Code"
    Write-Host "TRACE $line"
    if ($line -notmatch "HOOK story-language ok $Code") { throw "FAIL $line" }
}

function Save-LanguageShots {
    # Shot 04: the same book and page in English, Spanish and Japanese (one still each, store-04-lang-<code>.png), and the 'languages' clip from them: 10 frames (1 s) of each still.
    param($Package)
    $notes = @()
    $stills = @()
    foreach ($code in @('en', 'es', 'ja')) {
        Close-ReadingPage
        Set-StoryLanguage $Package $code
        Open-FirstBook $Package $code
        Wait-VisibleText $LanguageTitleFragments[$code] "the reading page in $code (story-language hook outcome ok, then the book reopened)"
        Start-Sleep -Seconds 2   # fixed-wait: the page text settles in the picked language
        $name = "store-04-lang-$code.png"
        $notes += Save-Shot $name
        $stills += (Join-Path $ResultsDir $name)
    }
    $frame = 0
    foreach ($still in $stills) {
        for ($i = 0; $i -lt $FootageFps; $i++) { $frame++; Copy-Item $still (Join-Path $ResultsDir ('footage-languages-{0:0000}.png' -f $frame)) }
    }
    Close-ReadingPage
    Set-StoryLanguage $Package 'en'
    return $notes + "languages $frame frames"
}

function Save-RecordShot {
    # Shot 03 and the 'record' clip: the real recording screen (the shelf is unlocked): the first book's dots button, the sheet's "Record your voice", the intro's Start, then the screen's RecordButton must be visible
    # (never started, so no microphone prompt). Then back out to the shelf with Esc.
    $dots = Wait-Until { Find-FirstTileMore (Get-DinoWindow) } 15 'FAIL no dots button (TileMore) on a book of the unlocked shelf'
    Invoke-Uia $dots
    Wait-Until { Find-UiaVisibleById (Get-DinoWindow) 'BookSheetClose' } 10 'FAIL the book sheet did not open' | Out-Null
    Invoke-Uia (Wait-Until { Find-UiaByName -Root (Get-DinoWindow) -Name 'Record your voice' } 10 'FAIL no "Record your voice" on the book sheet')
    $start = Wait-Until { $b = Find-UiaVisibleById (Get-DinoWindow) 'RecordIntroStart'; if ($b -and $b.Current.IsEnabled) { $b } } 15 'FAIL the Record intro showed no enabled Start button'
    Invoke-Uia $start
    # The first invoke of Start can be lost (dino-record.ps1 Start-RecordingFromIntro retries for the same reason; run 9 log): invoke once more while Start is still showing.
    $shown = { Find-UiaVisibleById (Get-DinoWindow) 'RecordButton' }
    try { Wait-Until $shown 15 'RecordButton after the first Start' | Out-Null }
    catch {
        # The intro may already be closing (run 11: the second invoke hit a Start with no bounds and threw): a failed retry only means the first invoke was taken.
        $again = Find-UiaVisibleById (Get-DinoWindow) 'RecordIntroStart'
        if ($again) { try { Invoke-Uia $again } catch { Write-Host "NOTE second Start not invoked: $($_.Exception.Message)" } }
        try { Wait-Until $shown 25 'FAIL the recording screen did not show (no visible RecordButton)' | Out-Null }
        catch { throw "$($_.Exception.Message). On screen: $((Get-VisibleNames (Get-DinoWindow) 30) -join ' / ')" }
    }
    Start-Sleep -Seconds 1   # fixed-wait: the recording screen settles
    $notes = @(Save-Shot 'store-03-record.png')
    $notes += Save-Clip 'record' 4
    $covering = { foreach ($id in 'RecordButton', 'RecordIntroStart', 'BookSheetClose') { if (Find-UiaVisibleById (Get-DinoWindow) $id) { return $true } }; return $false }
    for ($i = 0; $i -lt 6 -and (& $covering); $i++) {
        [NativeKeys]::Chord(0x1B)   # Esc steps back: recording screen, intro, sheet
        Start-Sleep -Seconds 1   # fixed-wait: the page closes
    }
    if (& $covering) { throw 'FAIL Esc did not bring the shelf back from the recording screen' }
    return $notes
}

function Unlock-Shelf {
    # The real gate (hold, then the sum), never a bypass; leaves the shelf unlocked.
    $HoldRingName = 'Press and hold for 3 seconds'
    Enter-KidsShelf
    Invoke-Uia (Wait-Until { Find-UiaVisibleById (Get-DinoWindow) 'GalleryLock' } 15 'FAIL the lock is not on the shelf')
    $answer = [string](Invoke-GateHold -FindRing { Wait-Until { Find-UiaExact (Get-DinoWindow) $HoldRingName } 15 'FAIL the gate did not open' } -PackageName $PackageName -HoldMs 3500)
    $key = Find-UiaExact (Get-DinoWindow) $answer 'Button'
    if (-not $key) { throw "FAIL no answer key $answer on the gate" }
    Invoke-Uia $key
    Wait-Until { Find-UiaVisibleById (Get-DinoWindow) 'LockChip' } 15 'FAIL the shelf did not unlock after the gate' | Out-Null
}

function Save-AgesShot {
    # Shot 06: the grown-ups' corner Store tab, each book showing its age range (the shelf is unlocked).
    Invoke-Uia (Wait-Until { Find-UiaVisibleById (Get-DinoWindow) 'GalleryStore' } 10 'FAIL no Store tab in the rail')
    Start-Sleep -Seconds 3   # fixed-wait: the store list loads and its covers draw
    return Save-Shot 'store-06-ages.png'
}

Invoke-Check -ResultFile $ResultFile -Body {
    $pkg = Get-InstalledPackage $PackageName
    $notes = @()
    $notes += Set-GuestDisplay
    $notes += Assert-StoreScale
    $notes += Request-FullShelf $pkg
    $notes += 'window ' + (Set-StoreWindow $pkg)
    Wait-Until { Find-UiaVisibleById (Get-DinoWindow) 'GalleryTitle' } 20 'the shelf is not showing' | Out-Null
    $notes += Save-Clip 'shelf' 3 @{ 0.5 = { [NativeKeys]::Chord(0x27) }; 1.2 = { [NativeKeys]::Chord(0x27) }; 1.9 = { [NativeKeys]::Chord(0x27) }; 2.5 = { [NativeKeys]::Chord(0x27) } }   # Right arrows along the shelf
    $notes += Save-Shot 'store-05-shelf.png'
    Open-FirstBook $pkg
    $notes += Save-Shot 'store-01-page.png'
    $notes += Save-Clip 'page' 3 @{ 1.0 = { [NativeKeys]::Chord(0x27) } }   # Right turns the page
    [NativeKeys]::Chord(0x20)   # Space plays
    Start-Sleep -Seconds 2   # fixed-wait: the first words are read and the line is lit
    $notes += Save-Shot 'store-02-reading.png'
    $notes += Save-Clip 'voices' 3
    [NativeKeys]::Chord(0x20)   # Space pauses
    $notes += Save-LanguageShots $pkg   # leaves the reading page first
    Unlock-Shelf
    $notes += Save-RecordShot
    $notes += Save-AgesShot
    $notes -join '; '
}
