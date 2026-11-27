<#
.SYNOPSIS
  One session or agent holds the real phone at a time (Slice 206).
.DESCRIPTION
  -Take -Owner <name> [-Minutes 120]  writes the lease (renews it when the same owner holds it); exits 4 naming the holder and its expiry when another owner holds it.
  -Renew -Owner <name> [-Minutes 120] moves the expiry of the lease this owner holds to that many minutes from now; exits 4 when someone else holds it or nobody does.
                                      Scripts also renew the caller's own lease by themselves when under 15 minutes remain.
  -Release [-Owner <name>]            removes the lease; -Owner defaults to $env:RTST_PHONE_OWNER, and with neither it says so and exits 2 (Slice 393 item 4); exits 4 when another owner holds a live one. An expired lease is free. Then (Slice 225) writes back the stay_on_while_plugged_in value
                                      recorded when this owner took the lease (nothing recorded by this owner: nothing changed). The screen is left to the phone's own timeout.
  -Show                               (also what no parameters at all means) prints who holds the phone, or that it is free. A corrupt lease file is named as such, with its age and when it frees itself (Slice 216).
  -Clear -Corrupt                     removes the lease file only when it is corrupt (does not parse); a valid lease, live or expired, is never touched. Exits 0 when the file was cleared or there was nothing to clear. With no phone connected it checks every phone lease file instead (no pairing needed).
  The lease is a file in <worktrees folder>\.phone\ (one per phone, keyed by `getprop ro.serialno`, not by the adb name), taken under a named mutex and written through a temp file and a rename. Scripts that touch the phone (audio-timing,
  reset-dev-app, tap) exit 4 when a live lease belongs to someone else than $env:RTST_PHONE_OWNER; set that variable to your owner name after -Take.
  No lease at all means every script is allowed, as before.
