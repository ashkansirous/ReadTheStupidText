# Shared lease helpers for every device the test tooling uses (Slice 206 for the phone, Slice 207 for the emulator). Dot-sourced, not run.
# A lease is a file <worktrees>\.<phone|emulator>\lease-<key>.json holding owner, taken and expires (unix seconds, so ConvertFrom-Json in 5.1 and 7
# read the same numbers). The key is the phone's own `getprop ro.serialno`, or the emulator's adb serial (emulator-5554). An expired lease is free; a
# corrupt or unreadable one is held (see Read-Lease). Read-modify-write runs under a named mutex, so two sessions cannot both take it.
# The emulator kind also stores the owner's process id and start time: a lease whose owner process is gone is free (a crashed run never blocks the next).
. (Join-Path $PSScriptRoot 'emulator\activity.ps1')
$script:LeaseDir = Join-Path (Get-WorktreesRoot) '.phone'
$script:EmulatorLeaseDir = Join-Path (Get-WorktreesRoot) '.emulator'
$script:LeaseKinds = @{
    phone    = @{ Mutex = 'rtst-phone-lease-'; TracksProcess = $false }
    emulator = @{ Mutex = 'rtst-emulator-lease-'; TracksProcess = $true }
}
$script:DefaultLeaseMinutes = 120
$script:MaxLeaseMinutes = 1440
$script:LeaseLockSeconds = 10
$script:LeaseReadAttempts = 3
$script:UnreadableLeaseOwner = '(lease file unreadable)'
$script:CorruptLeaseOwner = '(lease file corrupt: remove it by hand)'
$script:RenewBelowMinutes = 15

