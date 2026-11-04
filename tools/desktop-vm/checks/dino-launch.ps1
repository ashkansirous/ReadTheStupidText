# Launches Dino Reader, maximises it and walks the first-run pages (English, two test books from the picker, the offline phone voice) to the shelf.
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"
Invoke-Check -ResultFile $ResultFile -Body {
    $pkg = Get-InstalledPackage $PackageName
    $window = { Get-AppWindows 'DinoReader.App' | Select-Object -First 1 }
    [NativeKeys]::Chord(0x1B)   # closes the Start menu the VM checkpoint was taken with
    Start-PackagedApp $pkg
    $w = Wait-Until $window 30 'Dino Reader window did not open'
    Wait-Until { Find-UiaById (& $window) 'FirstRunLanguage' } 30 'first-run language page did not show' | Out-Null
    Invoke-Uia (Find-UiaById (& $window) 'Maximize')   # the default window cuts off the first-run buttons
    Start-Sleep -Seconds 2
    Invoke-Uia (Find-UiaByName -Root (& $window) -Name 'English' -ControlType Pane)
    Start-Sleep -Seconds 1
    Invoke-Uia (Find-UiaByName -Root (& $window) -Name 'Continue' -ControlType Pane)
    # Slice 261: no book ships in the app, so the picker comes next. This package is built with -p:DinoTestBooks=true, so the picker lists the four test books (not the live store): tick two, Continue.
    Wait-Until { Find-UiaById (& $window) 'FirstBooksPicker' } 30 'first-books picker did not show' | Out-Null
    foreach ($card in 'FirstBookCard-0', 'FirstBookCard-1') {
        Invoke-Uia (Wait-Until { Find-UiaById (& $window) $card } 30 "picker card $card did not show")
        Start-Sleep -Milliseconds 500
    }
    Invoke-Uia (Find-UiaByName -Root (& $window) -Name 'Continue' -ControlType Pane)
    $voice = Wait-Until { Find-UiaByName -Root (& $window) -Name 'Continue with the phone voice' -ControlType Text } 30 'offline voice page did not appear'
    Invoke-Uia $voice
    $skip = Wait-Until { Find-UiaByName -Root (& $window) -Name 'Skip, start reading' -ControlType Button } 30 'ready page did not appear'
    Invoke-Uia $skip
    $title = Wait-Until { Find-UiaById (& $window) 'GalleryTitle' } 30 'the shelf did not show'
    "launched, first run done, shelf shows '$($title.Current.Name)' (pid $((Get-Process DinoReader.App).Id -join ','))"
}
