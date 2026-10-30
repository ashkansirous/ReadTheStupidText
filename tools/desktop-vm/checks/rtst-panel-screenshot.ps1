# Retakes the landing-page screenshot of the control panel (Slice 194): 1920x1080, the clipboard sample text read aloud,
# the panel opened from the tray, and only the panel window saved as PNG under <work dir>\results\control-panel.png.
# Opt-in (not in the default RTST check list): .\Invoke-DesktopTest.ps1 -App rtst -Check rtst-panel-screenshot -CopyOut <host folder>;
# the runner copies the PNG out before it restores the VM to clean (no -KeepRunning).
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"
function Assert-ScreenshotEnvironment {
    # The site image is English at 100% scale: throw when the guest is not, otherwise report both in the message.
    $dpi = (Get-ItemProperty 'HKCU:\Control Panel\Desktop\WindowMetrics' -ErrorAction SilentlyContinue).AppliedDPI
    if (-not $dpi) { $dpi = 96 }
    $culture = (Get-UICulture).Name
    if ($dpi -ne 96) { throw "the guest scale is $([int]($dpi / 96 * 100))%, the screenshot needs 100%" }
    if ($culture -notlike 'en*') { throw "the guest UI language is $culture, the screenshot needs English" }
    "scale 100%, UI language $culture"
}

function Assert-PanelOnScreen {
    # CopyFromScreen saves whatever is on top, so the panel must lie fully inside the screen and be the window under its centre.
    param($Panel)
    $r = $Panel.Current.BoundingRectangle
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    if ($r.Left -lt $screen.Left -or $r.Top -lt $screen.Top -or $r.Right -gt $screen.Right -or $r.Bottom -gt $screen.Bottom) { throw "the panel ($r) is not fully inside the screen ($screen)" }
    $top = [System.Windows.Automation.AutomationElement]::FromPoint((New-Object System.Windows.Point(($r.X + $r.Width / 2), ($r.Y + $r.Height / 2))))
    if (-not $top -or $top.Current.ProcessId -ne $Panel.Current.ProcessId) { throw 'another window covers the centre of the panel' }
}

Invoke-Check -ResultFile $ResultFile -Body {
    $pkg = Get-InstalledPackage $PackageName
    if (-not (Get-Process -Name ReadTheStupidText.App -ErrorAction SilentlyContinue)) { throw 'the app is not running' }
    Set-ScreenResolution 1920 1080
    $envInfo = Assert-ScreenshotEnvironment
    $sample = 'Read The Stupid Text reads whatever you select, out loud, at the speed you choose.'
    [System.Windows.Forms.Clipboard]::SetText($sample)
    Start-Sleep -Seconds 1
    [NativeKeys]::Chord(0x11, 0x5B, 0x52)
    $rows = Wait-Until { @((Get-RtstLogText $pkg) -split "`n" | Where-Object { $_ -match "`tHotkey`t" -and $_ -like "*$sample*" }) } 15 'the sample text never reached the input log'
    $states = ($rows | ForEach-Object { ($_ -split "`t")[3] }) -join ' > '
    Set-TrayIconPromoted 'ReadTheStupidText.App.exe'
    $point = Wait-Until { Find-TrayIconCenter } 15 'tray icon not visible, cannot click it'
    Click-Screen $point.X $point.Y
    $panel = Wait-Until { Get-AppWindows 'ReadTheStupidText.App' | Where-Object { Find-UiaById $_ 'SpeedPillToggle' } | Select-Object -First 1 } 10 'the control panel did not open after the tray click'
    Start-Sleep -Seconds 2
    $dir = Split-Path $ResultFile
    $png = Join-Path $dir 'control-panel.png'
    Assert-PanelOnScreen $panel
    Save-WindowScreenshot -Window $panel -Path $png
    "panel saved to $png (read states: $states; $envInfo)"
}
