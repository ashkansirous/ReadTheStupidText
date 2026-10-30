<#
.SYNOPSIS
  One session or agent holds the real phone at a time (Slice 206).
.DESCRIPTION
  -Take -Owner <name> [-Minutes 120]  writes the lease (renews it when the same owner holds it); exits 4 naming the holder and its expiry when another owner holds it.
  -Renew -Owner <name> [-Minutes 120] moves the expiry of the lease this owner holds to that many minutes from now; exits 4 when someone else holds it or nobody does.
                                      Scripts also renew the caller's own lease by themselves when under 15 minutes remain.
  -Release -Owner <name>              removes the lease; exits 4 when another owner holds a live one. An expired lease is free.
  -Show                               prints who holds the phone, or that it is free.
  The lease is a file in <worktrees folder>\.phone\ (one per phone, keyed by `getprop ro.serialno`, not by the adb name), taken under a named mutex and written through a temp file and a rename. Scripts that touch the phone (audio-timing,
  reset-dev-app, tap) exit 4 when a live lease belongs to someone else than $env:RTST_PHONE_OWNER; set that variable to your owner name after -Take.
  No lease at all means every script is allowed, as before.
#>
param(
    [Parameter(Mandatory, ParameterSetName = 'Take')][switch]$Take,
    [Parameter(Mandatory, ParameterSetName = 'Renew')][switch]$Renew,
    [Parameter(Mandatory, ParameterSetName = 'Release')][switch]$Release,
    [Parameter(Mandatory, ParameterSetName = 'Show')][switch]$Show,
    [Parameter(Mandatory, ParameterSetName = 'Take')][Parameter(Mandatory, ParameterSetName = 'Renew')][Parameter(Mandatory, ParameterSetName = 'Release')][string]$Owner,
    [Parameter(ParameterSetName = 'Take')][Parameter(ParameterSetName = 'Renew')][ValidateRange(1, 1440)][int]$Minutes = 120,
    [string]$Serial
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

if (-not $Serial) { $Serial = Find-WirelessPhone '' }
if (-not $Serial) { Write-PairingAsk; exit 2 }
$Serial = Get-PhoneLeaseKey $Serial  # the phone's own serial number, the same whichever adb name it was found under
$held = Read-Lease $Serial
if ($Show) {
    if ($held) { Write-Host "Phone $Serial is leased by $(Format-LeaseHolder $held)." } else { Write-Host "Phone $Serial is free." }
    exit 0
}
if ($Take) {
    if (-not (Request-Lease $Serial $Owner $Minutes)) { Write-Host "Phone $Serial is already leased by $(Format-LeaseHolder (Read-Lease $Serial)). Not taking it."; exit $script:ExitLeaseHeld }
    Write-Host "Phone $Serial leased to '$Owner' for $Minutes min. Set `$env:$($script:LeaseOwnerVariable) = '$Owner' in the session that runs the phone scripts."
    exit 0
}
if ($Renew) {
    $renewed = Update-Lease $Serial $Owner $Minutes
    if ($renewed -ne 'Renewed') {
        $who = if ($renewed -eq 'Other') { "leased by $(Format-LeaseHolder (Read-Lease $Serial))" } else { 'not leased' }
        Write-Host "Phone $Serial is $who, not by '$Owner'. Nothing to renew."
        exit $script:ExitLeaseHeld
    }
    Write-Host "Phone $Serial lease of '$Owner' renewed for $Minutes min."
    exit 0
}
if (-not (Remove-Lease $Serial $Owner)) { Write-Host "Phone $Serial is leased by $(Format-LeaseHolder (Read-Lease $Serial)), not by '$Owner'. Not releasing it."; exit $script:ExitLeaseHeld }
Write-Host "Phone $Serial released by '$Owner'."
exit 0
