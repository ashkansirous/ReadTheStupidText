# Launches Dino Reader, maximises it and walks the first-run pages (English, the offline phone voice) to the shelf.
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
    $voice = Wait-Until { Find-UiaByName -Root (& $window) -Name 'Continue with the phone voice' -ControlType Text } 30 'offline voice page did not appear'
    Invoke-Uia $voice
    $skip = Wait-Until { Find-UiaByName -Root (& $window) -Name 'Skip, start reading' -ControlType Button } 30 'ready page did not appear'
    Invoke-Uia $skip
    $title = Wait-Until { Find-UiaById (& $window) 'GalleryTitle' } 30 'the shelf did not show'
    "launched, first run done, shelf shows '$($title.Current.Name)' (pid $((Get-Process DinoReader.App).Id -join ','))"
}
