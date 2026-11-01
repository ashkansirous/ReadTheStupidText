# On the reading page, "Next sentence" advances through the page and turns it: the story text beside the picture must change.
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"
Invoke-Check -ResultFile $ResultFile -Body {
    $window = { Get-AppWindows 'DinoReader.App' | Select-Object -First 1 }
    if (-not (& $window)) { throw 'Dino Reader is not running' }
    $pageText = {
        $root = & $window
        if (-not $root) { throw 'Dino Reader window is gone' }
        $cond = New-Object System.Windows.Automation.PropertyCondition($script:AutomationElement::AutomationIdProperty, 'StoryText')
        $labels = $root.FindAll($script:TreeScope::Descendants, $cond)
        $names = @(foreach ($label in $labels) { try { $label.Current.Name } catch { } })
        # Slice 225 item 24 (Slice 226 item 5): a picture+text page's accessible name has no line break in it (PictureTextPages.AccessibleName joins the words with single spaces).
        # The check fails on a build whose name keeps CR or LF, and passes on main. No CR or LF escape sequence is written here: a Write-tool slip once turned one into a raw CR byte.
        $withBreak = @($names | Where-Object { $_ -and ($_.Contains([string][char]10) -or $_.Contains([string][char]13)) })
        if ($withBreak.Count -gt 0) {
            $shown = ($withBreak[0] -replace '\s+', '|')
            throw "FAIL: $($withBreak.Count) StoryText UIA name(s) contain a CR or LF (the accessible name must be one line, PictureTextPages.AccessibleName): '$($shown.Substring(0, [Math]::Min(60, $shown.Length)))...'"
        }
        return (($names | Where-Object { $_ }) -join ' ')
    }
    # Slice 205: the story label carries the page's text as its accessible description (a FormattedText label can have an empty UIA
    # name). If it is still empty, the failure lists what the page does expose instead of guessing.
    $dumpPage = { 'UI: ' + ((Get-VisibleNames (& $window) 60) -join ' / ') }
    Wait-Until { Find-UiaById (& $window) 'ReadingTransport' } 10 'not on the reading page (run the open-book check first)' | Out-Null
    # Text-in-picture books (the starter "Good Enough") open in Picture only, where no StoryText is shown. The mode is saved per book
    # (story.json PreferredMode), so switch to "Picture + text" for the check and put the original mode back at the end.
    $modeNames = 'Picture only', 'Picture + text', 'Text only'
    $selectedMode = {
        foreach ($m in $modeNames) {
            $cell = Find-UiaByName -Root (& $window) -Name $m
            if ($cell -and $cell.Current.Name -match 'Selected') { return $m }
        }
        return $null
    }
    $setMode = {
        param([string] $Mode)
        if ((& $selectedMode) -eq $Mode) { return }
        Invoke-Uia (Find-UiaByName -Root (& $window) -Name $Mode)
        Wait-Until { (& $selectedMode) -eq $Mode } 10 "the '$Mode' mode never reported Selected" | Out-Null
    }
    $originalMode = & $selectedMode
    & $setMode 'Picture + text'
    $turnPage = {
        $before = & $pageText
        if (-not $before) { throw ('FAIL: the reading page shows no story text (no element with AutomationId StoryText has a name). ' + (& $dumpPage)) }
        # The label's name is the page's text, so it changes only when the page turns, not on each highlighted sentence: allow a long page.
        $maxTaps = 120
        for ($i = 1; $i -le $maxTaps; $i++) {
            Invoke-Uia (Find-UiaByName -Root (& $window) -Name 'Next sentence')
            Start-Sleep -Milliseconds 1200
            $after = & $pageText
            if (-not $after) { throw "FAIL: after tap $i the StoryText label has no name (the page text is no longer exposed). $(& $dumpPage)" }
            if ($after -ne $before) { return "page turned after $i taps: '$($before.Substring(0, [Math]::Min(30, $before.Length)))...' -> '$($after.Substring(0, [Math]::Min(30, $after.Length)))...'" }
        }
        throw "FAIL: the StoryText name still equals the first page's text after $maxTaps taps on Next sentence: the page never turned, or the label's name does not follow the page. $(& $dumpPage)"
    }
    try { return (& $turnPage) }
    finally { if ($originalMode) { try { & $setMode $originalMode } catch { } } }
}
