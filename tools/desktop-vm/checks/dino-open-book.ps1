# Locks the shelf if it is unlocked and opens the first visible book on it (one of the two picked in dino-launch) by a cover tap into the reading page.
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"
Invoke-Check -ResultFile $ResultFile -Body {
    $window = { Get-AppWindows 'DinoReader.App' | Select-Object -First 1 }
    if (-not (& $window)) { throw 'Dino Reader is not running' }
    # Slice 418/420: there is one shelf and no "Ready for my child" button. A cover tap reads the book on the locked shelf, so lock it (a tap on the lock when the chip shows) and tap the cover.
    Enter-KidsShelf
    $tile = Wait-Until {
        $found = (& $window).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($e in $found) { if ($e.Current.AutomationId -eq 'StoryTile' -and -not $e.Current.BoundingRectangle.IsEmpty) { return $e } }
    } 15 'no visible story tile on the shelf'
    Invoke-Uia $tile
    Wait-Until { Find-UiaById (& $window) 'ReadingTransport' } 20 'the reading page did not open' | Out-Null
    $title = Find-UiaById (& $window) 'ReadingBack'
    $heading = (& $window).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition) |
        Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.Text' -and $_.Current.BoundingRectangle.Y -gt 40 -and $_.Current.BoundingRectangle.Y -lt 90 -and $_.Current.Name } | Select-Object -First 1
    "shelf book open on the reading page ($($heading.Current.Name))"
}
