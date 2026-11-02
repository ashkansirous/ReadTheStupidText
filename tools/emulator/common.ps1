# Shared helpers for the warm headless emulator scripts (Slice 161). Dot-sourced, not run.
. (Join-Path $PSScriptRoot 'activity.ps1')
. (Join-Path $PSScriptRoot 'speed.ps1')  # Slice 241: snapshot boot, detached boot, crash lines, flow split
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

# Slice 242 item 5: instance 3 is the tablet AVD RTST_Tablet (port 5558; create it with new-avd.ps1 -Tablet). It is never picked by `-Instance any` (that stays 1..InstanceCount).
$script:TabletInstance = 3
$script:TabletAvdName = 'RTST_Tablet'
$script:TabletAlias = 'tablet'

# '1', '2', '3' or 'tablet' (what -Instance accepts) -> the instance number.
function Resolve-EmulatorInstance([string]$Instance) {
    if ($Instance -eq $script:TabletAlias) { return $script:TabletInstance }
    return [int]$Instance
}

function Set-EmulatorInstance([int]$Instance) {
    if ($Instance -lt 1 -or $Instance -gt $script:TabletInstance) { throw "Instance must be 1 to $($script:InstanceCount), or $($script:TabletInstance) ('$($script:TabletAlias)'), not $Instance." }
    $script:EmuInstance = $Instance
    $script:Port = $script:InstanceBasePort + 2 * ($Instance - 1)
    $script:Serial = "emulator-$($script:Port)"
    $suffix = if ($Instance -eq 1) { '' } else { "$Instance" }
    $script:AvdName = if ($Instance -eq $script:TabletInstance) { $script:TabletAvdName } else { "RTST_Test$suffix" }
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

# Slice 221 item 2: one Dino dev package id on the emulator, the same one the phone scripts use (tools/phone/common.ps1). The csproj id stays the store id; the dev build overrides it
# with -p:ApplicationId. The plain store id is removed from an EMULATOR only (emulator-<port>): on a real phone it would be the user's store install, which is never uninstalled.
$script:DinoDevPackage = 'uk.sirous.dinoreader.dbg'
$script:DinoStorePackage = 'uk.sirous.dinoreader'
$script:EmulatorPackages = @{ rtst = 'uk.sirous.readthestupidtext'; dino = $script:DinoDevPackage }
$script:EmulatorSerialPattern = '^emulator-\d+$'

# The cache platform name of the emulator APK: Dino's carries the .dbg id, so an APK cached before the id change (plain id) is never installed.
function Get-EmulatorBuildPlatform([string]$App) {
    if ($App -eq 'dino') { return 'android-dbg' }
    return 'android'
}

# Pure: the packages to uninstall before installing the dev build: the plain Dino store id when `pm list packages` shows it exactly AND the serial is an emulator; nothing on any other serial.
function Get-StalePackagesToRemove([string]$App, [string]$Serial, [string]$PmListOutput) {
    if ($App -ne 'dino' -or $Serial -cnotmatch $script:EmulatorSerialPattern) { return @() }
    if ("$PmListOutput" -match ('(?m)^package:' + [regex]::Escape($script:DinoStorePackage) + '\s*$')) { return @($script:DinoStorePackage) }
    return @()
}

# Pure: every Dino package `pm list packages` shows (the store id and the dev id both match the prefix, nothing else does).
function Get-DinoPackagesListed([string]$PmListOutput) {
    return @([regex]::Matches("$PmListOutput", '(?m)^package:(uk\.sirous\.dinoreader(?:\.dbg)?)\s*$') | ForEach-Object { $_.Groups[1].Value })
}

# Pure: $null when exactly the dev package is installed, else what is wrong (the check install-debug.ps1 runs after an install). On a serial that is not an emulator
# the plain store id is the user's own app and is never removed (Get-StalePackagesToRemove), so it is ignored there: exactly one dev package is the whole check.
function Resolve-DinoPackageProblem([string]$PmListOutput, [string]$Serial = '') {
    $listed = @(Get-DinoPackagesListed $PmListOutput)
    if ($Serial -and $Serial -cnotmatch $script:EmulatorSerialPattern) { $listed = @($listed | Where-Object { $_ -ceq $script:DinoDevPackage }) }
    if ($listed.Count -eq 1 -and $listed[0] -ceq $script:DinoDevPackage) { return $null }
    return "expected exactly $($script:DinoDevPackage) after the install, pm list packages shows: [$($listed -join ', ')]"
}

# Uninstalls the stale packages from the emulator (adb uninstall wipes only that package's data on the emulator). Says what it removed.
function Remove-StaleDevPackages([string]$App) {
    $pm = "$(& $script:Adb -s $script:Serial shell pm list packages 2>&1 | Out-String)"
    foreach ($stale in @(Get-StalePackagesToRemove $App $script:Serial $pm)) {
        Write-Host "Removing the stale $stale from $($script:Serial) (the Dino dev install is $($script:DinoDevPackage))."
        $result = "$(& $script:Adb -s $script:Serial uninstall $stale 2>&1 | Out-String)".Trim()
        if ($LASTEXITCODE -ne 0 -or $result -notmatch 'Success') { Write-Warning "Could not remove the stale $stale from $($script:Serial) ($result). The install goes on; the Dino package check after it will say if two dinoreader packages remain." }
        else { Write-Host $result }
    }
}

# Pure: the problem shown by the answer to `pm list packages <package>`, or $null when the package is listed. A package-service failure (no `package:` line and an
# error from the service itself) is an unhealthy emulator, not a missing install: run-flows must restart it, not tell the user to reinstall.
function Resolve-InstallProblem([string]$PmListOutput, [string]$PackageName) {
    if ("$PmListOutput" -match ('(?m)^package:' + [regex]::Escape($PackageName) + '\s*$')) { return $null }
    if ("$PmListOutput" -notmatch '(?m)^package:' -and "$PmListOutput" -match "Can't find service|Failure calling service|Broken pipe|device offline") { return "EMULATOR UNHEALTHY: the package service did not answer: [$("$PmListOutput".Trim())]" }
    return "$PackageName is not installed"
}

# Slice 225 item 4: `pm list packages <package>` prints one `package:` line per match (the store id and the .dbg id both match a prefix). Joined with a space the lines run together and the
# per-line match above cannot see them, so the caller joins them with newlines.
function Join-PmLines($PmLines) { return (@($PmLines) | ForEach-Object { "$_".TrimEnd() }) -join "`n" }

# Slice 225 item 19: -Flow takes a flow name (record-page-shows-words); a path to the yaml (.maestro/dino/record-page-shows-words.yaml) is reduced to its name.
function Get-FlowName([string]$Flow) {
    if (-not $Flow) { return '' }
    return [IO.Path]::GetFileNameWithoutExtension($Flow.Trim())
}

# Slice 225 item 4: the flows a run takes. Without -Flow: .maestro/<app>/*.yaml (flows under needs-file/ need a pushed file and are skipped). With -Flow <name>: the flow of that name in the app's folder,
# else the one in needs-file/ (the caller has pushed its file by hand, so naming it is the request to run it).
function Find-FlowFiles([string]$FlowDir, [string]$Flow) {
    $name = Get-FlowName $Flow
    $own = @(Get-ChildItem $FlowDir -Filter '*.yaml' -File -ErrorAction SilentlyContinue)
    if (-not $name) { return @($own | Sort-Object Name) }
    $hit = @($own | Where-Object { $_.BaseName -eq $name })
    if ($hit.Count -eq 0) { $hit = @(Get-ChildItem (Join-Path $FlowDir 'needs-file') -Filter '*.yaml' -File -ErrorAction SilentlyContinue | Where-Object { $_.BaseName -eq $name }) }
    return $hit
}

# Slice 225 item 12: a Maestro driver (dev.mobile.maestro) left running from an earlier flow made `uiautomator dump` print "Killed" on the emulator. It is force-stopped before every dump;
# Maestro installs and starts its driver again when the next flow needs it. $AdbCall takes the adb arguments (a self-test passes a recorder).
$script:MaestroDriverPackage = 'dev.mobile.maestro'
function Invoke-EmulatorAdbText([string[]]$AdbArgs) { return (& $script:Adb -s $script:Serial @AdbArgs 2>&1 | Out-String) }

# Pure: run-flows' end-of-run line. Takes the results as an array (pass $list.ToArray()): `@($list).Count` on a List[object] throws "Argument types do not match" in pwsh 7.6.
function Format-FlowsSummary([string]$App, [object[]]$Results, [double]$Seconds) {
    $failed = @($Results | Where-Object { $_.Problem }).Count
    $notRun = @($Results | Where-Object { $_.NotRun }).Count
    $passed = $Results.Length - $failed - $notRun
    return ('{0}: {1} passed, {2} failed, {3} not run (app crashed) in {4:n1} s.' -f $App, $passed, $failed, $notRun, $Seconds)
}

# Slice 241: the one line that names a crash, from Find-AppCrashLines output (the exception / signal / ANR line, else the first line).
function Get-FirstCrashLine([object[]]$Crash) {
    $all = @($Crash)
    $key = @($all | Where-Object { $_ -match 'Process:|ANR in|am_anr|Fatal signal|FATAL|Exception' })
    return ((@($key) + $all)[0]).Trim()
}

# Slice 241: `app crashed in <flow>: <first crash line>` for every flow that crashed, or nothing.
function Format-CrashSummary([object[]]$Results) {
    return @(@($Results) | Where-Object { @($_.Crash).Count -gt 0 } | ForEach-Object { 'app crashed in {0}: {1}' -f $_.Name, (Get-FirstCrashLine $_.Crash) })
}

function Get-UiDumpXml([scriptblock]$AdbCall = { param($a) Invoke-EmulatorAdbText $a }) {
    $null = & $AdbCall @('shell', 'am', 'force-stop', $script:MaestroDriverPackage)
    return (& $AdbCall @('shell', 'uiautomator', 'dump', '/dev/tty'))
}

# Slice 225 item 21: TalkBack on the emulator without the Settings UI, so no permission dialog appears (the dialog stopped the Slice 226/231 TalkBack checks). It writes the two secure settings
# the Settings switch writes. Disable-EmulatorTalkBack restores what Enable-EmulatorTalkBack found. NOT confirmed on a device: that TalkBack (com.google.android.marvin.talkback) is on the AVD image,
# and that it starts without its first-run tutorial; Test-EmulatorTalkBackOn is how the check proves it (accessibility_enabled = 1).
$script:TalkBackService = 'com.google.android.marvin.talkback/com.google.android.marvin.talkback.TalkBackService'
$script:AccessibilityServicesSetting = 'enabled_accessibility_services'
$script:AccessibilityEnabledSetting = 'accessibility_enabled'

# Pure: the adb arguments that turn TalkBack on, given the services already enabled (kept, colon-separated) and not duplicated.
function Get-TalkBackEnableCommands([string]$CurrentServices) {
    $current = @("$CurrentServices".Trim() -split ':' | Where-Object { $_ -and $_ -ne 'null' })
    $services = (@($current | Where-Object { $_ -ne $script:TalkBackService }) + $script:TalkBackService) -join ':'
    $servicesCommand = @('shell', 'settings', 'put', 'secure', $script:AccessibilityServicesSetting, $services)
    $enabledCommand = @('shell', 'settings', 'put', 'secure', $script:AccessibilityEnabledSetting, '1')
    return @($servicesCommand, $enabledCommand)
}

# Pure: the adb arguments that put the settings back as they were before (null = the setting did not exist).
function Get-TalkBackDisableCommands([string]$PreviousServices, [string]$PreviousEnabled) {
    $services = "$PreviousServices".Trim()
    $enabled = "$PreviousEnabled".Trim()
    $first = if ($services -and $services -ne 'null') { @('shell', 'settings', 'put', 'secure', $script:AccessibilityServicesSetting, $services) } else { @('shell', 'settings', 'delete', 'secure', $script:AccessibilityServicesSetting) }
    $second = if ($enabled -and $enabled -ne 'null') { @('shell', 'settings', 'put', 'secure', $script:AccessibilityEnabledSetting, $enabled) } else { @('shell', 'settings', 'delete', 'secure', $script:AccessibilityEnabledSetting) }
    return @($first, $second)
}

# Turns TalkBack on; returns what to give to Disable-EmulatorTalkBack. Throws when accessibility_enabled is not 1 afterwards.
function Enable-EmulatorTalkBack([scriptblock]$AdbCall = { param($a) Invoke-EmulatorAdbText $a }) {
    $previous = [pscustomobject]@{
        Services = "$(& $AdbCall @('shell', 'settings', 'get', 'secure', $script:AccessibilityServicesSetting))".Trim()
        Enabled = "$(& $AdbCall @('shell', 'settings', 'get', 'secure', $script:AccessibilityEnabledSetting))".Trim()
    }
    foreach ($command in Get-TalkBackEnableCommands $previous.Services) { $null = & $AdbCall $command }
    if (-not (Test-EmulatorTalkBackOn $AdbCall)) { throw 'TalkBack did not switch on: settings get secure accessibility_enabled is not 1 (is TalkBack on this AVD image?).' }
    return $previous
}

function Disable-EmulatorTalkBack($Previous, [scriptblock]$AdbCall = { param($a) Invoke-EmulatorAdbText $a }) {
    foreach ($command in Get-TalkBackDisableCommands $Previous.Services $Previous.Enabled) { $null = & $AdbCall $command }
}

function Test-EmulatorTalkBackOn([scriptblock]$AdbCall = { param($a) Invoke-EmulatorAdbText $a }) {
    $enabled = "$(& $AdbCall @('shell', 'settings', 'get', 'secure', $script:AccessibilityEnabledSetting))".Trim()
    $services = "$(& $AdbCall @('shell', 'settings', 'get', 'secure', $script:AccessibilityServicesSetting))"
    return ($enabled -eq '1' -and $services.Contains('talkback'))
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
