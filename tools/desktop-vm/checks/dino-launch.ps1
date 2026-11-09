# after: dino-onboarding-keys
# Launches Dino Reader, maximises it and walks the first-run pages (English, two test books from the picker, "Ready to read", "See what Dino can do", the intro's "Skip all") to the shelf. Slice 325: dino-onboarding-keys runs first and does the same walk with the keyboard; this check then only finds the shelf.
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
    Wait-Until { Find-UiaById (& $window) 'FirstRunLanguage' } 30 'first-run language page did not show' | Out-Null
    Invoke-Uia (Find-UiaById (& $window) 'Maximize')   # the default window cuts off the first-run buttons
    Start-Sleep -Seconds 2
    Invoke-Uia (Find-UiaByName -Root (& $window) -Name 'English' -ControlType Pane)
    Start-Sleep -Seconds 1
    Invoke-Uia (Find-UiaByName -Root (& $window) -Name 'Continue' -ControlType Pane)
    # Slice 261: no book ships in the app, so the picker comes next. This package is built with -p:DinoTestBooks=true, so the picker lists the four test books (not the live store): tick two, Continue.
    Wait-Until { Find-UiaById (& $window) 'FirstBooksPicker' } 30 'first-books picker did not show' | Out-Null
    # Each card's tick is a real button (FirstBookTick-n, Invoke pattern); after each tick wait for the count line to change, so the next one is never sent before the page took the first.
    $count = { (Find-UiaById (& $window) 'FirstBooksCount').Current.Name }
    foreach ($tick in 'FirstBookTick-0', 'FirstBookTick-1') {
        $before = & $count
        Invoke-Uia (Wait-Until { Find-UiaById (& $window) $tick } 30 "picker tick $tick did not show")
        Wait-Until { (& $count) -ne $before } 10 "count line stayed '$before' after $tick" | Out-Null
    }
    Wait-Until { (& $count) -match 'picked' } 10 "count line did not read the picked state (reads '$(& $count)')" | Out-Null
    Invoke-Uia (Find-UiaByName -Root (& $window) -Name 'Continue' -ControlType Pane)
    # Slice 278 part B: there is no voice download page any more; the "Ready to read" page follows the picker.
    # Slice 325 (mock O5/OW5): the Ready page has one coral button, "See what Dino can do" (resx Intro_SeeWhat); it opens the intro, whose top bar has "Skip all" (ends it at once, to the shelf).
    Wait-Until { Find-UiaVisibleById (& $window) 'FirstRunReady' } 30 'ready page did not appear' | Out-Null
    Invoke-Uia (Wait-Until { Find-UiaByName -Root (& $window) -Name 'See what Dino can do' -ControlType Pane } 30 'the Ready page has no "See what Dino can do" button')
    Invoke-Uia (Wait-Until { Find-UiaExact (& $window) 'Skip all' } 30 'the intro did not open (no "Skip all" in its top bar)')
    $title = Wait-Until { Find-UiaById (& $window) 'GalleryTitle' } 30 'the shelf did not show'
    "launched, first run done, shelf shows '$($title.Current.Name)' (pid $((Get-Process DinoReader.App).Id -join ','))"
}
