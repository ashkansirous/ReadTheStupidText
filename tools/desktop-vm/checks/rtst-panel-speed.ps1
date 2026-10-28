# A tray click opens the control panel and its 1.5x preset chip changes the speed pill to 1.5x.
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"
Invoke-Check -ResultFile $ResultFile -Body {
    if (-not (Get-Process -Name ReadTheStupidText.App -ErrorAction SilentlyContinue)) { throw 'the app is not running' }
    Set-TrayIconPromoted 'ReadTheStupidText.App.exe'
    $point = Wait-Until { Find-TrayIconCenter } 15 'tray icon not visible, cannot click it'
    Click-Screen $point.X $point.Y
    $panel = Wait-Until { Get-AppWindows 'ReadTheStupidText.App' | Where-Object { Find-UiaById $_ 'SpeedPillToggle' } | Select-Object -First 1 } 10 'the control panel did not open after the tray click'
    Invoke-Uia (Find-UiaById $panel 'SpeedPillToggle')
    $chip = Wait-Until { Find-UiaByName -Root $panel -Name '1.5' -ControlType Button } 5 'no 1.5x preset chip'
    Invoke-Uia $chip
    $pill = Find-UiaById $panel 'SpeedPill'
    Wait-Until { $pill.Current.Name -eq '1.5x' } 5 "speed pill shows '$($pill.Current.Name)', not 1.5x" | Out-Null
    "panel opened from the tray, speed pill reads $($pill.Current.Name)"
}
