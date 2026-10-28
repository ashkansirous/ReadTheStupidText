<#
.SYNOPSIS
  Starts the RTST_Test AVD headless (no window) on fixed port 5554, or reuses a running one.
  Slice 180: -Instance 2 does the same for AVD RTST_Test2 on port 5556 (create it once with new-avd.ps1); -Instance any
  takes the first instance that is not running and prints which one it chose (pass that number to every later script).
  A running instance counts as taken, so stop it (stop.ps1 -Instance n) when you are done.
  Always a cold boot (~45 s) with -no-snapshot-load -no-snapshot-save: a resumed snapshot carried stale
  app/system state and broke Maestro flows (Slice 162), so none is ever loaded or written. "Warm" means an
  already-running emulator is reused. RAM and GPU come from
  command-line flags, so the AVD's config.ini (outside the repo) is never edited.
  A machine-wide mutex serialises concurrent callers; state lives in <worktrees>\.emulator\.
#>
param(
    [ValidateSet('1', '2', 'any')][string]$Instance = '1',
    [string]$Gpu = 'swiftshader_indirect',
    [int]$MemoryMb = 4096,
    [int]$TimeoutSeconds = 240
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

function Start-Emulator {
    $null = New-Item -ItemType Directory -Force $script:StateDir
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $emulatorArgs = @('-avd', $script:AvdName, '-port', $script:Port, '-no-window', '-no-boot-anim', '-no-audio',
                      '-gpu', $Gpu, '-memory', $MemoryMb, '-no-metrics',
                      '-no-snapshot-load', '-no-snapshot-save')
    $proc = Start-Process -FilePath $script:EmulatorExe -ArgumentList $emulatorArgs -WindowStyle Hidden -PassThru
    @{ pid = $proc.Id; port = $script:Port; serial = $script:Serial } | ConvertTo-Json | Set-Content $script:StateFile
    if (-not (Wait-BootCompleted $TimeoutSeconds $proc)) {
        if ($proc.HasExited) { Remove-Item $script:StateFile -Force -ErrorAction SilentlyContinue; throw "Emulator exited early (code $($proc.ExitCode)); is the AVD $($script:AvdName) missing?" }
        throw "Emulator did not finish booting in $TimeoutSeconds s (left running; stop.ps1 stops it)."
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
