# after: dino-launch
# The grown-ups' gate as one centred cream card (Slice 363, mock Missing States G1-G3): the card is 440 dp wide and centred in the window (G1 hold, G2 sum), the hold ring fills while the
# Space bar is held and empties on letting go, the sum step shows six answer keys (the right answer once), a wrong typed answer then Enter clears the field and rolls a new sum, a click on the
# right answer key opens the grown-ups' corner, and Esc on the gate goes back to the child's shelf. Keys are real key events (keybd_event) sent to the focused window; screenshots of G1 and G2
# are saved next to the result file for the comparison with the mock. The VM runs at 100% scaling, so UIA's pixels are the app's dp.
# Runs after dino-keyboard (which ends on the kids' shelf); it opens the gate itself from the shelf, so it also runs alone after dino-launch.
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"

$HoldRingName = 'Press and hold for 3 seconds'   # Gate_HoldDescription on the card's HoldRing (the title is hidden from UIA on the hold step, so the name is the ring's alone)
$CardId = 'GateCard'                               # the card's AutomationId (GatePage.xaml)
$CardWidth = 440
$ShortCardWidth = 470
$ShortBelowHeight = 540
$Tolerance = 14
$AnswerKeyCount = 6
$VkSpace = 0x20; $VkEnter = 0x0D; $VkEscape = 0x1B; $VkDigit0 = 0x30
$HoldMs = 3600
$ShortHoldMs = 1200
$RepeatMs = 250   # a held key repeats about every 33 ms on a real keyboard; keybd_event sends one press, so repeat it by hand
$ScreenshotHold = 'gate-g1-hold.png'
$ScreenshotSum = 'gate-g2-sum.png'

function Get-DinoWindow {
    $w = Get-AppWindows 'DinoReader.App' | Select-Object -First 1
    if (-not $w) { throw 'Dino Reader is not running' }
    return $w
}

function Wait-VisibleId {
    param([string] $Id, [int] $TimeoutSec, [string] $Failure)
    return Wait-Until { Find-UiaVisibleById (Get-DinoWindow) $Id } $TimeoutSec $Failure
}

function Focus-DinoWindow {
    $w = Get-DinoWindow
    [NativeKeys]::SetForegroundWindow([IntPtr]$w.Current.NativeWindowHandle) | Out-Null
    try { $w.SetFocus() } catch { }
    Start-Sleep -Milliseconds 400
}

