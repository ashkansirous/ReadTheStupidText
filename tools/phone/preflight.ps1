<#
.SYNOPSIS
  One go/no-go check before an unattended run on the real phone (Slice 206).
.DESCRIPTION
  Prints one line per check and ends with `PREFLIGHT GO` (exit 0) or `PREFLIGHT NO-GO: <reasons>` (exit 1). Not connected over wireless adb prints the pairing ask and exits 2.
  Checks: connected, charging, screen lock None or Swipe (a PIN, pattern or password is NO-GO: ask the user to set Screen lock to None or Swipe; a lock state that
  cannot be read, or reads contradictory, is NO-GO too: GO needs a positive "no secure keyguard" read made in this run), stay-awake-while-charging (the bit of the charger in use must be set in stay_on_while_plugged_in: 1 AC, 2 USB, 4 wireless; with -Fix a missing bit is fixed by setting 7, it stays on), free space on /data (warn under 2 GB, NO-GO under 0.5 GB), the .dbg dev app(s)
  installed with their versionName (with -App both a missing app is a warning, and NO-GO only when neither is installed), and the phone lease (held by
  $env:RTST_PHONE_OWNER; free is a warning, because an unleased phone can be taken by another session mid-run: run lease.ps1 -Take). Read-only on the phone unless -Fix is given, which sets
  stay_on_while_plugged_in and clears device leftovers of a dead check (dataSync timeout, compat flag, dev-app network block; Slice 225). The stay-on value it found is
  printed; the value recorded when the lease was taken (lease.ps1 -Take) is what -Release writes back. -Fix refuses while another session's lease is live. Every check runs inside a try/catch: a dropped link or a timeout is a FAIL with its
  message, so the last line is always GO or NO-GO.
