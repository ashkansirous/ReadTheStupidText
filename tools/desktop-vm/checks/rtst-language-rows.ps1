# Slice 373 (Missing States mock RT2): the first-run language window lists its languages as templated rows (a ListItem per language named in
# itself, the icon tile, the name and the room for a second line), and an idle row shows no Retry button and no error line: those appear only
# on the chosen language's row after a failed download. The failed row itself needs a forced 404 that Windows has no hook for yet, so it is
# under the slice's "Not yet verified". It starts the app and stops it again at the end (window and process), so the next check starts clean:
# `.\Invoke-DesktopTest.ps1 -App rtst -Check rtst-language-rows`; it is first in the default RTST list in Invoke-DesktopTest.ps1.
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"
$ExpectedRows = 15

function Get-LanguageRows {
    param($Window)
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:AutomationElement::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    return @($Window.FindAll($script:TreeScope::Descendants, $cond))
}

Invoke-Check -ResultFile $ResultFile -Body {
    $pkg = Get-InstalledPackage $PackageName
    [NativeKeys]::Chord(0x1B)   # closes the Start menu the VM checkpoint was taken with
    Start-PackagedApp $pkg
    $window = Wait-Until { Get-AppWindows 'ReadTheStupidText.App' | Where-Object { $_.Current.Name -like '*Language*' } | Select-Object -First 1 } 30 'first-run language window did not open'
    $rows = Wait-Until { $r = Get-LanguageRows $window; if ($r.Count -ge $ExpectedRows) { $r } } 15 "the language list shows fewer than $ExpectedRows rows"
    if (-not (Find-UiaExact -Root $window -Name 'English' -ControlType 'ListItem')) { throw 'FAIL no row is named English (the row template lost the language name)' }
    if (-not (Find-UiaExact -Root $window -Name 'Deutsch' -ControlType 'ListItem')) { throw 'FAIL no row is named Deutsch' }
    if (Find-UiaVisibleById $window 'LanguageRowRetry') { throw 'FAIL a row shows Retry before any download failed' }
    if (Find-UiaVisibleById $window 'LanguageRowError') { throw 'FAIL a row shows an error line before any download failed' }
    $summary = "language window lists $(@($rows).Count) templated rows (English, Deutsch found); no Retry or error line on an idle row"
    Get-Process -Name ReadTheStupidText.App -ErrorAction SilentlyContinue | Stop-Process -Force   # the window and the app go, so the next check starts clean
    $summary
}
