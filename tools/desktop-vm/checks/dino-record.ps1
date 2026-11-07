# after: dino-launch
# Slice 279 Record flow on the Windows head. Stages, each PASS/FAIL, the next still runs:
#   1 grown-up mode -> a tile -> book sheet -> "Record your voice" pill -> the Record intro shows (RecordIntroStart)
#   2 Start recording -> the Record screen: one piece, Skip / Record / key labels "Skip . S", "Record . R"
#   3 S -> trace line "Dino keyboard: S used" (a piece skipped)
#   4 Esc -> back out of the Record screen (before R: R may raise the microphone consent prompt, a window outside the app)
#   5 reopen, R last -> the trace line "Dino keyboard: R used" AND the Stop button (a recording state; a consent prompt that holds focus fails the stage, Slice 284), the window that has focus after R is named; R again stops
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName, [Parameter(Position = 1)] [string] $AlertTextsBase64)
. "$PSScriptRoot\_lib.ps1"

$HoldRingName = 'Press and hold for 3 seconds'
$HoldMs = 3500
$TraceFileName = 'window-trace.log'
$VkEscape = 0x1B; $VkR = 0x52; $VkS = 0x53
$script:Results = @()

function Get-DinoWindow {
    $w = Get-AppWindows 'DinoReader.App' | Select-Object -First 1
    if (-not $w) { throw 'Dino Reader is not running' }
    return $w
}
function Get-AlertTexts {
    # The app's microphone alert texts, read by the host from the worktree's resx (Invoke-DesktopTest.ps1, Get-RecordAlertArgument); a missing argument or key fails the stage.
    if (-not $AlertTextsBase64) { throw 'the host passed no microphone alert texts (resx keys Rec_NoMicTitle, Rec_Failed)' }
    $texts = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($AlertTextsBase64)) | ConvertFrom-Json
    if ($texts.Error) { throw $texts.Error }
    return $texts
}
function Get-TraceLines {
    $p = Join-Path $env:LOCALAPPDATA "Packages\$((Get-InstalledPackage $PackageName).PackageFamilyName)\LocalState\$TraceFileName"
    if (-not (Test-Path $p)) { return @() }
    return @(Get-Content -Path $p)
}
function Count-Trace { param([string] $Pattern) return @(Get-TraceLines | Where-Object { $_ -match $Pattern }).Count }
function Focus-DinoWindow {
    $w = Get-DinoWindow
    [NativeKeys]::SetForegroundWindow([IntPtr]$w.Current.NativeWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 400
}
function Press-Key {
    param([byte] $Vk)
    Focus-DinoWindow
    [NativeKeys]::keybd_event($Vk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [NativeKeys]::keybd_event($Vk, 0, [NativeKeys]::KeyUp, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 600
}
function Find-Visible { param([string] $Id) try { return Find-UiaVisibleById (Get-DinoWindow) $Id } catch { return $null } }
function Enter-GrownUpMode {
    if (Find-Visible 'GalleryDone') { return }
    Invoke-Uia (Wait-Until { Find-Visible 'GalleryGrownUps' } 15 'no For grown-ups button')
    $ring = Wait-Until { Find-UiaExact (Get-DinoWindow) $HoldRingName } 15 'gate did not open'
    Invoke-PressAndHold $ring $HoldMs
    $sum = Wait-Until {
        foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
            if ($e.Current.Name -match 'What is (\d+) plus (\d+)') { return [int]$Matches[1] + [int]$Matches[2] }
        }
    } 10 'gate sum did not appear'
    foreach ($ch in ([string]$sum).ToCharArray()) {
        Invoke-Uia (Wait-Until { Find-UiaExact (Get-DinoWindow) ([string]$ch) 'Button' } 5 "no keypad key $ch")
        Start-Sleep -Milliseconds 300
    }
    Invoke-Uia (Wait-Until { Find-UiaExact (Get-DinoWindow) 'OK' 'Button' } 5 'no OK key')
    Wait-Until { Find-Visible 'GalleryDone' } 15 'grown-up mode did not open' | Out-Null
}
function Start-RecordingFromIntro {
    # Waits until Start recording is visible and enabled, invokes it, then waits for the app's "Start tapped" trace line; a click that never reached the app is invoked once more.
    # Returns a note naming which happened (first invoke, or the retry), so a stage line shows a lost click.
    $before = Count-Trace 'Record intro: Start tapped'
    $tapped = { (Count-Trace 'Record intro: Start tapped') -gt $before }
    $enabled = { $b = Find-Visible 'RecordIntroStart'; if ($b -and $b.Current.IsEnabled) { $b } }
    foreach ($attempt in 1, 2) {
        Invoke-Uia (Wait-Until $enabled 15 'Start recording was not visible and enabled')
        try { Wait-Until $tapped 6 'Start tapped trace line' | Out-Null; if ($attempt -eq 1) { return 'Start tapped on the first invoke' } else { return 'Start tapped only after the retry (first invoke lost)' } }
        catch { $null = $_ }
    }
    throw 'Start recording was invoked twice but the app never traced "Record intro: Start tapped"'
}
function Get-FocusedWindowNote {
    # Names the window that has keyboard focus: "Dino Reader" when it is the app's own, else the other process and its top-level window name (e.g. a microphone consent prompt).
    try {
        $focused = [System.Windows.Automation.AutomationElement]::FocusedElement
        if (-not $focused) { return 'no focused element' }
        $focusPid = $focused.Current.ProcessId
        $dinoPid = (Get-DinoWindow).Current.ProcessId
        $top = $focused
        $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
        while ($true) {
            $parent = $walker.GetParent($top)
            if (-not $parent -or $parent -eq [System.Windows.Automation.AutomationElement]::RootElement) { break }
            $top = $parent
        }
        $proc = (Get-Process -Id $focusPid -ErrorAction SilentlyContinue).ProcessName
        $label = "process=$proc window='$($top.Current.Name)' focused='$($focused.Current.Name)'"
        if ($focusPid -eq $dinoPid) { return "Dino's own window ($label)" }
        return "OTHER WINDOW, not Dino ($label)"
    }
    catch { return "could not read focus: $($_.Exception.Message)" }
}
function Add-Result {
    param([string] $Name, [scriptblock] $Body)
    try { $detail = & $Body; $script:Results += "PASS $Name $detail" }
    catch {
        $ui = ''
        try { $ui = (Get-VisibleNames (Get-DinoWindow) 30) -join ' / ' } catch { $ui = 'no window' }
        $recordLines = (Get-TraceLines | Where-Object { $_ -match 'Dino Record|Record intro' } | Select-Object -Last 6) -join ' | '
        $script:Results += "FAIL $Name $($_.Exception.Message) UI: $ui RECORD TRACE: $recordLines"
    }
}
function Reset-ToGallery {
    # A failed stage can leave the Record screen (mic live) or the intro open: Esc back out until a gallery button shows, so the next check starts from the shelf.
    for ($i = 0; $i -lt 5; $i++) {
        try { if ((Find-Visible 'GalleryGrownUps') -or (Find-Visible 'GalleryDone')) { return } } catch { $null = $_ }
        try { Press-Key $VkEscape } catch { return }
        Start-Sleep -Milliseconds 800
    }
}

Invoke-Check -ResultFile $ResultFile -Body {
    Add-Result 'intro' {
        Enter-GrownUpMode
        $tile = Wait-Until {
            foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
                if ($e.Current.AutomationId -eq 'StoryTile' -and -not $e.Current.BoundingRectangle.IsEmpty) { return $e }
            }
        } 15 'no visible story tile in the grown-ups corner'
        Invoke-Uia $tile
        Wait-Until { Find-Visible 'BookSheetClose' } 10 'book sheet did not open' | Out-Null
        $names = (Get-VisibleNames (Get-DinoWindow) 40) -join ' / '
        $pill = Wait-Until { Find-UiaByName -Root (Get-DinoWindow) -Name 'Record your voice' } 5 "no Record pill on the book sheet. UI: $names"
        Invoke-Uia $pill
        Wait-Until { Find-Visible 'RecordIntroStart' } 15 'the Record intro did not show (no RecordIntroStart)' | Out-Null
        'book sheet Record pill opened the intro with Start recording'
    }
    Add-Result 'record-page' {
        $startNote = Start-RecordingFromIntro
        Wait-Until { Find-Visible 'RecordButton' } 25 'the Record screen did not show a Record button' | Out-Null
        $skip = Find-Visible 'RecordSkip'; $next = Find-Visible 'RecordNext'
        if (-not $skip) { throw 'no Skip button' }
        $words = Find-Visible 'RecordPageWords'
        $names = (Get-VisibleNames (Get-DinoWindow) 40) -join ' / '
        "Record screen ($startNote): Skip=$([bool]$skip) Record=True Next=$([bool]$next) words=$([bool]$words) UI: $names"
    }
    Add-Result 'key-S' {
        # First on the fresh, unrecorded piece (S needs the Ready step); R then records the next piece.
        $before = Count-Trace 'Dino keyboard: S used'
        Press-Key $VkS
        Start-Sleep -Seconds 2
        $used = (Count-Trace 'Dino keyboard: S used') - $before
        $ui = (Get-VisibleNames (Get-DinoWindow) 30) -join ' / '
        if ($used -lt 1) { throw "S was not used (no trace line; S only works on an unrecorded piece, Ready state); UI: $ui" }
        "S used x$used; UI: $ui"
    }
    Add-Result 'esc-back' {
        # Esc runs on a fresh piece BEFORE key-R: R may raise Windows' microphone consent prompt, a window outside the app that takes the next key.
        $before = Count-Trace 'Dino keyboard: Escape used'
        Press-Key $VkEscape
        # A positive sign of having left: the intro (or the book sheet under it) is showing and the Record screen's own controls are gone.
        # The absence of the Record button alone proves nothing: a recorded piece has none either.
        $left = { (Find-Visible 'RecordIntroStart') -or (Find-Visible 'BookSheetClose') }
        try { Wait-Until $left 10 'neither the Record intro nor the book sheet showed after Esc' | Out-Null } catch { $null = $_ }
        $ui = (Get-VisibleNames (Get-DinoWindow) 30) -join ' / '
        if (-not (& $left)) { throw "still on the Record screen after Esc (no Record intro, no book sheet); UI: $ui" }
        if (Find-Visible 'RecordSkip') { throw "Record screen controls still showing after Esc; UI: $ui" }
        $escUsed = (Count-Trace 'Dino keyboard: Escape used') - $before
        "left the Record screen (Escape trace lines x$escUsed; intro=$([bool](Find-Visible 'RecordIntroStart')) sheet=$([bool](Find-Visible 'BookSheetClose'))); UI: $ui"
    }
    Add-Result 'key-R' {
        # Last: reopen the Record screen, press R, then name the window that has focus (a microphone consent prompt would be a system window, not Dino's).
        $startNote = Start-RecordingFromIntro
        Wait-Until { Find-Visible 'RecordButton' } 25 'the Record screen did not reopen (no Record button)' | Out-Null
        $before = Count-Trace 'Dino keyboard: R used'
        Press-Key $VkR
        # Wait on the trace line, not a fixed time; a miss is reported below with the UI.
        try { Wait-Until { (Count-Trace 'Dino keyboard: R used') -gt $before } 6 'R trace line' | Out-Null } catch { $null = $_ }
        # Review finding 1: the trace line is logged before the recorder opens the device (asynchronous on Windows), so wait for the Stop button too; a miss is judged below.
        try { Wait-Until { Find-Visible 'RecordStopButton' } 8 'the Stop button' | Out-Null } catch { $null = $_ }
        $used = (Count-Trace 'Dino keyboard: R used') - $before
        $focus = Get-FocusedWindowNote
        $state = if (Find-Visible 'RecordStopButton') { 'Stop button showing (recording)' } else { 'no Stop button' }
        $ui = (Get-VisibleNames (Get-DinoWindow) 30) -join ' / '
        $detail = "reopened ($startNote); R used x$used; $state; focus after R: $focus; UI: $ui"
        try {
            if ($used -lt 1) { throw "R was not used (no trace line); $state; focus after R: $focus; UI: $ui" }
            # Slice 284: the trace line alone also appears while the microphone-consent prompt holds focus and no recording starts. Pass only when a recording state shows:
            # the Stop button, or the app's own refusal alert (RecordPage.TryStartRecorderAsync: "Microphone is off" when refused, "The recording didn't work..." with no usable microphone;
            # the alerts carry no AutomationId, so they are found by their English text, as the VM runs in English). Neither showing (a consent prompt holding focus) fails.
            $dinoWindow = Get-DinoWindow
            if (-not $dinoWindow) { throw "R was used but the Dino window is gone; focus after R: $focus" }
            $alertTexts = Get-AlertTexts
            $refused = (Find-UiaExact $dinoWindow $alertTexts.NoMic) -or (Find-UiaExact $dinoWindow $alertTexts.Failed)
            if ($refused) {
                # The alert is modal: dismiss it so it never covers the later stages or takes the finally block's second R. It must be gone within 3 s, else the stage fails (check pass 2).
                try { Invoke-Uia (Find-UiaExact $dinoWindow 'OK' 'Button') } catch { Press-Key $VkEscape }
                try { Wait-Until { -not ((Find-UiaExact (Get-DinoWindow) $alertTexts.NoMic) -or (Find-UiaExact (Get-DinoWindow) $alertTexts.Failed)) } 3 'the alert to close' | Out-Null }
                catch { throw "the microphone alert did not close within 3 s after dismissing it (OK button / Escape); it would block the later stages; UI: $ui" }
            }
            if (-not (Find-Visible 'RecordStopButton') -and -not $refused) { throw "R was used but neither a recording nor a microphone-refused/no-microphone alert showed (a microphone consent prompt may hold focus); focus after R: $focus; UI: $ui" }
            if ($refused) { $detail += '; microphone refused or missing (the app showed its alert), key handling itself worked' }
        }
        finally {
            # Always stop a running recording, also when the stage fails, so the mic is not left live for the next check.
            if (Find-Visible 'RecordStopButton') {
                Press-Key $VkR
                Start-Sleep -Seconds 2
                $detail += "; second R: stop=$([bool](Find-Visible 'RecordStopButton')) play=$([bool](Find-Visible 'RecordPlay')) redo=$([bool](Find-Visible 'RecordRedo'))"
            }
        }
        $detail
    }
    Reset-ToGallery # always, so the next check starts from the shelf with the mic off, even when every stage passed
    $trace = (Get-TraceLines | Select-Object -Last 15) -join ' | '
    $text = ($script:Results -join "`n") + "`nTRACE: $trace"
    if ($script:Results | Where-Object { $_ -like 'FAIL*' }) { throw $text }
    $text
}