#>
param(
    [string]$Serial,
    [switch]$Fix,
    [ValidateSet('rtst', 'dino', 'both')][string]$App = 'both'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$script:StayOnAll = 7  # bit mask of settings global stay_on_while_plugged_in: 1 AC, 2 USB, 4 wireless
$script:LowSpaceKb = 2GB / 1KB
$script:NoSpaceKb = 512MB / 1KB   # under this a pack download cannot work at all: FAIL (the runner stops on it)
$script:Reasons = New-Object 'System.Collections.Generic.List[string]'

function Write-Check([string]$Name, [string]$Status, [string]$Detail) {
    Write-Host ("  [{0}] {1}: {2}" -f $Status, $Name, $Detail)
    if ($Status -eq 'FAIL') { $script:Reasons.Add("${Name}: $Detail") }
}

# One check; an exception (adb timeout, unreadable output) becomes a FAIL instead of ending the script without its GO/NO-GO line.
function Invoke-Check([string]$Name, [scriptblock]$CheckBody) {
    try { & $CheckBody } catch { Write-Check $Name 'FAIL' "check failed: $($_.Exception.Message)" }
}

function Test-Charging([string]$Phone) {
    $power = Get-PowerState $Phone
    if ($power.Powered) { Write-Check 'charging' 'OK' "on charger, $($power.Wakefulness)" }
    else { Write-Check 'charging' 'FAIL' "ON BATTERY ($($power.Wakefulness)): plug the phone in" }
}

function Test-ScreenLock([string]$Phone) {
    $window = (Invoke-Adb $Phone @('shell', 'dumpsys window | grep -i keyguard')) -join "`n"
    $disabled = (Invoke-Adb $Phone @('shell', 'cmd lock_settings get-disabled')) -join ' '
    switch (Resolve-LockState $window $disabled) {
        'Open' { Write-Check 'screen lock' 'OK' 'None or Swipe (no secure keyguard)' }
        'Secure' { Write-Check 'screen lock' 'FAIL' 'a PIN, pattern or password is set: ask the user to set Screen lock to None or Swipe' }
        default { Write-Check 'screen lock' 'FAIL' 'could not read the lock state positively (no isKeyguardSecure=false in dumpsys window): ask the user to confirm Screen lock is None or Swipe' }
    }
}

function Test-StayAwake([string]$Phone) {
    if ($Fix) {
        # Round 3: a dead run's record is healed by the same restore rule before this run reads the setting, so a leftover 7 is not mistaken for the user's own choice.
        try { $healed = Invoke-StayOnRestore $Phone (Get-PhoneLeaseKey $Phone) ''; if ($healed -and $healed.Restored) { Write-Host "   put back stay_on_while_plugged_in to $($healed.Value) for a dead run's record" } }
        catch { Write-Host "   WARNING: could not heal a dead run's stay-on record: $($_.Exception.Message)" }
    }
    $value = "$(Invoke-Adb $Phone @('shell', 'settings', 'get', 'global', 'stay_on_while_plugged_in'))".Trim()
    $bit = Get-ChargerStayOnBit "$(Invoke-Adb $Phone @('shell', 'dumpsys battery'))"
    # Slice 225 item 11 / review 2: only printed here (the night's preflight.md can quote it). The value is recorded when an owner takes the lease (lease.ps1 -Take), never by a plain preflight.
    Write-Host "   found $($script:StayOnSetting) = '$value'"
    if ($bit -eq 0) { Write-Check 'stay awake' 'WARN' "not on a charger, so stay_on_while_plugged_in ($value) cannot apply"; return }
    if (Test-StayOnCovers $value $bit) { Write-Check 'stay awake' 'OK' "stay_on_while_plugged_in = $value (covers this charger, bit $bit)"; return }
    if (-not $Fix) { Write-Check 'stay awake' 'FAIL' "stay_on_while_plugged_in = '$value' does not cover this charger (bit $bit): the screen can sleep. Run preflight.ps1 -Fix to set it to $($script:StayOnAll)"; return }
    Invoke-Adb $Phone @('shell', 'settings', 'put', 'global', 'stay_on_while_plugged_in', $script:StayOnAll) | Out-Null
    Write-Check 'stay awake' 'OK' "was '$value', set stay_on_while_plugged_in to $($script:StayOnAll) (-Fix; it stays on, turn it off in Developer options if unwanted)"
}

function Test-FreeSpace([string]$Phone) {
    $availableKb = ConvertFrom-DfAvailableKb (Invoke-Adb $Phone @('shell', 'df', '-k', '/data'))
    if ($availableKb -lt 0) { Write-Check 'free space' 'WARN' 'could not read df -k /data'; return }
    $gb = [math]::Round($availableKb / 1MB, 1)
    if ($availableKb -lt $script:NoSpaceKb) { Write-Check 'free space' 'FAIL' "$gb GB free on /data (under 0.5 GB: no room for a voice pack)"; return }
    if ($availableKb -lt $script:LowSpaceKb) { Write-Check 'free space' 'WARN' "$gb GB free on /data (under 2 GB: a voice pack download may fail)" }
    else { Write-Check 'free space' 'OK' "$gb GB free on /data" }
}

# Returns $true when the app is installed. A missing app is a FAIL when it is the one asked for, a WARN when -App both asked for two.
function Test-DevApp([string]$Phone, [string]$Key, [string]$MissingStatus) {
    $package = Get-DevPackage $Key
    $info = (Invoke-Adb $Phone @('shell', 'dumpsys', 'package', $package)) -join "`n"
    if ($info -match 'versionName=(\S+)') { Write-Check "$Key dev app" 'OK' "$package $($Matches[1])"; return $true }
    Write-Check "$Key dev app" $MissingStatus "$package is not installed: run audio-timing.ps1 -App $Key (it builds and installs it)"
    return $false
}

function Test-DevApps([string]$Phone) {
    $keys = if ($App -eq 'both') { 'rtst', 'dino' } else { @($App) }
    $missing = if ($App -eq 'both') { 'WARN' } else { 'FAIL' }
    $installed = 0
    foreach ($key in $keys) {
        $found = Invoke-Check "$key dev app" { Test-DevApp $Phone $key $missing }
        if ($found -eq $true) { $installed++ }
    }
    if ($installed -eq 0 -and $App -eq 'both') { Write-Check 'dev apps' 'FAIL' 'neither dev app is installed' }
}

# Slice 225 item 1: a leftover dataSync timeout, compat flag or dev-app network block from a check that died. Read-only without -Fix (a WARN); with -Fix restore-device-state.ps1's step clears it.
function Test-DeviceLeftovers([string]$Phone) {
    $packages = @($script:DevApps.Values | ForEach-Object { $_.Package })
    if (-not $Fix) { Write-Check 'device leftovers' 'OK' 'not looked for without -Fix (restore-device-state.ps1 clears them)'; return }
    $result = Clear-DeviceLeftovers $Phone $packages
    if ($result.Remaining.Count -gt 0) { Write-Check 'device leftovers' 'FAIL' "still set after the clear: $($result.Remaining -join '; ')"; return }
    if ($result.Unknown.Count -gt 0) { Write-Check 'device leftovers' 'WARN' "the block list could not be read back, so a dev-app network block is unknown (not failed): $($result.Unknown -join '; ')"; return }
    if ($result.Leftovers.Count -gt 0) { Write-Check 'device leftovers' 'WARN' "found and cleared: $($result.Leftovers -join '; '). The dev app may need reset-dev-app.ps1 -Level auto (restore-device-state.ps1 does both)"; return }
    Write-Check 'device leftovers' 'OK' 'none (no dataSync timeout, no dev-app network block)'
}

function Test-Lease([string]$Phone) {
    $held = Read-Lease (Get-PhoneLeaseKey $Phone)
    $me = [Environment]::GetEnvironmentVariable($script:LeaseOwnerVariable)
    if (-not $held) { Write-Check 'phone lease' 'WARN' 'no lease held: another session can take the phone mid-run (lease.ps1 -Take -Owner <name>, then set $env:RTST_PHONE_OWNER)' }
    elseif ($held.Owner -eq $me) { Write-Check 'phone lease' 'OK' "held by this session ($(Format-LeaseHolder $held))" }
    else { Write-Check 'phone lease' 'FAIL' "held by $(Format-LeaseHolder $held), not by this session ('$me')" }
}

$phone = $null
try { $phone = Find-WirelessPhone $Serial } catch { Write-Host "adb failed: $($_.Exception.Message)"; Write-Host "PREFLIGHT NO-GO: adb: $($_.Exception.Message)"; exit 1 }
if (-not $phone) { Write-PairingAsk; exit 2 }
Write-Host "Preflight for $phone"
Write-Check 'connection' 'OK' 'wireless adb'
Invoke-Check 'charging' { Test-Charging $phone }
Invoke-Check 'screen lock' { Test-ScreenLock $phone }
Invoke-Check 'stay awake' { Test-StayAwake $phone }
Invoke-Check 'free space' { Test-FreeSpace $phone }
Invoke-Check 'device leftovers' { Test-DeviceLeftovers $phone }
Test-DevApps $phone
Invoke-Check 'phone lease' { Test-Lease $phone }
if ($script:Reasons.Count -eq 0) { Write-Host 'PREFLIGHT GO'; exit 0 }
Write-Host "PREFLIGHT NO-GO: $($script:Reasons -join '; ')"
exit 1
