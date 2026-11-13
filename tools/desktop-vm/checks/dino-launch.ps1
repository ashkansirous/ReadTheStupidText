# after: dino-onboarding
# Launches Dino Reader (or finds the running instance) and checks that the first run is done and the shelf shows. Slice 362: the first run (language, story languages, books, listen, your voice, corner tour) is walked with the keyboard by dino-onboarding-keys and dino-onboarding, which run first; this check then only finds the shelf.
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"
Invoke-Check -ResultFile $ResultFile -Body {
    $pkg = Get-InstalledPackage $PackageName
    $window = { Get-AppWindows 'DinoReader.App' | Select-Object -First 1 }
    [NativeKeys]::Chord(0x1B)   # closes the Start menu the VM checkpoint was taken with
    # Slice 325: a WinUI 3 app opens a second process and window on every launch, and dino-onboarding-keys leaves its instance running. Two windows at 0,0 split the checks: UIA answers for one while the mouse presses land on the other (the gate's press-and-hold never started; every log since the key walk shows two pids here, older ones one). Start the app only when none runs.
    # A process that exists but has no window, or is not responding, is a stale or hung instance (not the one dino-onboarding-keys left working): kill it and start fresh.
    $running = @(Get-Process DinoReader.App -ErrorAction SilentlyContinue)
    # A process still starting has no window yet: give it up to 20 s to show one before it counts as stale.
    if ($running.Count -gt 0 -and -not (& $window)) {
        $deadline = (Get-Date).AddSeconds(20)
        while (-not (& $window) -and (Get-Date) -lt $deadline -and @(Get-Process DinoReader.App -ErrorAction SilentlyContinue).Count -gt 0) { Start-Sleep -Milliseconds 500 }
    }
    # A process that reports Responding=false may only be painting its first frame: re-check for up to 10 s (fresh Get-Process each time, Responding is cached on an object) before it counts as hung.
    if ($running.Count -gt 0 -and ($running | Where-Object { -not $_.Responding })) {
        $grace = (Get-Date).AddSeconds(10)
        while ((Get-Date) -lt $grace -and @(Get-Process DinoReader.App -ErrorAction SilentlyContinue | Where-Object { -not $_.Responding }).Count -gt 0) { Start-Sleep -Milliseconds 500 }
        $running = @(Get-Process DinoReader.App -ErrorAction SilentlyContinue)
    }
    # A process that still does not respond after the grace, or still shows no window after the 20 s above, is stopped and the app started fresh (a windowless instance would only fail the window wait below).
    if ($running.Count -gt 0 -and (($running | Where-Object { -not $_.Responding }) -or -not (& $window))) {
        $running | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        $running = @()
    }
    $running = @(Get-Process DinoReader.App -ErrorAction SilentlyContinue)   # the waits above can outlast a process: decide on what runs now
    if ($running.Count -eq 0) { Start-PackagedApp $pkg }
    $w = Wait-Until $window 30 'Dino Reader window did not open'
    # Slice 325: dino-onboarding-keys runs before this check and walks the first run with the keyboard; when it already left the app on the kids' shelf there is nothing to walk.
    $already = Find-UiaVisibleById (& $window) 'GalleryTitle'
    if ($already) { return "launched, first run already done by dino-onboarding-keys, shelf shows '$($already.Current.Name)' (pid $((Get-Process DinoReader.App).Id -join ','))" }
    # Slice 362: the first run has six steps and its voice step needs the keyboard walk (dino-onboarding-keys, then dino-onboarding), so this check no longer walks it by mouse: when the shelf is not showing, the two earlier checks did not finish the first run.
    if (Find-UiaVisibleById (& $window) 'FirstRunLanguage') { throw 'FAIL the first run is still at its language page: dino-onboarding-keys and dino-onboarding must run before this check' }
    $title = Wait-Until { Find-UiaById (& $window) 'GalleryTitle' } 30 'the shelf did not show'
    "launched, first run done, shelf shows '$($title.Current.Name)' (pid $((Get-Process DinoReader.App).Id -join ','))"
}