function Get-UnixNow { return [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() }

function Get-LeaseDir([string]$Kind) {
    if (-not $script:LeaseKinds.ContainsKey($Kind)) { throw "Unknown lease kind '$Kind'." }
    if ($Kind -eq 'emulator') { return $script:EmulatorLeaseDir }
    return $script:LeaseDir
}

function Get-LeasePath([string]$Key, [string]$Kind = 'phone') {
    return Join-Path (Get-LeaseDir $Kind) ('lease-{0}.json' -f ($Key -replace '[^\w.-]', '_'))
}

# Runs $LockedBody holding the machine-wide mutex for this device; an abandoned mutex (its holder died) is taken over.
function Invoke-WithLeaseLock([string]$Key, [scriptblock]$LockedBody, [string]$Kind = 'phone') {
    $mutex = New-Object Threading.Mutex($false, ('Global\' + $script:LeaseKinds[$Kind].Mutex + ($Key -replace '[^\w.-]', '_')))
    $got = $false
    try {
        try { $got = $mutex.WaitOne($script:LeaseLockSeconds * 1000) } catch [Threading.AbandonedMutexException] { $got = $true }
        if (-not $got) { throw "Another session has held the $Kind lease lock for more than $($script:LeaseLockSeconds) s." }
        & $LockedBody
    } finally {
        if ($got) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}

# The live lease, or $null when there is none, it expired, or (emulator kind) its owner process no longer runs. A file that cannot be read after
# retries, or whose JSON is corrupt, is a held lease (fail closed): a corrupt one is held until its file is older than the longest lease allowed
# (MaxLeaseMinutes), or until someone removes it by hand.
function Read-Lease([string]$Key, [int64]$Now = 0, [string]$Kind = 'phone') {
    if ($Now -le 0) { $Now = Get-UnixNow }
    $path = Get-LeasePath $Key $Kind
    if (-not (Test-Path $path)) { return $null }
    $text = $null
    for ($try = 1; $try -le $script:LeaseReadAttempts -and $null -eq $text; $try++) {
        try { $text = [IO.File]::ReadAllText($path) } catch [IO.IOException] { Start-Sleep -Milliseconds 100 } catch [UnauthorizedAccessException] { Start-Sleep -Milliseconds 100 }
    }
    if ($null -eq $text) { return [pscustomobject]@{ Owner = $script:UnreadableLeaseOwner; Taken = $Now; Expires = $Now + 60; OwnerPid = 0 } }
    try { $lease = $text | ConvertFrom-Json -ErrorAction Stop } catch { return Resolve-CorruptLease $path $Now }
    if (-not $lease.owner -or [int64]$lease.expires -le $Now) { return $null }
    if ($lease.pid -and -not (Test-ActivityProcessAlive $lease)) { return $null }
    return [pscustomobject]@{ Owner = "$($lease.owner)"; Taken = [int64]$lease.taken; Expires = [int64]$lease.expires; OwnerPid = [int]$lease.pid }
}

function Resolve-CorruptLease([string]$Path, [int64]$Now) {
    $written = [DateTimeOffset]::new((Get-Item $Path).LastWriteTimeUtc).ToUnixTimeSeconds()
    $until = $written + ($script:MaxLeaseMinutes * 60)
    if ($until -le $Now) { return $null }
    return [pscustomobject]@{ Owner = $script:CorruptLeaseOwner; Taken = $written; Expires = $until; OwnerPid = 0 }
}

# Temp file, then rename, so a reader never sees half of it. Callers hold the lease lock.
function Write-Lease([string]$Key, [string]$Owner, [int]$Minutes, [int64]$Now = 0, [string]$Kind = 'phone') {
    if ($Now -le 0) { $Now = Get-UnixNow }
    $null = New-Item -ItemType Directory -Force (Get-LeaseDir $Kind)
    $path = Get-LeasePath $Key $Kind
    $temp = "$path.$PID.tmp"
    $record = @{ owner = $Owner; taken = $Now; expires = $Now + ($Minutes * 60) }
    if ($script:LeaseKinds[$Kind].TracksProcess) {
        $record.pid = $PID
        $record.procStart = (Get-Process -Id $PID).StartTime.ToFileTimeUtc()
    }
    $record | ConvertTo-Json | Set-Content $temp
    Move-Item $temp $path -Force
}

function Format-LeaseHolder($Lease) {
    $until = [DateTimeOffset]::FromUnixTimeSeconds($Lease.Expires).LocalDateTime.ToString('HH:mm')
    return "'$($Lease.Owner)' (until $until)"
}

# True when $Held (a live lease) blocks $Owner: another owner, or (emulator kind) the same owner name held by another running process.
function Test-LeaseBlocks($Held, [string]$Owner, [string]$Kind) {
    if (-not $Held) { return $false }
    if ($Held.Owner -ne $Owner) { return $true }
    return [bool]($script:LeaseKinds[$Kind].TracksProcess -and $Held.OwnerPid -and $Held.OwnerPid -ne $PID)
}

# $true and the lease is written (or renewed by the same owner); $false when another owner holds it. Read, decision and write are one guarded step,
# and an expired lease is replaced under the same guard.
function Request-Lease([string]$Key, [string]$Owner, [int]$Minutes, [int64]$Now = 0, [string]$Kind = 'phone') {
    if ($Minutes -lt 1 -or $Minutes -gt $script:MaxLeaseMinutes) { throw "Lease minutes must be 1 to $($script:MaxLeaseMinutes) (got $Minutes)." }
    return (Invoke-WithLeaseLock $Key {
        $held = Read-Lease $Key $Now $Kind
        if (Test-LeaseBlocks $held $Owner $Kind) { return $false }
        Write-Lease $Key $Owner $Minutes $Now $Kind
        return $true
    } $Kind)
}

function Remove-Lease([string]$Key, [string]$Owner, [int64]$Now = 0, [string]$Kind = 'phone') {
    return (Invoke-WithLeaseLock $Key {
        $held = Read-Lease $Key $Now $Kind
        if ($held -and $held.Owner -ne $Owner) { return $false }
        Remove-Item (Get-LeasePath $Key $Kind) -Force -ErrorAction SilentlyContinue
        return $true
    } $Kind)
}

# True when the caller's own lease has less than RenewBelowMinutes left.
function Test-LeaseNeedsRenewal($Lease, [string]$Owner, [int64]$Now) {
    return [bool]($Lease -and $Lease.Owner -ceq $Owner -and ($Lease.Expires - $Now) -lt ($script:RenewBelowMinutes * 60))
}

# 'Renewed' when $Owner holds a live lease (its expiry moves to $Minutes from now), 'NotHeld' when nobody does, 'Other' when someone else does.
function Update-Lease([string]$Key, [string]$Owner, [int]$Minutes, [int64]$Now = 0, [string]$Kind = 'phone') {
    if ($Now -le 0) { $Now = Get-UnixNow }
    return (Invoke-WithLeaseLock $Key {
        $held = Read-Lease $Key $Now $Kind
        if (-not $held) { return 'NotHeld' }
        if ($held.Owner -cne $Owner -or (Test-LeaseBlocks $held $Owner $Kind)) { return 'Other' }  # same name, another running process (emulator kind) is someone else
        Write-Lease $Key $Owner $Minutes $Now $Kind
        return 'Renewed'
    } $Kind)
}

# ---- Waiting for a device (Slice 207). Used by run-flows.ps1 and audio-timing.ps1 -Emulator.
$script:ExitLeaseHeld = 4
$script:EmulatorLeaseMinutes = 30
$script:LeaseNoticeSeconds = 60
$script:DeviceOwnerVariable = 'RTST_DEVICE_OWNER'

# The owner name this session uses on a device: RTST_DEVICE_OWNER, else the phone owner RTST_PHONE_OWNER, else "<Default>-<pid>".
function Get-DeviceLeaseOwner([string]$Default) {
    foreach ($name in $script:DeviceOwnerVariable, 'RTST_PHONE_OWNER') {
        $value = [Environment]::GetEnvironmentVariable($name)
        if ($value) { return $value }
    }
    return "$Default-$PID"
}

# $true when $Owner still holds the lease after renewing it (also when it lapsed with nobody else taking it: it is taken again); $false when another
# owner has it, which means this run no longer has the device to itself and must stop.
function Confirm-DeviceLease([string]$Key, [string]$Owner, [int]$Minutes, [string]$Kind) {
    $state = Update-Lease $Key $Owner $Minutes 0 $Kind
    if ($state -eq 'Renewed') { return $true }
    if ($state -eq 'NotHeld') { return [bool](Request-Lease $Key $Owner $Minutes 0 $Kind) }
    return $false
}

# Takes the lease, waiting up to $WaitMinutes while another owner holds it (says "LEASE waiting on <holder>" once a minute).
# Returns $null once taken, else the holder's text when the wait ran out.
function Wait-ForLease([string]$Key, [string]$Owner, [int]$Minutes, [double]$WaitMinutes, [string]$Kind, [int]$PollSeconds = 5) {
    $deadline = (Get-Date).AddMinutes($WaitMinutes)
    $nextNotice = Get-Date
    while ($true) {
        if (Request-Lease $Key $Owner $Minutes 0 $Kind) { return $null }
        $held = Read-Lease $Key 0 $Kind
        if (-not $held) { if ((Get-Date) -ge $deadline) { return '(lease unreadable)' }; Start-Sleep -Milliseconds 200; continue }  # released between the request and the read: try again shortly
        $holder = Format-LeaseHolder $held
        if ((Get-Date) -ge $deadline) { return $holder }
        if ((Get-Date) -ge $nextNotice) { Write-Host "LEASE waiting on $holder"; $nextNotice = (Get-Date).AddSeconds($script:LeaseNoticeSeconds) }
        Start-Sleep -Seconds $PollSeconds
    }
}
