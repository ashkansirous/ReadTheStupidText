<#
.SYNOPSIS
  Starts the RTST_Test AVD headless (no window) on fixed port 5554, or reuses a running one.
  Quick-boot snapshots make every start after the first a warm resume. RAM and GPU come from
  command-line flags, so the AVD's config.ini (outside the repo) is never edited.
  A machine-wide mutex serialises concurrent callers; state lives in <worktrees>\.emulator\.
#>
param(
    [string]$Gpu = 'swiftshader_indirect',
    [int]$MemoryMb = 4096,
    [int]$TimeoutSeconds = 240
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

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
                      '-gpu', $Gpu, '-memory', $MemoryMb, '-no-metrics')
    $proc = Start-Process -FilePath $script:EmulatorExe -ArgumentList $emulatorArgs -WindowStyle Hidden -PassThru
    @{ pid = $proc.Id; port = $script:Port; serial = $script:Serial } | ConvertTo-Json | Set-Content $script:StateFile
    if (-not (Wait-BootCompleted $TimeoutSeconds $proc)) {
        if ($proc.HasExited) { Remove-Item $script:StateFile -Force -ErrorAction SilentlyContinue; throw "Emulator exited early (code $($proc.ExitCode)); is the AVD $($script:AvdName) missing?" }
        throw "Emulator did not finish booting in $TimeoutSeconds s (left running; stop.ps1 stops it)."
    }
    Write-Host ("Booted {0} headless in {1:n1} s (pid {2})." -f $script:Serial, $sw.Elapsed.TotalSeconds, $proc.Id)
}

$mutex = Lock-Emulator $TimeoutSeconds
try {
    if (Test-EmulatorPresent) {
        # Possibly still booting under another caller: wait for it instead of launching a second one.
        if (-not (Wait-BootCompleted $TimeoutSeconds)) { throw "$($script:Serial) is up but did not finish booting in $TimeoutSeconds s." }
        Use-RunningEmulator
    } else {
        Start-Emulator
    }
} finally {
    $mutex.ReleaseMutex()
}
