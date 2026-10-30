# Kids' shelf -> "For grown-ups" button -> the real grown-up gate (hold 3 s, then the sum) -> grown-up mode -> its gear -> Settings
# with its groups -> Back -> grown-up mode -> Done -> kids' shelf.
# The flow of Slice 189 (the shelf gear is gone; Settings opens from the gear inside grown-up mode, with no second gate).
# No test bypass of the gate (Slice 188). Runs after dino-page-turn, so it first leaves the reading page for the shelf.
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"

$HoldRingName = 'Press and hold for 3 seconds'   # Gate_HoldDescription on the HoldRing; the label above it ends in a full stop
$HoldMs = 3500
$BackName = 'Back'

function Get-DinoWindow {
    $w = Get-AppWindows 'DinoReader.App' | Select-Object -First 1
    if (-not $w) { throw 'Dino Reader is not running' }
    return $w
}

function Wait-VisibleId {
    param([string] $Id, [int] $TimeoutSec, [string] $Failure)
    return Wait-Until { Find-UiaVisibleById (Get-DinoWindow) $Id } $TimeoutSec $Failure
}

function Wait-VisibleName {
    param([string] $Name, [int] $TimeoutSec, [string] $Failure)
    return Wait-Until { Find-UiaExact (Get-DinoWindow) $Name } $TimeoutSec $Failure
}

function Find-GroupHeading {
    # SettingsRows.GroupHeading upper-cases its text, so UIA shows "READING"; match case-insensitively.
    param([string] $Heading)
    return Find-UiaExact (Get-DinoWindow) $Heading.ToUpperInvariant()
}

function Find-GateSum {
    foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
        if ($e.Current.Name -match 'What is (\d+) plus (\d+)') { return [int]$Matches[1] + [int]$Matches[2] }
    }
    return $null
}

function Open-GateFromShelf {
    $back = Find-UiaById (Get-DinoWindow) 'ReadingBack'
    if ($back) { Invoke-Uia $back }
    $button = Wait-VisibleId 'GalleryGrownUps' 15 'FAIL the "For grown-ups" button is not on the kids'' shelf'
    Invoke-Uia $button   # InvokePattern first; the mouse click is only the fallback when the button has no pattern
    $gate = $null
    try { $gate = Wait-VisibleName $HoldRingName 5 'gate not up' } catch { $gate = $null }
    if (-not $gate) {
        # A run once saw only the button's tooltip after the first press: invoke once more before failing.
        Invoke-Uia (Wait-VisibleId 'GalleryGrownUps' 5 'FAIL the "For grown-ups" button vanished before the retry')
        Wait-VisibleName $HoldRingName 15 'FAIL the gate did not open from the "For grown-ups" button' | Out-Null
    }
}

function Pass-Gate {
    Invoke-PressAndHold (Wait-VisibleName $HoldRingName 5 'FAIL no hold ring') $HoldMs
    $answer = [string](Wait-Until { Find-GateSum } 10 'FAIL gate sum did not appear after the hold')
    foreach ($ch in $answer.ToCharArray()) {
        Invoke-Uia (Wait-Until { Find-UiaExact (Get-DinoWindow) ([string]$ch) 'Button' } 5 "FAIL no keypad key $ch")
        Start-Sleep -Milliseconds 300
    }
    Invoke-Uia (Wait-Until { Find-UiaExact (Get-DinoWindow) 'OK' 'Button' } 5 'FAIL no OK key')
    Wait-VisibleId 'GalleryDone' 15 'FAIL grown-up mode did not open after the gate' | Out-Null
    return $answer
}

function Open-Settings {
    Invoke-Uia (Wait-VisibleId 'GallerySettings' 5 'FAIL no gear in grown-up mode')
    Wait-VisibleId 'SettingsTitle' 15 'FAIL Settings did not open from the grown-up gear' | Out-Null
    foreach ($group in @('Reading', 'This phone')) {
        if (-not (Find-GroupHeading $group)) { throw "FAIL Settings group '$group' missing" }
    }
}

function Invoke-SettingsBack {
    # Back on a Settings page is a tappable Border named "Back" (SemanticProperties.Description), not a Button.
    Invoke-Uia (Wait-VisibleName $BackName 5 'FAIL no Back control')
}

function Get-PageTitle {
    # Every Settings page shows its title in the Label with AutomationId SettingsTitle ("Settings").
    $t = Find-UiaVisibleById (Get-DinoWindow) 'SettingsTitle'
    if ($t) { return $t.Current.Name }
    return $null
}

function Test-BackToShelf {
    Wait-Until { (Get-PageTitle) -eq 'Settings' } 10 'FAIL Settings page title not showing' | Out-Null
    Invoke-SettingsBack
    Wait-Until { -not (Find-UiaVisibleById (Get-DinoWindow) 'SettingsTitle') } 10 'FAIL Settings is still showing after Back' | Out-Null
    Wait-VisibleId 'GalleryDone' 10 'FAIL Back from Settings did not return to grown-up mode' | Out-Null
    Invoke-Uia (Find-UiaVisibleById (Get-DinoWindow) 'GalleryDone')
    Wait-VisibleId 'GalleryGrownUps' 10 'FAIL Done did not return to the kids'' shelf' | Out-Null
    if (Find-UiaVisibleById (Get-DinoWindow) 'GallerySettings') { throw 'FAIL the gear is still visible on the kids'' shelf' }
}

Invoke-Check -ResultFile $ResultFile -Body {
    $log = @()
    try {
        Open-GateFromShelf;           $log += 'gate opened from the For grown-ups button'
        $answer = Pass-Gate;          $log += "gate passed (answer $answer), grown-up mode open"
        Open-Settings;                $log += 'gear -> Settings, groups Reading and This phone shown'
        Test-BackToShelf;             $log += 'Back -> grown-up mode, Done -> kids'' shelf'
    } catch {
        $dump = Get-VisibleNames (Get-DinoWindow)
        throw ('[' + ($log -join ' | ') + '] ' + $_.Exception.Message + ' UI: ' + ($dump -join ' / '))
    }
    $log -join ' | '
}
