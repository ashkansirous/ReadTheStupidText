# The startup task is declared in the package and the panel's launch-at-startup switch enables it (State 2 in the
# per-user package store).
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"
Invoke-Check -ResultFile $ResultFile -Body {
    $pkg = Get-InstalledPackage $PackageName
    $manifest = Get-Content (Join-Path $pkg.InstallLocation 'AppxManifest.xml') -Raw
    if ($manifest -notmatch 'TaskId="ReadTheStupidTextStartup"') { throw 'the installed manifest has no ReadTheStupidTextStartup task' }
    if (-not (Get-Process -Name ReadTheStupidText.App -ErrorAction SilentlyContinue)) { throw 'the app is not running' }
    $panel = Get-AppWindows 'ReadTheStupidText.App' | Where-Object { Find-UiaById $_ 'StartupToggle' } | Select-Object -First 1
    if (-not $panel) { throw 'the control panel is not open (run the panel check first)' }
    $toggle = Find-UiaById $panel 'StartupToggle'
    $state = $toggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if ($state.Current.ToggleState -eq 'Off') { Invoke-Uia $toggle }
    $key = "HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\$($pkg.PackageFamilyName)\ReadTheStupidTextStartup"
    $value = Wait-Until { (Get-ItemProperty $key -ErrorAction SilentlyContinue).State } 10 'Windows has no state for the startup task'
    if ($value -ne 2) { throw "startup task state is $value, expected 2 (enabled)" }
    'startup task declared, switch on, Windows state 2 (enabled)'
}
