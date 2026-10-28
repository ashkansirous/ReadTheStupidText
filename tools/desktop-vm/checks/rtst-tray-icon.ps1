# Tray icon present: the app is running, Windows registered its notification icon, and the icon is drawn on the taskbar.
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"
Invoke-Check -ResultFile $ResultFile -Body {
    if (-not (Get-Process -Name ReadTheStupidText.App -ErrorAction SilentlyContinue)) { throw 'the app is not running, so it has no tray icon' }
    Set-TrayIconPromoted 'ReadTheStupidText.App.exe'
    $point = Wait-Until { Find-TrayIconCenter } 15 'icon not visible in the taskbar tray'
    "tray icon registered and drawn at $($point.X),$($point.Y)"
}