function Press-Key {
    param([byte] $Vk, [int] $DownMs = 120)
    [NativeKeys]::keybd_event($Vk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $DownMs
    [NativeKeys]::keybd_event($Vk, 0, [NativeKeys]::KeyUp, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
}

function Hold-Key {
    # Key down, then a repeat of the key down every $RepeatMs (what Windows does for a held key), then key up.
    param([byte] $Vk, [int] $HoldForMs)
    $until = (Get-Date).AddMilliseconds($HoldForMs)
    try {
        while ((Get-Date) -lt $until) {
            [NativeKeys]::keybd_event($Vk, 0, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds $RepeatMs
        }
    } finally { [NativeKeys]::keybd_event($Vk, 0, [NativeKeys]::KeyUp, [UIntPtr]::Zero) }
    Start-Sleep -Milliseconds 300
}

function Type-Number {
    param([int] $Number)
    foreach ($ch in ([string]$Number).ToCharArray()) { Press-Key ([byte]($VkDigit0 + [int][string]$ch)) }
}

function Save-Shot {
    param([string] $Name)
    Save-WindowScreenshot -Window (Get-DinoWindow) -Path (Join-Path (Split-Path $script:ResultPath) $Name)
}

function Open-Gate {
    Enter-KidsShelf   # the first run ends in the grown-ups' corner
    $back = Find-UiaById (Get-DinoWindow) 'ReadingBack'
    if ($back) { Invoke-Uia $back }
    Invoke-Uia (Wait-VisibleId 'GalleryGrownUps' 15 'FAIL the "For grown-ups" button is not on the kids'' shelf')
    $ring = $null
    try { $ring = Wait-Until { Find-UiaExact (Get-DinoWindow) $HoldRingName } 5 'gate not up' } catch { $ring = $null }
    if (-not $ring) {
        Invoke-Uia (Wait-VisibleId 'GalleryGrownUps' 5 'FAIL the "For grown-ups" button vanished before the retry')
        Wait-Until { Find-UiaExact (Get-DinoWindow) $HoldRingName } 15 'FAIL the gate did not open from the "For grown-ups" button' | Out-Null
    }
    Focus-DinoWindow
}

function Get-GateSum {
    foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
        if ($e.Current.Name -match 'What is (\d+) plus (\d+)') { return [int]$Matches[1] + [int]$Matches[2] }
    }
    return $null
}

function Get-AnswerKeys {
    # The six answer keys: visible Buttons named by a 1 or 2 digit number.
    $keys = @()
    foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
        if ($e.Current.ControlType.ProgrammaticName -ne 'ControlType.Button') { continue }
        if ($e.Current.Name -notmatch '^\d{1,2}$' -or $e.Current.BoundingRectangle.IsEmpty) { continue }
        $keys += $e
    }
    return $keys
}

function Test-CardShape {
    # G1 to G3: the card is 440 wide (470 in a window under 540 high) and centred in the window.
    param([string] $Step)
    $window = (Get-DinoWindow).Current.BoundingRectangle
    $card = Wait-Until { Find-UiaVisibleById (Get-DinoWindow) $CardId } 5 "FAIL no gate card ($CardId) on the $Step step"
    $r = $card.Current.BoundingRectangle
    # The same size rule as OnboardingLayout.Gate: under 540 high the 470 short card, else the 440 card, never over the window less 24 dp each side.
    $expected = [Math]::Min($(if ($window.Height -lt $ShortBelowHeight) { $ShortCardWidth } else { $CardWidth }), $window.Width - 48)
    if ([Math]::Abs($r.Width - $expected) -gt $Tolerance) { throw "FAIL the gate card on the $Step step is $([int]$r.Width) dp wide, not $([int]$expected) (window $([int]$window.Width) x $([int]$window.Height))" }
    $offset = [Math]::Abs(($r.X + $r.Width / 2) - ($window.X + $window.Width / 2))
    if ($offset -gt $Tolerance) { throw "FAIL the gate card on the $Step step is $([int]$offset) dp off the window's middle (it must be centred)" }
    if ($r.Height -ge $window.Height) { throw "FAIL the gate card on the $Step step is as tall as the window" }
}

function Test-HoldStep {
    Open-Gate
    Test-CardShape 'hold'
    Save-Shot $ScreenshotHold
    # Letting go early empties the ring: the sum must not appear.
    Hold-Key $VkSpace $ShortHoldMs
    Start-Sleep -Seconds 1
    if (Get-GateSum) { throw "FAIL the sum appeared after holding the Space bar for only $ShortHoldMs ms (letting go must empty the ring)" }
    Hold-Key $VkSpace $HoldMs
    return [int](Wait-Until { Get-GateSum } 10 'FAIL the sum did not appear after holding the Space bar for 3.6 s')
}

function Test-SumStep {
    param([int] $Answer)
    Test-CardShape 'sum'
    $keys = @(Get-AnswerKeys)
    if ($keys.Count -ne $AnswerKeyCount) { throw "FAIL the sum step shows $($keys.Count) answer keys, not $AnswerKeyCount" }
    $names = @($keys | ForEach-Object { $_.Current.Name })
    if (@($names | Where-Object { $_ -eq [string]$Answer }).Count -ne 1) { throw "FAIL the right answer $Answer is not on exactly one answer key (keys: $($names -join ', '))" }
    if (@($names | Select-Object -Unique).Count -ne $AnswerKeyCount) { throw "FAIL the answer keys are not all different (keys: $($names -join ', '))" }
    Save-Shot $ScreenshotSum
    # A wrong answer typed then Enter clears the field and shows a new sum; the gate stays.
    Type-Number ($Answer + 1)
    Press-Key $VkEnter
    Start-Sleep -Milliseconds 800
    if (Find-UiaVisibleById (Get-DinoWindow) 'GalleryDone') { throw 'FAIL a wrong typed answer opened grown-up mode' }
    $next = [int](Wait-Until { Get-GateSum } 5 'FAIL no sum after a wrong answer')
    # A click on the right answer key is the answer.
    $right = @(Get-AnswerKeys | Where-Object { $_.Current.Name -eq [string]$next })
    if ($right.Count -ne 1) { throw "FAIL the new sum's right answer $next is not on exactly one answer key" }
    Invoke-Uia $right[0]
    Wait-VisibleId 'GalleryDone' 15 'FAIL clicking the right answer key did not open grown-up mode' | Out-Null
}

function Test-EscBack {
    # The corner is left by its coral button, then the gate is opened again and Esc must close it onto the kids' shelf.
    Enter-KidsShelf
    Open-Gate
    Press-Key $VkEscape
    # The shelf's button is already visible under the modal gate, so wait for the card to vanish first.
    Wait-Until { -not (Find-UiaVisibleById (Get-DinoWindow) $CardId) } 10 'FAIL the gate card is still showing after Esc' | Out-Null
    Wait-VisibleId 'GalleryGrownUps' 10 'FAIL Esc on the gate did not come back to the kids'' shelf' | Out-Null
    if (Find-UiaExact (Get-DinoWindow) $HoldRingName) { throw 'FAIL the gate is still showing after Esc' }
}

$script:ResultPath = $ResultFile
Invoke-Check -ResultFile $ResultFile -Body {
    $answer = Test-HoldStep
    Test-SumStep $answer
    Test-EscBack
    "gate card: 440 dp (470 dp short card in a window under 540 high) and centred on the hold and sum steps; Space hold fills the ring and letting go empties it; six different answer keys held the right one; a wrong typed answer rolled a new sum; a click on the right key opened the corner; Esc went back to the shelf (screenshots $ScreenshotHold, $ScreenshotSum)"
}
