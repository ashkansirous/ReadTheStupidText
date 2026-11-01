<#
.SYNOPSIS
  Starts the RTST_Test AVD headless (no window) on fixed port 5554, or reuses a running one.
  Slice 180: -Instance 2 does the same for AVD RTST_Test2 on port 5556 (create it once with new-avd.ps1); -Instance any
  takes the first instance that is not running and prints which one it chose (pass that number to every later script).
  A running instance counts as taken, so stop it (stop.ps1 -Instance n) when you are done.
  Slice 241: boots from the quickboot snapshot 'rtst-clean' (~10 s instead of ~58 s) when the AVD has one; -ColdBoot (or RTST_COLD_BOOT=1) skips it.
  Slice 162 turned snapshots off because a resumed one carried stale app/system state. The snapshot is therefore taken once, right after a cold boot with the dev
  apps and the Maestro driver uninstalled (a clean machine), is only loaded (-no-snapshot-save: an exit never writes it back), and the "installed" key files are
  dropped after a resume, because the restored disk has no dev app. A snapshot that does not boot in 90 s is killed and the boot falls back to a cold one.
  "Warm" also means an already-running emulator is reused. RAM and GPU come from
  command-line flags, so the AVD's config.ini (outside the repo) is never edited.
  A machine-wide mutex serialises concurrent callers; state lives in <worktrees>\.emulator\.
#>
param(
    [ValidateSet('1', '2', 'any')][string]$Instance = '1',
    [string]$Gpu = 'swiftshader_indirect',
    [int]$MemoryMb = 4096,
    [int]$TimeoutSeconds = 240,
    # Slice 241: boot cold (no snapshot load) even when the clean snapshot exists; a cold boot of an AVD with no snapshot creates it.
    [switch]$ColdBoot,
    # Slice 241: a build runs beside this boot (run-flows starts it that way), so a slow snapshot resume is not proof that the snapshot is bad.
    [switch]$UnderLoad
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
Set-EmulatorInstance $(if ($Instance -eq 'any') { 1 } else { [int]$Instance })

function Use-RunningEmulator {
    $avd = Get-RunningAvdName
    if ($avd -ne $script:AvdName) { throw "$($script:Serial) is running AVD '$avd', not $($script:AvdName); not touching it." }
    Save-StateIfMissing
    Write-Host "Reusing running $($script:Serial)."
}

# An emulator started by hand or earlier has no state file; record its qemu pid so stop.ps1 can stop it.
function Save-StateIfMissing {
    if (Test-Path $script:StateFile) { return }
    $owner = Get-EmulatorPortOwnerPid
    if (-not $owner) { return }
    $null = New-Item -ItemType Directory -Force $script:StateDir
    @{ pid = $owner; port = $script:Port; serial = $script:Serial } | ConvertTo-Json | Set-Content $script:StateFile
}

# Starts the emulator process in the given boot mode ('cold' or 'snapshot') and records its pid. Returns the process.
function Start-EmulatorProcess([string]$Mode) {
    $emulatorArgs = Get-EmulatorStartArguments $script:AvdName $script:Port $Gpu $MemoryMb $Mode
    $proc = Start-Process -FilePath $script:EmulatorExe -ArgumentList $emulatorArgs -WindowStyle Hidden -PassThru
    @{ pid = $proc.Id; port = $script:Port; serial = $script:Serial } | ConvertTo-Json | Set-Content $script:StateFile
    return $proc
}

# Snapshot resume; $true when it booted. A resume that fails (exits, or is not booted in $SnapshotBootSeconds) is killed so the caller can boot cold.
function Start-FromSnapshot {
    $proc = Start-EmulatorProcess 'snapshot'
    if (Wait-BootCompleted $script:SnapshotBootSeconds $proc) { Remove-Item (Get-SnapshotFailFile) -Force -ErrorAction SilentlyContinue; return $proc }
    Write-Host "SNAPSHOT: $($script:CleanSnapshotName) did not boot in $($script:SnapshotBootSeconds) s (exited: $($proc.HasExited)); falling back to a cold boot."
    & taskkill.exe /T /F /PID $proc.Id 2>&1 | Out-Null
    $stopDeadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $stopDeadline -and (Get-EmulatorPortOwnerPid)) { Start-Sleep -Milliseconds 500 }
    $failures = 1 + (Read-SnapshotFailures)
    if (Resolve-SnapshotRemoval ($UnderLoad.IsPresent -or (Test-OtherEmulatorOnline)) $failures) {
        Remove-CleanSnapshot $script:AvdName   # the cold boot below then saves a new one
        Remove-Item (Get-SnapshotFailFile) -Force -ErrorAction SilentlyContinue
    }
    else {
        Set-Content (Get-SnapshotFailFile) $failures
        Write-Host "SNAPSHOT: kept $($script:CleanSnapshotName): the resume was slow under load (failure $failures of 2 before it is deleted)."
    }
    return $null
}

function Get-SnapshotFailFile { return (Join-Path $script:StateDir 'snapshot-fails.txt') }

# How many snapshot resumes in a row failed under load (0 when none was recorded).
function Read-SnapshotFailures {
    $text = if (Test-Path (Get-SnapshotFailFile)) { "$(Get-Content (Get-SnapshotFailFile) -Raw)".Trim() } else { '' }
    $count = 0
    if ([int]::TryParse($text, [ref]$count)) { return $count }
    return 0
}

