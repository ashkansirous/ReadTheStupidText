<#
.SYNOPSIS
  Checks that the user's real phone is connected (USB first, else wireless adb), and pairs/connects it when given the details (Slice 165).
.DESCRIPTION
  Looks at `adb mdns services` and `adb devices` first. If a USB phone is plugged in it prints its serial and exits 0 with no pairing; else a ready wireless phone is printed and exits 0.
  If neither exists, it prints exactly what to ask the user for (pairing IP:port + 6-digit code, then the connect IP:port) and exits 2.
  With -PairAddress/-PairCode it runs `adb pair`; with -ConnectAddress it runs `adb connect` (always, even with a USB phone plugged in). A USB phone that is unauthorized or offline gets one line (unlock it, accept the USB debugging prompt) instead of the ask (Write-PairingAsk). The pairing ask is the last resort.
  -Serial limits the check to one exact serial (a serial that is not there gives the same ask).
#>
param(
    [string]$Serial,
    [string]$PairAddress,
    [string]$PairCode,
    [string]$ConnectAddress
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$explicitRequest = [bool]($PairAddress -or $ConnectAddress)   # review 3: an explicit pair/connect always runs, even with a USB phone plugged in
$phone = if ($explicitRequest) { $null } else { Find-Phone $Serial }
if (-not $phone) {
    $ErrorActionPreference = 'Continue'
    if ($PairAddress -and $PairCode) { & $script:Adb pair $PairAddress $PairCode }
    if ($ConnectAddress) { & $script:Adb connect $ConnectAddress }
    $ErrorActionPreference = 'Stop'
    # after an explicit -ConnectAddress, report that address (or the mDNS name adb lists for it) when adb shows it connected, not a USB phone
    $phone = if ($ConnectAddress) { Select-ConnectedAddress @(Get-AdbDevices) $ConnectAddress } else { Find-Phone $Serial }
}
if (-not $phone) {
    Write-PairingAsk $Serial   # says the USB line instead when a USB phone is listed unauthorized or offline (review 4)
    exit 2
}
Write-Host "Phone ready over $(if ((Get-DeviceKind $phone) -eq $script:KindUsb) { 'USB' } else { 'wireless adb' }): $phone"
exit 0
