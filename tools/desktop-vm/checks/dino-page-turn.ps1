# On the reading page, "Next sentence" advances through the page and turns it: the story text beside the picture must change.
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"
Invoke-Check -ResultFile $ResultFile -Body {
    $window = { Get-AppWindows 'DinoReader.App' | Select-Object -First 1 }
    if (-not (& $window)) { throw 'Dino Reader is not running' }
    $pageText = {
        $surface = Find-UiaById (& $window) 'ReadingSurface'
        if (-not $surface) { return '' }
        $names = $surface.FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition) |
            Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.Text' } |
            ForEach-Object { $_.Current.Name }
        return ($names -join ' ')
    }
    Wait-Until { Find-UiaById (& $window) 'ReadingTransport' } 10 'not on the reading page (run the open-book check first)' | Out-Null
    $before = & $pageText
    if (-not $before) { throw 'the reading page shows no story text' }
    for ($i = 1; $i -le 10; $i++) {
        Invoke-Uia (Find-UiaByName -Root (& $window) -Name 'Next sentence')
        Start-Sleep -Milliseconds 1200
        $after = & $pageText
        if ($after -and $after -ne $before) { return "page turned after $i taps: '$($before.Substring(0, [Math]::Min(30, $before.Length)))...' -> '$($after.Substring(0, [Math]::Min(30, $after.Length)))...'" }
    }
    throw 'the story text never changed after 10 taps on Next sentence'
}
