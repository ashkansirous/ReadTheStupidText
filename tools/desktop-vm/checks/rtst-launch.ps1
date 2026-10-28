# Launches the installed app, answers its first-run language window (English) and checks it is running with its log started.
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"
Invoke-Check -ResultFile $ResultFile -Body {
    $pkg = Get-InstalledPackage $PackageName
    [NativeKeys]::Chord(0x1B)   # closes the Start menu the VM checkpoint was taken with
    Start-PackagedApp $pkg
    Wait-Until { Get-Process -Name ReadTheStupidText.App -ErrorAction SilentlyContinue } 30 'process did not start' | Out-Null
    $language = Wait-Until { Get-AppWindows 'ReadTheStupidText.App' | Where-Object { $_.Current.Name -like '*Language*' } | Select-Object -First 1 } 30 'first-run language window did not open'
    Invoke-Uia (Find-UiaByName -Root $language -Name 'English' -ControlType ListItem)
    Start-Sleep -Seconds 2
    try { Invoke-Uia (Find-UiaByName -Root $language -Name 'Close' -ControlType Button) } catch { }   # the window is gone before Invoke returns
    Wait-Until { -not (Get-AppWindows 'ReadTheStupidText.App' | Where-Object { $_.Current.Name -like '*Language*' }) } 15 'language window did not close' | Out-Null
    $proc = Wait-Until { Get-Process -Name ReadTheStupidText.App -ErrorAction SilentlyContinue } 10 'app exited after the language window closed'
    $logDir = Join-Path $env:LOCALAPPDATA "Packages\$($pkg.PackageFamilyName)\TempState\logs"
    $logFile = Wait-Until { Get-ChildItem $logDir -Filter 'system-*.log' -ErrorAction SilentlyContinue | Select-Object -First 1 } 15 'no system log file'
    "running (pid $(@($proc)[0].Id)), language chosen, $($logFile.Name) started"
}
