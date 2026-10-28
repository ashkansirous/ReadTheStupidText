<#
.SYNOPSIS
  Stops only the emulator start-warm.ps1 started (pid in <worktrees>\.emulator\state.json).
  No snapshot is saved (start-warm.ps1 boots with -no-snapshot-save).
  -Instance 2 stops the second emulator (state2.json); the default is instance 1 (Slice 180).
#>
param(
    [ValidateSet(1, 2)][int]$Instance = 1
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
Set-EmulatorInstance $Instance

function Stop-RecordedEmulator($state) {
    $ErrorActionPreference = 'Continue'  # adb writes to stderr once the emulator is already gone
    $proc = Get-Process -Id $state.pid -ErrorAction SilentlyContinue
    $owner = Get-EmulatorPortOwnerPid  # the VM may outlive the launcher pid
    if (-not $proc -and -not $owner) { Write-Host 'Recorded emulator is not running.'; return $true }
    # `emu kill` asks the emulator to exit cleanly; no snapshot is written.
    & $script:Adb -s $state.serial emu kill 2>$null | Out-Null
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline -and ((Get-Process -Id $state.pid -ErrorAction SilentlyContinue) -or (Get-EmulatorPortOwnerPid))) { Start-Sleep -Milliseconds 500 }
    foreach ($id in @($state.pid, (Get-EmulatorPortOwnerPid)) | Where-Object { $_ }) {
        if (Get-Process -Id $id -ErrorAction SilentlyContinue) { & taskkill.exe /T /F /PID $id 2>&1 | Out-Null }
    }
    Start-Sleep -Seconds 1
    if (Get-EmulatorPortOwnerPid) { return $false }
    Write-Host "Stopped emulator pid $($state.pid)."
    return $true
}

$mutex = Lock-Emulator 60
try {
    if (-not (Test-Path $script:StateFile)) { Write-Host 'No emulator recorded by start-warm.ps1; nothing to stop.'; exit 0 }
    $state = Get-Content $script:StateFile -Raw | ConvertFrom-Json
    if (Stop-RecordedEmulator $state) { Remove-Item $script:StateFile -Force }
    else { throw "Emulator pid $($state.pid) would not stop; state kept." }
} finally {
    $mutex.ReleaseMutex()
}
