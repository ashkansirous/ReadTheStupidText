# Ctrl+Win+R reads the clipboard when nothing selectable is focused: the clipboard holds a sentinel, the hotkey is
# pressed, and the input log (one row per state change) must show a Hotkey read of that sentinel. The VM has no audio
# device, so the final state is reported, not asserted.
param([string] $ResultFile, [Parameter(Position = 0)] [string] $PackageName)
. "$PSScriptRoot\_lib.ps1"
Invoke-Check -ResultFile $ResultFile -Body {
    $pkg = Get-InstalledPackage $PackageName
    if (-not (Get-Process -Name ReadTheStupidText.App -ErrorAction SilentlyContinue)) { throw 'the app is not running' }
    $sentinel = 'hotkey sentinel ' + ([guid]::NewGuid().ToString('N').Substring(0, 8) -replace '\d', 'x')
    [System.Windows.Forms.Clipboard]::SetText($sentinel)
    Start-Sleep -Seconds 1
    [NativeKeys]::Chord(0x11, 0x5B, 0x52)
    $rows = Wait-Until { @((Get-RtstLogText $pkg) -split "`n" | Where-Object { $_ -match "`tHotkey`t" -and $_ -like "*$sentinel*" }) } 15 "no Hotkey row with the sentinel '$sentinel' in the input log"
    $states = ($rows | ForEach-Object { ($_ -split "`t")[3] }) -join ' > '
    "hotkey read the clipboard ($states)"
}