#>
[CmdletBinding(DefaultParameterSetName = 'Show')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Take')][switch]$Take,
    [Parameter(Mandatory, ParameterSetName = 'Renew')][switch]$Renew,
    [Parameter(Mandatory, ParameterSetName = 'Release')][switch]$Release,
    [Parameter(ParameterSetName = 'Show')][switch]$Show,   # Slice 393 item 4: no parameters at all is -Show, not a parameter-set error
    [Parameter(Mandatory, ParameterSetName = 'Clear')][switch]$Clear,
    [Parameter(Mandatory, ParameterSetName = 'Clear')][switch]$Corrupt,
    [Parameter(Mandatory, ParameterSetName = 'Take')][Parameter(Mandatory, ParameterSetName = 'Renew')][Parameter(ParameterSetName = 'Release')][string]$Owner,
    [Parameter(ParameterSetName = 'Take')][Parameter(ParameterSetName = 'Renew')][ValidateRange(1, 1440)][int]$Minutes = 120,
    [string]$Serial
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

# Slice 393 item 4: -Release without -Owner releases as the session's own owner name ($env:RTST_PHONE_OWNER, set after -Take); with neither it says so and exits 2.
if ($Release -and -not $Owner) {
    $Owner = [Environment]::GetEnvironmentVariable($script:LeaseOwnerVariable)
    if (-not $Owner) { Write-Host "-Release needs -Owner <name> (or `$env:$($script:LeaseOwnerVariable) set to the owner name used for -Take)."; exit 2 }
}
# Slice 393 review 5: -Release / -Renew with no phone connected work on this owner's lease file(s) directly. The screen timeout is not written back (no adb): the phone's own record stays for the next take or -Release with the phone.
function Invoke-LeaseFileOnly {
    $dir = Get-LeaseDir 'phone'
    $keys = @(if (Test-Path $dir) { Get-ChildItem $dir -Filter 'lease-*.json' -File | ForEach-Object { $_.BaseName.Substring('lease-'.Length) } })
    $mine = @($keys | Where-Object { $held = Read-Lease $_; $held -and "$($held.Owner)" -ceq $Owner })
    if (-not $mine.Count) { Write-Host "No phone is connected and no lease file is held by '$Owner'. Nothing to $(if ($Release) { 'release' } else { 'renew' })."; return $(if ($Release) { 0 } else { $script:ExitLeaseHeld }) }
    $failed = $false
    foreach ($key in $mine) {
        if ($Release) { if (Remove-Lease $key $Owner) { Write-Host "No phone connected: lease file for $key released by '$Owner' (the screen timeout was not written back; no adb).$(if (Read-StayOnOriginal $key) { ' Its stay-on record is kept: the next lease take or -Release with the phone connected writes it back, unless the record is over 24 h old, which discards it.' })" } else { Write-Host "No phone connected: lease file for $key is now held by someone else, not by '$Owner'. Not releasing it."; $failed = $true } }
        elseif ((Update-Lease $key $Owner $Minutes) -eq 'Renewed') { Write-Host "No phone connected: lease of '$Owner' on $key renewed for $Minutes min." }
        else { Write-Host "No phone connected: lease of '$Owner' on $key expired or was taken by someone else. Nothing renewed."; $failed = $true }
    }
    return $(if ($failed) { $script:ExitLeaseHeld } else { 0 })
}
if ($Serial -and -not (Test-PhoneSerialShape $Serial)) { Write-Host (Get-PhoneSerialShapeText $Serial); exit 2 }   # Slice 442: a serial that cannot be a phone is refused before anything is leased
if ($Clear) {
    # A corrupt file can be removed with or without a phone connected: check every phone lease file
    # (a corrupt lease left under another or an earlier phone's key would otherwise stay up to 24 h).
    $results = @(Clear-AllCorruptLeases)
    $cleared = @($results | Where-Object { $_.Result -eq 'Cleared' })
    $unreadable = @($results | Where-Object { $_.Result -eq 'Unreadable' })
    foreach ($r in $cleared) { Write-Host "The corrupt lease file for $($r.Key) was removed." }
    foreach ($r in $unreadable) { Write-Host "The lease file for $($r.Key) cannot be read, so it is not proven corrupt. Not removing it." }
    if (-not $cleared.Count -and -not $unreadable.Count) { Write-Host 'No corrupt phone lease file found.' }
    exit $(if ($unreadable.Count) { 1 } else { 0 })
}
if (-not $Serial) {
    if ($Release -or $Renew) { try { $Serial = Find-Phone '' } catch { $Serial = $null } }   # review pass 2 item 6: a missing adb or a failed adb server means no phone, so -Release/-Renew still reach the lease-file path
    else { $Serial = Find-Phone '' }   # PR #1439 check 1 item 1: -Take and -Show surface the adb error instead of asking to pair a phone
}
# Slice 442: below the -Clear block, which needs no phone (a stalled adb must not delay or break that cleanup)
if (-not $Serial -and ($Release -or $Renew)) { exit (Invoke-LeaseFileOnly) }   # Slice 393 review 5: the lease file needs no phone, so an unplugged phone never keeps it held
if (-not $Serial) { Write-PairingAsk $Serial; exit 2 }
$adbSerial = $Serial
$Serial = Get-PhoneLeaseKey $Serial  # the phone's own serial number, the same whichever adb name it was found under
$held = Read-Lease $Serial
if ($Show -or $PSCmdlet.ParameterSetName -eq 'Show') {
    if ($held) { Write-Host "Phone $Serial is leased by $(Format-LeaseHolder $held)." } else { Write-Host "Phone $Serial is free." }
    exit 0
}
if ($Take) {
    $wasMine = [bool]($held -and $held.Owner -ceq $Owner)
    if (-not (Request-Lease $Serial $Owner $Minutes)) { Write-Host "Phone $Serial is already leased by $(Format-LeaseHolder (Read-Lease $Serial)). Not taking it."; exit $script:ExitLeaseHeld }
    if (-not $wasMine) {
        # Review 2: the screen timeout as it is at the moment this owner takes the phone; -Release puts it back.
        try { if (Save-StayOnForLease $adbSerial $Serial $Owner) { Write-Host "Recorded $($script:StayOnSetting) for '$Owner'; -Release writes it back." } }
        catch { Write-Host "WARNING: could not record the screen timeout: $($_.Exception.Message)" }
    }
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
# Slice 225 item 11: the holder gives the screen timeout back (the value recorded when the lease was taken, and only if the phone still holds the value this tooling set), then releases (Close-PhoneLease; a restore failure is said, never a failed release). The screen itself is left alone.
if (-not (Close-PhoneLease $adbSerial $Serial $Owner)) { Write-Host "Phone $Serial is leased by $(Format-LeaseHolder (Read-Lease $Serial)), not by '$Owner'. Not releasing it."; exit $script:ExitLeaseHeld }
Write-Host "Phone $Serial released by '$Owner'."
exit 0
