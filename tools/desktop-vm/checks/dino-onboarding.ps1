# after: dino-onboarding-keys
# The rest of the first run on the Windows head (Slice 362, mock 2026-11-13 frames N5 to N10, WN5): dino-onboarding-keys leaves the app on the voice step. This check finishes the path with the keyboard and lands on the kids' shelf.
# With a microphone: R records, R stops, Right goes on (Finish on the last line) -> "That's you!" (N7): Enter is Continue, which deletes the try. Without one (the Record page opens and the microphone is refused, or the recorder fails to open):
# the "Microphone is off" state shows and its quiet "Continue without recording" is the only way on. Slice 418: there is no corner tour any more (five steps): the app lands on the locked shelf (the title "My stories", the
# lock without its countdown chip) right after the voice step; a second launch is not part of this check (the flow onboarding-phone.yaml proves later launches open on the shelf).
# Screenshots go to <results>\onboarding-hear-yourself.png (N7) and onboarding-shelf.png (the locked shelf). The text is ASCII on purpose (Windows PowerShell 5.1 reads a BOM-less file as ANSI).
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"

$VkEnter = 0x0D; $VkRight = 0x27; $VkR = 0x52
$script:Notes = [System.Collections.Generic.List[string]]::new()   # the harness collects only the string Invoke-Check's body returns, so notes ride on the pass message

function Get-DinoWindow {
    $w = Get-AppWindows 'DinoReader.App' | Select-Object -First 1
    if (-not $w) { throw 'Dino Reader is not running (dino-onboarding-keys leaves it on the voice step)' }
    return $w
}

function Focus-DinoWindow {
    $w = Get-DinoWindow
    [NativeKeys]::SetForegroundWindow([IntPtr]$w.Current.NativeWindowHandle) | Out-Null
    try { $w.SetFocus() } catch { }
    Start-Sleep -Milliseconds 400
}

function Press-Key {
    param([byte] $Vk)
    [NativeKeys]::keybd_event($Vk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [NativeKeys]::keybd_event($Vk, 0, 2, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 400
}

function Save-Shot {
    param([string] $Name)
    Save-WindowScreenshot -Window (Get-DinoWindow) -Path (Join-Path (Split-Path $script:ResultPath) $Name)
}

function Test-Present {
    param([string] $Id)
    return [bool](Find-UiaVisibleById (Get-DinoWindow) $Id)
}

function Wait-Present {
    param([string] $Id, [int] $Seconds, [string] $Failure)
    Wait-Until { Test-Present $Id } $Seconds $Failure | Out-Null
}

# The voice step: record with the keys when the microphone works, else take the quiet way past it.
function Complete-VoiceStep {
    Wait-Present 'OnboardingVoice' 30 'FAIL the voice step is not showing (dino-onboarding-keys should have left the app on it)'
    Focus-DinoWindow
    Press-Key $VkEnter
    Wait-Until { (Test-Present 'RecordButton') -or (Test-Present 'OnboardingMicOffTitle') } 30 'FAIL Enter on the voice step opened neither the Record page nor the microphone-off state' | Out-Null
    if (Test-Present 'RecordButton') {
        Record-WithKeys
        return
    }

    $script:Notes.Add('the microphone is off: Continue without recording')
    Invoke-Uia (Wait-Until { Find-UiaById (Get-DinoWindow) 'OnboardingContinueWithout' } 20 'FAIL the microphone-off state has no Continue without recording')
}

function Deny-FirstMicPrompt {
    # The clean image asks on the first R (a Windows prompt, reachable only as the focused element). It is DENIED here, the answer dino-record's stage 5 expects, so a full run still proves the deny path once;
    # the app then shows its microphone-off state and setup goes on with Continue without recording. Returns $true when that state showed (setup is done), $false when no prompt came (consent already decided: recording started).
    $script:Notes.Add((Answer-MicConsentPrompt (Get-DinoWindow).Current.ProcessId 'Deny' 20 { (Test-Present 'RecordStopButton') -or (Test-Present 'OnboardingMicOffTitle') }))
    if (-not (Test-Present 'RecordStopButton')) { Wait-Until { (Test-Present 'OnboardingMicOffTitle') -or (Test-Present 'RecordStopButton') } 15 'FAIL R on the voice step started no recording and showed no microphone-off state' | Out-Null }
    if (-not (Test-Present 'OnboardingMicOffTitle')) { return $false }
    $script:Notes.Add('the microphone prompt was denied: Continue without recording')
    Invoke-Uia (Wait-Until { Find-UiaById (Get-DinoWindow) 'OnboardingContinueWithout' } 20 'FAIL the microphone-off state has no Continue without recording')
    return $true
}

function Record-WithKeys {
    for ($line = 1; $line -le 4 -and -not (Test-Present 'OnboardingHearYourself'); $line++) {
        Focus-DinoWindow
        Press-Key $VkR
        if ($line -eq 1 -and (Deny-FirstMicPrompt)) { return }
        Wait-Present 'RecordStopButton' 15 "FAIL R did not start recording line $line"
        Start-Sleep -Milliseconds 1500   # fixed-wait: a second and a half of microphone gives the clip a length
        Focus-DinoWindow   # the stopping R must reach the app, not another window that took focus meanwhile
        Press-Key $VkR
        if (Test-Present 'OnboardingMicOffTitle') { return }
        Wait-Present 'RecordChip' 20 "FAIL line $line shows no Recorded chip after R stopped it"
        Press-Key $VkRight
        Start-Sleep -Seconds 1
        if (Test-Present 'OnboardingMicOffTitle') { Invoke-Uia (Find-UiaById (Get-DinoWindow) 'OnboardingContinueWithout'); return }
    }

    if (Test-Present 'OnboardingHearYourself') {
        Start-Sleep -Seconds 1   # fixed-wait: the chip and the picture draw in after the page shows
        Save-Shot 'onboarding-hear-yourself.png'
        Focus-DinoWindow
        Press-Key $VkEnter
        $script:Notes.Add('recorded with R, Right, and continued from "That''s you" with Enter')
    }
}

function Test-LockedShelfAfterSetup {
    Wait-Until { Find-UiaById (Get-DinoWindow) 'GalleryTitle' } 60 'FAIL the voice step did not land on the shelf' | Out-Null
    Wait-Until { Test-LockedShelfShown (Get-DinoWindow) } 20 'FAIL the shelf after setup is not locked (the lock without its countdown chip)' | Out-Null
    Start-Sleep -Seconds 1   # fixed-wait: the shelf finishes laying out before the screenshot
    Save-Shot 'onboarding-shelf.png'
}

Invoke-Check -ResultFile $ResultFile -Body {
    $script:ResultPath = $ResultFile
    $pkg = Get-InstalledPackage $PackageName
    Complete-VoiceStep
    Test-LockedShelfAfterSetup
    $title = Find-UiaById (Get-DinoWindow) 'GalleryTitle'
    "first run finished with the keyboard, the locked shelf shows '$($title.Current.Name)'; the voice step answered Enter; " + ($script:Notes -join '; ')
}
