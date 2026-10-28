# Notepad selection + Ctrl+Win+R: the Hotkey row must hold the SELECTED text, not the clipboard decoy (the selection path;
# rtst-hotkey-read covers the clipboard path). Slice 175.
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"
function Close-NotepadWelcome {
    # A fresh profile (the clean checkpoint) shows "Notepad automatically saves your progress" with a "Got it" button over the text box; UIA does not see the box until it is closed.
    param($Window)
    $name = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Got it')
    $button = $Window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.AndCondition($name,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))))
    if ($button) { $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 800 }
}

function Get-NotepadTreeSummary {
    # What UI Automation shows of Notepad when the text box is missing, so a changed Notepad is diagnosable from the result.
    $wins = @(Get-AppWindows 'notepad')
    $parts = foreach ($w in $wins) {
        $kids = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) | Select-Object -First 25 |
            ForEach-Object { "$($_.Current.ControlType.ProgrammaticName -replace 'ControlType\.','')/$($_.Current.ClassName)/$($_.Current.Name)" }
        "window '$($w.Current.Name)' [$($kids -join '; ')]"
    }
    "notepad windows: $($wins.Count) " + ($parts -join ' ')
}
Invoke-Check -ResultFile $ResultFile -Body {
    if (-not (Get-Process -Name ReadTheStupidText.App -ErrorAction SilentlyContinue)) { throw 'the app is not running' }
    $pkg = Get-InstalledPackage $PackageName
    $tag = ([guid]::NewGuid().ToString('N').Substring(0, 6) -replace '[0-9]', 'q')
    $selText = "selected sentence $tag from notepad"
    $decoy = "clipboard decoy $tag"
    $file = Join-Path $env:TEMP 'rtst-selection-check.txt'
    Set-Content $file $selText -Encoding ASCII
    Start-Process notepad.exe $file
    try {
        $null = Wait-Until { Get-AppWindows 'notepad' | Select-Object -First 1 } 30 'notepad window did not open'
        $ae = [System.Windows.Automation.AutomationElement]
        $ct = [System.Windows.Automation.ControlType]
        $scope = [System.Windows.Automation.TreeScope]::Descendants
        # Notepad loads its text box a moment after the window shows (and the window can be replaced once by the packaged app): look again until it is there.
        try { $doc = Wait-Until {
            $w = Get-AppWindows 'notepad' | Select-Object -First 1
            if (-not $w) { return $null }
            Close-NotepadWelcome $w
            $found = $w.FindFirst($scope, (New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, $ct::Document)))
            if (-not $found) { $found = $w.FindFirst($scope, (New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, $ct::Edit))) }
            $found
        } 30 'no document/edit control in notepad' } catch { throw ($_.Exception.Message + ' | ' + (Get-NotepadTreeSummary)) }
        $doc.SetFocus(); Start-Sleep -Milliseconds 500
        [System.Windows.Forms.SendKeys]::SendWait('^a')
        Start-Sleep -Milliseconds 500
        # The app's clipboard monitor can hold the clipboard for a moment; retry instead of failing on it.
        $null = Wait-Until { try { [System.Windows.Forms.Clipboard]::SetText($decoy); $true } catch { $false } } 15 'the clipboard stayed locked'
        Start-Sleep -Seconds 1
        [NativeKeys]::Chord(0x11, 0x5B, 0x52)
        Start-Sleep -Seconds 5
        $lines = @((Get-RtstLogText $pkg) -split "`n" | Where-Object { $_ -like "*$tag*" })
        $hot = @($lines | Where-Object { $_ -match "`tHotkey`t" })
        if (-not $hot.Count) { throw "no Hotkey row with tag $tag; rows with tag: " + ($lines -join ' | ') }
        $bad = @($hot | Where-Object { $_ -like "*$decoy*" })
        $good = @($hot | Where-Object { $_ -like "*$selText*" })
        if ($bad.Count -and -not $good.Count) { throw "hotkey read the CLIPBOARD decoy: " + $hot[0] }
        if (-not $good.Count) { throw "hotkey row lacks selection text: " + $hot[0] }
        "hotkey read the notepad selection, not the decoy; rows: " + (($lines | ForEach-Object { ($_ -split "`t")[1..3] -join '/' }) -join ' ; ')
    } finally {
        Get-Process notepad -ErrorAction SilentlyContinue | Stop-Process -Force
        Remove-Item $file -ErrorAction SilentlyContinue
    }
}
