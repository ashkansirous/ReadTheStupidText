# Shared helpers for the warm headless emulator scripts (Slice 161). Dot-sourced, not run.
. (Join-Path $PSScriptRoot 'activity.ps1')
. (Join-Path $PSScriptRoot '..\lease-common.ps1')  # Slice 207: the emulator lease helpers run-flows.ps1 needs
# Slice 180: two emulator instances. Instance 1 is the original (AVD RTST_Test, port 5554); instance 2 is RTST_Test2 on port 5556.
# (The chosen number lives in $script:EmuInstance, never $script:Instance: dot-sourced, that name is the caller's own -Instance parameter.)
# A script calls Set-EmulatorInstance with its -Instance parameter right after dot-sourcing this file; everything below
# (serial, port, AVD, state file, lock) follows the chosen instance, so two agents never address the same emulator.
$script:InstanceBasePort = 5554
$script:InstanceCount = 2
$script:EmuInstance = 1
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

function Set-EmulatorInstance([int]$Instance) {
    if ($Instance -lt 1 -or $Instance -gt $script:InstanceCount) { throw "Instance must be 1 to $($script:InstanceCount), not $Instance." }
    $script:EmuInstance = $Instance
    $script:Port = $script:InstanceBasePort + 2 * ($Instance - 1)
    $script:Serial = "emulator-$($script:Port)"
    $suffix = if ($Instance -eq 1) { '' } else { "$Instance" }
    $script:AvdName = "RTST_Test$suffix"
    $script:StateFile = Join-Path $script:StateDir "state$suffix.json"
    $script:MutexName = "Global\RTST_WarmEmulator$suffix"
}
Set-EmulatorInstance 1

# The build key of the app installed on this instance (written by install-debug.ps1), so a flows run can tell a stale install from the current build.
function Get-InstalledKeyFile([string]$App) {
    return Join-Path $script:StateDir "installed-$App-i$($script:EmuInstance).key"
}

function Save-InstalledKey([string]$App, [string]$Key) {
    $null = New-Item -ItemType Directory -Force $script:StateDir
    Set-Content (Get-InstalledKeyFile $App) $Key
}

function Remove-InstalledKey([string]$App) {
    Remove-Item (Get-InstalledKeyFile $App) -Force -ErrorAction SilentlyContinue
}

function Get-InstalledKey([string]$App) {
    $file = Get-InstalledKeyFile $App
    if (-not (Test-Path $file)) { return $null }
    return (Get-Content $file -Raw -ErrorAction SilentlyContinue).Trim()
}

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

# ---- Slice 215: emulator health. On 2026-10-30 (11:32) the emulator's system_server died after hours of runs: `pm` was gone, sys.boot_completed still read 1,
# and the next flow failed at its first step, which an agent read as an app failure. run-flows.ps1 checks this before every flow and after a failed one.
$script:ExitEmulatorUnhealthy = 5
$script:BootCompletedValue = '1'
$script:PmPathPattern = '(?m)^package:'  # a line of `pm path` output; an error text that merely contains the word does not match

# Pure: the problem the two probe answers show, or $null when the emulator is healthy. $PmOutput is `pm path android`, $BootValue is `getprop sys.boot_completed`.
function Resolve-HealthProblem([string]$PmOutput, [string]$BootValue) {
    if ("$PmOutput" -notmatch $script:PmPathPattern) { return "pm path android did not answer (system_server is gone): [$("$PmOutput".Trim())]" }
    if ("$BootValue".Trim() -ne $script:BootCompletedValue) { return "sys.boot_completed is [$("$BootValue".Trim())], not 1" }
    return $null
}

# Pure: the problem shown by the answer to `pm list packages <package>`, or $null when the package is listed. A package-service failure (no `package:` line and an
# error from the service itself) is an unhealthy emulator, not a missing install: run-flows must restart it, not tell the user to reinstall.
function Resolve-InstallProblem([string]$PmListOutput, [string]$PackageName) {
    if ("$PmListOutput" -match ('(?m)^package:' + [regex]::Escape($PackageName) + '\s*$')) { return $null }
    if ("$PmListOutput" -notmatch '(?m)^package:' -and "$PmListOutput" -match "Can't find service|Failure calling service|Broken pipe|device offline") { return "EMULATOR UNHEALTHY: the package service did not answer: [$("$PmListOutput".Trim())]" }
    return "$PackageName is not installed"
}

# Slice 215: an adb call that gives up after $Seconds (a wedged adbd/qemu makes `adb shell` block forever). Returns the output, or $null on a timeout (the process is killed).
function Invoke-AdbBounded([string[]]$AdbArgs, [int]$Seconds = 15) {
    $info = New-Object Diagnostics.ProcessStartInfo $script:Adb
    $info.Arguments = ($AdbArgs -join ' ')
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $proc = [Diagnostics.Process]::Start($info)
    $out = $proc.StandardOutput.ReadToEndAsync()
    $err = $proc.StandardError.ReadToEndAsync()
    if (-not $proc.WaitForExit($Seconds * 1000)) { try { $proc.Kill() } catch { }; return $null }
    $proc.WaitForExit()
    return ($out.Result + $err.Result)
}

# Asks the emulator; an adb failure counts as an empty answer and a hung adb as a timeout answer, so a dead emulator is a problem, never an exception or a hang.
function Get-EmulatorHealthProblem {
    $ErrorActionPreference = 'Continue'
    $pm = Invoke-AdbBounded @('-s', $script:Serial, 'shell', 'pm', 'path', 'android')
    if ($null -eq $pm) { return 'adb shell did not answer within 15 s (adbd or the emulator is wedged)' }
    $boot = Invoke-AdbBounded @('-s', $script:Serial, 'shell', 'getprop', 'sys.boot_completed')
    if ($null -eq $boot) { return 'adb shell did not answer within 15 s (adbd or the emulator is wedged)' }
    return (Resolve-HealthProblem $pm $boot)
}

# Evidence for the next death (item 10): the crash log, the guest's memory, and the host's free RAM and the qemu working set, saved to $Dir.
function Save-EmulatorEvidence([string]$Dir) {
    $ErrorActionPreference = 'Continue'
    $null = New-Item -ItemType Directory -Force $Dir
    "$(Invoke-AdbBounded @('-s', $script:Serial, 'logcat', '-b', 'crash', '-d'))" | Out-File (Join-Path $Dir 'logcat-crash.txt') -Encoding utf8
    "$(Invoke-AdbBounded @('-s', $script:Serial, 'shell', 'dumpsys', 'meminfo'))" | Out-File (Join-Path $Dir 'dumpsys-meminfo.txt') -Encoding utf8
    $os = Get-CimInstance Win32_OperatingSystem
    $qemuPid = Get-EmulatorPortOwnerPid
    $qemu = if ($qemuPid) { Get-Process -Id $qemuPid -ErrorAction SilentlyContinue } else { $null }
    $host_ = "host free RAM: {0:n0} MB of {1:n0} MB" -f ($os.FreePhysicalMemory / 1KB), ($os.TotalVisibleMemorySize / 1KB)
    $host_ += "`r`nqemu pid: $qemuPid working set: $(if ($qemu) { '{0:n0} MB' -f ($qemu.WorkingSet64 / 1MB) } else { 'process not found' })"
    $host_ | Out-File (Join-Path $Dir 'host-memory.txt') -Encoding utf8
    Write-Host "EMULATOR UNHEALTHY evidence saved to $Dir (logcat-crash.txt, dumpsys-meminfo.txt, host-memory.txt)."
}
