<#
.SYNOPSIS
  Checks that the user's real phone is connected over wireless adb, and pairs/connects it when given the details (Slice 165).
.DESCRIPTION
  Looks at `adb mdns services` and `adb devices` first. If a wireless phone is ready it prints its serial and exits 0.
  If not, it prints exactly what to ask the user for (pairing IP:port + 6-digit code, then the connect IP:port) and exits 2.
  With -PairAddress/-PairCode it runs `adb pair`; with -ConnectAddress it runs `adb connect`. It never uses or offers USB.
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

$phone = Find-WirelessPhone $Serial
if (-not $phone) {
    $ErrorActionPreference = 'Continue'
    if ($PairAddress -and $PairCode) { & $script:Adb pair $PairAddress $PairCode }
    if ($ConnectAddress) { & $script:Adb connect $ConnectAddress }
    $ErrorActionPreference = 'Stop'
    $phone = Find-WirelessPhone $Serial
}
if (-not $phone) {
    Write-PairingAsk
    exit 2
}
Write-Host "Phone ready over wireless adb: $phone"
exit 0
