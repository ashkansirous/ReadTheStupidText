# after: dino-onboarding-keys
# The second route through the first run (Slice 418 decision 2, mock NO5): "Skip this step" (OnboardingSkipStep) on the voice intro ends setup at once and lands on the locked shelf, with no recording and no microphone prompt.
# It takes the place of dino-onboarding, never follows it: the first run happens once per clean image, so it is not in the default check list; run it alone with -Check dino-onboarding-skip (dino-onboarding-keys runs first and leaves the app on the voice step).
# Screenshot: <results>\onboarding-skip-shelf.png. The text is ASCII on purpose (Windows PowerShell 5.1 reads a BOM-less file as ANSI).
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"

function Get-DinoWindow {
    $w = Get-AppWindows 'DinoReader.App' | Select-Object -First 1
    if (-not $w) { throw 'Dino Reader is not running (dino-onboarding-keys leaves it on the voice step)' }
    return $w
}

Invoke-Check -ResultFile $ResultFile -Body {
    $null = Get-InstalledPackage $PackageName   # proves the package is installed before the UI is touched
    Wait-Until { Find-UiaVisibleById (Get-DinoWindow) 'OnboardingVoice' } 30 'FAIL the voice step is not showing (dino-onboarding-keys should have left the app on it)' | Out-Null
    Invoke-Uia (Wait-Until { Find-UiaVisibleById (Get-DinoWindow) 'OnboardingSkipStep' } 20 'FAIL the voice intro has no Skip this step (OnboardingSkipStep)')
    Wait-Until { Find-UiaById (Get-DinoWindow) 'GalleryTitle' } 60 'FAIL Skip this step did not land on the shelf' | Out-Null
    Wait-Until { Test-LockedShelfShown (Get-DinoWindow) } 20 'FAIL the shelf after Skip this step is not locked (the lock without its countdown chip)' | Out-Null
    if (Find-UiaVisibleById (Get-DinoWindow) 'OnboardingHearYourself') { throw 'FAIL "That''s you" showed after Skip this step (no clip was recorded)' }
    Start-Sleep -Seconds 1   # fixed-wait: the shelf finishes laying out before the screenshot
    Save-WindowScreenshot -Window (Get-DinoWindow) -Path (Join-Path (Split-Path $ResultFile) 'onboarding-skip-shelf.png')
    'first run finished with Skip this step on the voice intro; the locked shelf shows, no recording and no microphone prompt'
}