# True when an emulator other than this instance's is online: the machine is loaded, so a slow resume proves nothing.
function Test-OtherEmulatorOnline {
    $ErrorActionPreference = 'Continue'
    $devices = "$(& $script:Adb devices 2>&1 | Out-String)"
    return @([regex]::Matches($devices, 'emulator-\d+\s+device') | Where-Object { $_.Value -notlike "$($script:Serial)*" }).Count -gt 0
}

# Slice 241: the clean snapshot uninstalls the dev apps and the Maestro driver, so it is made only while this process holds the emulator lease (a run-flows call
# that started it, or one taken here); with another owner on the emulator it is skipped and the next boot is cold again.
function Save-CleanSnapshotUnderLease {
    $owner = "start-warm-snapshot-$PID"
    $taken = [bool](Request-Lease $script:Serial $owner 10 0 'emulator')
    $held = if ($taken) { $null } else { Read-Lease $script:Serial 0 'emulator' }
    if (-not $taken -and -not ($held -and $held.OwnerPid -eq $PID)) {
        Write-Host "SNAPSHOT: not saving $($script:CleanSnapshotName): $($script:Serial) is in use by $(Format-LeaseHolder $held 0 'emulator'), and saving uninstalls the dev apps. The next boot is cold again."
        return
    }
    try { Save-CleanSnapshot; Clear-InstalledKeys }
    finally { if ($taken) { $null = Remove-Lease $script:Serial $owner 0 'emulator' } }
}

function Start-Emulator {
    $null = New-Item -ItemType Directory -Force $script:StateDir
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $coldAsked = $ColdBoot.IsPresent -or ($env:RTST_COLD_BOOT -eq '1')
    $mode = Resolve-BootMode $coldAsked (Test-CleanSnapshotExists $script:AvdName)
    $proc = if ($mode -eq 'snapshot') { Start-FromSnapshot } else { $null }
    if ($proc) { Clear-InstalledKeys; Write-Host "Boot mode: snapshot $($script:CleanSnapshotName)." }
    else {
        $proc = Start-EmulatorProcess 'cold'
        Write-Host "Boot mode: cold$(if ($coldAsked) { ' (asked for)' } elseif ($mode -eq 'cold') { ' (no snapshot yet)' } else { ' (snapshot failed)' })."
        if (-not (Wait-BootCompleted $TimeoutSeconds $proc)) {
            if ($proc.HasExited) { Remove-Item $script:StateFile -Force -ErrorAction SilentlyContinue; throw "Emulator exited early (code $($proc.ExitCode)); is the AVD $($script:AvdName) missing?" }
            throw "Emulator did not finish booting in $TimeoutSeconds s (left running; stop.ps1 stops it)."
        }
        if (-not (Test-CleanSnapshotExists $script:AvdName)) { Save-CleanSnapshotUnderLease }
    }
    Write-Host ("Booted {0} headless in {1:n1} s (pid {2})." -f $script:Serial, $sw.Elapsed.TotalSeconds, $proc.Id)
}

# True while another caller holds this instance's start/stop lock (it picked the instance and is still starting it).
function Test-InstanceLocked {
    $probe = New-Object System.Threading.Mutex($false, $script:MutexName)
    try {
        try { $got = $probe.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $got = $true }
        if ($got) { $probe.ReleaseMutex() }
        return (-not $got)
    } finally { $probe.Dispose() }
}

# True when this instance is running, is being started by another caller, or has a recorded live process.
function Test-InstanceTaken {
    if (Test-InstanceLocked) { return $true }
    if (Test-EmulatorPresent) { return $true }
    if (-not (Test-Path $script:StateFile)) { return $false }
    $state = try { Get-Content $script:StateFile -Raw | ConvertFrom-Json } catch { return $true }  # unreadable: someone is writing it, so taken
    return [bool](Get-Process -Id $state.pid -ErrorAction SilentlyContinue)
}

# -Instance any: the first free instance, chosen under a machine-wide lock so two callers never pick the same one.
function Select-FreeInstance {
    $pick = New-Object System.Threading.Mutex($false, 'Global\RTST_EmulatorPick')
    try { $got = $pick.WaitOne([TimeSpan]::FromSeconds($TimeoutSeconds)) } catch [System.Threading.AbandonedMutexException] { $got = $true }
    if (-not $got) { throw 'Another caller is choosing an emulator instance.' }
    try {
        foreach ($candidate in 1..$script:InstanceCount) {
            Set-EmulatorInstance $candidate
            if (-not (Test-InstanceTaken)) { return Lock-Emulator $TimeoutSeconds }
        }
        throw "Both emulator instances are running (in use). Wait for one to be stopped, or pass -Instance 1|2 to reuse one on purpose."
    } finally { $pick.ReleaseMutex() }
}

$mutex = if ($Instance -eq 'any') { Select-FreeInstance } else { Lock-Emulator $TimeoutSeconds }
try {
    if (Test-EmulatorPresent) {
        # Possibly still booting under another caller: wait for it instead of launching a second one.
        if (-not (Wait-BootCompleted $TimeoutSeconds)) { throw "$($script:Serial) is up but did not finish booting in $TimeoutSeconds s." }
        Use-RunningEmulator
    } else {
        Start-Emulator
    }
    Write-Host "Instance: $($script:EmuInstance) ($($script:Serial), AVD $($script:AvdName))"
} finally {
    $mutex.ReleaseMutex()
}
