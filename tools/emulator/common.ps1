# Shared helpers for the warm headless emulator scripts (Slice 161). Dot-sourced, not run.
$script:Serial = 'emulator-5554'
$script:Port = 5554
$script:AvdName = 'RTST_Test'
$script:SdkRoot = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { $env:ANDROID_SDK_ROOT }
if (-not $script:SdkRoot) { throw 'ANDROID_HOME (or ANDROID_SDK_ROOT) is not set.' }
$script:Adb = Join-Path $script:SdkRoot 'platform-tools\adb.exe'
$script:EmulatorExe = Join-Path $script:SdkRoot 'emulator\emulator.exe'
$script:RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

# The emulator is one shared instance, so its state lives in the shared worktrees folder, not in any one worktree.
function Get-SharedEmulatorDir {
    $parent = Split-Path $script:RepoRoot -Parent
    if ((Split-Path $parent -Leaf) -like '*-worktrees') { return Join-Path $parent '.emulator' }
    return Join-Path $parent ((Split-Path $script:RepoRoot -Leaf) + '-worktrees\.emulator')
}
$script:StateDir = Get-SharedEmulatorDir
$script:StateFile = Join-Path $script:StateDir 'state.json'
$script:MutexName = 'Global\RTST_WarmEmulator'

function Lock-Emulator([int]$TimeoutSeconds) {
    $mutex = New-Object System.Threading.Mutex($false, $script:MutexName)
    try { $got = $mutex.WaitOne([TimeSpan]::FromSeconds($TimeoutSeconds)) } catch [System.Threading.AbandonedMutexException] { $got = $true }
    if (-not $got) { throw 'Another start/stop of the warm emulator is still running.' }
    return $mutex
}

function Test-EmulatorOnline {
    $ErrorActionPreference = 'Continue'
    $state = & $script:Adb -s $script:Serial get-state 2>$null
    return ($state -eq 'device')
}

# adb reports `offline` while an emulator is still starting; that counts as present (starting), not absent.
function Test-EmulatorPresent {
    $ErrorActionPreference = 'Continue'
    $state = & $script:Adb -s $script:Serial get-state 2>$null
    return ($state -eq 'device' -or $state -eq 'offline')
}

# The qemu process that owns the emulator's console port (the launcher pid may exit while it lives on).
function Get-EmulatorPortOwnerPid {
    $c = Get-NetTCPConnection -LocalPort $script:Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($c) { return $c.OwningProcess }
    return $null
}

function Test-BootCompleted {
    $ErrorActionPreference = 'Continue'
    $v = & $script:Adb -s $script:Serial shell getprop sys.boot_completed 2>$null
    return ("$v".Trim() -eq '1')
}

function Get-RunningAvdName {
    $ErrorActionPreference = 'Continue'
    $out = & $script:Adb -s $script:Serial emu avd name 2>$null
    return ("$(@($out)[0])").Trim()
}

# Returns $true once booted; $false on timeout or when $Process (if given) exits first.
function Wait-BootCompleted([int]$TimeoutSeconds, $Process = $null) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ((Test-EmulatorOnline) -and (Test-BootCompleted)) { return $true }
        if ($Process -and $Process.HasExited) { return $false }
        Start-Sleep -Milliseconds 500
    }
    return $false
}
