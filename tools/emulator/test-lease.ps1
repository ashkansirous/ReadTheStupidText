<#
.SYNOPSIS
  Self-test for the emulator lease in tools/lease-common.ps1 (Slice 207): take, held, expiry, a dead owner process, the same owner name in two
  processes, release, the wait with a timeout. The phone kind is covered by tools/phone/test-phone-lib.ps1.
  Run it in both `powershell` (5.1) and `pwsh`. Needs no device; the lease files go to a temp folder.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\lease-common.ps1')
$script:EmulatorLeaseDir = Join-Path ([IO.Path]::GetTempPath()) ('rtst-emu-lease-test-' + [guid]::NewGuid().ToString('N'))
$script:LeaseNoticeSeconds = 1

$failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

function Start-LeaseHolder([string]$Key, [string]$Owner, [int]$Seconds) {
    $engine = (Get-Process -Id $PID).Path
    $command = ". '$PSScriptRoot\..\lease-common.ps1'; `$script:EmulatorLeaseDir='$($script:EmulatorLeaseDir)'; if (Request-Lease '$Key' '$Owner' 30 0 'emulator') { Start-Sleep -Seconds $Seconds }"
    return Start-Process $engine -ArgumentList '-NoProfile', '-Command', $command -PassThru -WindowStyle Hidden
}

function Wait-ForLeaseFile([string]$Key) {
    $deadline = (Get-Date).AddSeconds(30)
    while (-not (Test-Path (Get-LeasePath $Key 'emulator')) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200 }
}

try {
    $key = 'emulator-5554'
    $now = Get-UnixNow
    Assert-Equal 'no lease is free' $null (Read-Lease $key $now 'emulator')
    Assert-Equal 'take' $true (Request-Lease $key 'agent-a' 30 $now 'emulator')
    Assert-Equal 'owner recorded' 'agent-a' (Read-Lease $key ($now + 1) 'emulator').Owner
    Assert-Equal 'owner process recorded' $PID (Read-Lease $key ($now + 1) 'emulator').OwnerPid
    Assert-Equal 'file is under the emulator folder' $true ((Get-LeasePath $key 'emulator').StartsWith($script:EmulatorLeaseDir))
    Assert-Equal 'another owner is refused' $false (Request-Lease $key 'agent-b' 30 ($now + 5) 'emulator')
    Assert-Equal 'same owner in the same process renews' $true (Request-Lease $key 'agent-a' 30 ($now + 10) 'emulator')
    Assert-Equal 'renewed expiry' ($now + 10 + 1800) (Read-Lease $key ($now + 11) 'emulator').Expires
    Assert-Equal 'renew by another owner' 'Other' (Update-Lease $key 'agent-b' 30 ($now + 12) 'emulator')
    Assert-Equal 'renew by the owner' 'Renewed' (Update-Lease $key 'agent-a' 30 ($now + 13) 'emulator')
    Assert-Equal 'another owner cannot release' $false (Remove-Lease $key 'agent-b' ($now + 14) 'emulator')
    Assert-Equal 'expired lease is free' $null (Read-Lease $key ($now + 13 + 1800) 'emulator')
    Assert-Equal 'expired: another owner takes it' $true (Request-Lease $key 'agent-b' 30 ($now + 13 + 1800) 'emulator')
    Assert-Equal 'release by the owner' $true (Remove-Lease $key 'agent-b' ($now + 13 + 1801) 'emulator')
    Assert-Equal 'released is free' $null (Read-Lease $key ($now + 13 + 1802) 'emulator')
    Assert-Equal 'a second emulator is a separate lease' $true (Request-Lease 'emulator-5556' 'agent-c' 30 0 'emulator')
    Remove-Lease 'emulator-5556' 'agent-c' 0 'emulator' | Out-Null

    # An owner process that is gone no longer holds the lease; the same owner name in another live process does.
    $holder = Start-LeaseHolder $key 'shared-name' 120
    try {
        Wait-ForLeaseFile $key
        Assert-Equal 'a live holder process blocks another owner' $false (Request-Lease $key 'agent-a' 30 0 'emulator')
        Assert-Equal 'a live holder blocks the same owner name in another process' $false (Request-Lease $key 'shared-name' 30 0 'emulator')
        Assert-Equal 'a renewal by the same name in another process is refused' 'Other' (Update-Lease $key 'shared-name' 30 0 'emulator')
        $heldBy = Wait-ForLease $key 'agent-a' 30 0.03 'emulator' 1
        Assert-Equal 'the wait gives up naming the holder' $true ([bool]($heldBy -and $heldBy.Contains('shared-name')))
    } finally { Stop-Process -Id $holder.Id -Force -ErrorAction SilentlyContinue; $holder.WaitForExit(10000) | Out-Null }
    Assert-Equal 'a dead owner process frees the lease at once' $null (Read-Lease $key 0 'emulator')
    Assert-Equal 'taking over a dead owner works' $true (Request-Lease $key 'agent-a' 30 0 'emulator')
    Assert-Equal 'the wait takes a free lease at once' $null (Wait-ForLease $key 'agent-a' 30 0.03 'emulator' 1)
    Remove-Lease $key 'agent-a' 0 'emulator' | Out-Null

    # The wait: starts when the holder ends.
    $holder = Start-LeaseHolder $key 'short-run' 4
    Wait-ForLeaseFile $key
    $heldBy = Wait-ForLease $key 'agent-a' 30 1 'emulator' 1
    Assert-Equal 'the wait ends once the holder ends' $null $heldBy
    Remove-Lease $key 'agent-a' 0 'emulator' | Out-Null
    $holder.WaitForExit(10000) | Out-Null

    # Owner names: RTST_DEVICE_OWNER, else RTST_PHONE_OWNER, else <default>-<pid>.
    $savedDevice = $env:RTST_DEVICE_OWNER; $savedPhone = $env:RTST_PHONE_OWNER
    try {
        $env:RTST_DEVICE_OWNER = ''; $env:RTST_PHONE_OWNER = ''
        Assert-Equal 'owner defaults to name-pid' "run-flows-$PID" (Get-DeviceLeaseOwner 'run-flows')
        $env:RTST_PHONE_OWNER = 'phone-owner'
        Assert-Equal 'owner falls back to the phone owner' 'phone-owner' (Get-DeviceLeaseOwner 'run-flows')
        $env:RTST_DEVICE_OWNER = 'device-owner'
        Assert-Equal 'device owner wins' 'device-owner' (Get-DeviceLeaseOwner 'run-flows')
    } finally { $env:RTST_DEVICE_OWNER = $savedDevice; $env:RTST_PHONE_OWNER = $savedPhone }
} finally {
    Remove-Item $script:EmulatorLeaseDir -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failed) { Write-Host "$failed check(s) failed."; exit 1 }
Write-Host "All emulator-lease checks pass (PowerShell $($PSVersionTable.PSVersion))."
exit 0
