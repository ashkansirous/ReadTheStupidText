# Slice 241: faster emulator runs. Dot-sourced by common.ps1, not run. Pure parts are covered by test-speed.ps1.
. (Join-Path $PSScriptRoot '..\detached-process.ps1')
. (Join-Path $PSScriptRoot 'dialog-guard.ps1')   # Slice 253 item 1: hide_error_dialogs and CLOSE_SYSTEM_DIALOGS

# ---- item 2: boot from a snapshot of a clean state
# Line 7 of start-warm.ps1 said "no snapshots" because a resumed snapshot carried stale app and system state (Slice 162). The snapshot used now is taken once, right after a
# cold boot with every dev package and the Maestro driver uninstalled, and is only ever loaded (-no-snapshot-save), never written back by an exit. So a resume is the same clean
# machine every time. The installed apps are part of the disk state it restores, so after a snapshot boot the "installed" key files are dropped and the next install is a real one.
$script:CleanSnapshotName = 'rtst-clean'
$script:SnapshotBootSeconds = 90         # a snapshot resume that is not booted by then is killed and the boot falls back to a cold one
$script:ColdBootEnvVar = 'RTST_COLD_BOOT'

function Get-AvdHome { if ($env:ANDROID_AVD_HOME) { return $env:ANDROID_AVD_HOME } return (Join-Path $env:USERPROFILE '.android\avd') }

# True when the AVD has the clean snapshot folder with something in it (the emulator writes the folder last-ish; an empty one is an interrupted save).
function Test-CleanSnapshotExists([string]$AvdName, [string]$AvdHome = (Get-AvdHome)) {
    $dir = Join-Path $AvdHome "$AvdName.avd\snapshots\$($script:CleanSnapshotName)"
    if (-not (Test-Path $dir)) { return $false }
    return (@(Get-ChildItem $dir -File -ErrorAction SilentlyContinue).Count -gt 0)
}

# Pure: 'snapshot' when the clean snapshot should be loaded, else 'cold' (asked for, or there is none yet).
function Resolve-BootMode([bool]$ColdBoot, [bool]$SnapshotExists) {
    if ($ColdBoot -or $SnapshotExists -eq $false) { return 'cold' }
    return 'snapshot'
}

# Pure: the emulator command line. Never saves a snapshot on exit; a cold boot also never loads one.
function Get-EmulatorStartArguments([string]$AvdName, [int]$Port, [string]$Gpu, [int]$MemoryMb, [string]$Mode) {
    $common = @('-avd', $AvdName, '-port', $Port, '-no-window', '-no-boot-anim', '-no-audio', '-gpu', $Gpu, '-memory', $MemoryMb, '-no-metrics', '-no-snapshot-save')
    if ($Mode -eq 'snapshot') { return @($common + @('-snapshot', $script:CleanSnapshotName)) }
    return @($common + @('-no-snapshot-load'))
}

# Pure: the adb shell commands that bring the machine to the clean state just before the snapshot is saved.
function Get-CleanStateCommands([string[]]$Packages) {
    return @($Packages | ForEach-Object { "pm uninstall $_" })
}

function Get-CleanSnapshotPackages {
    return @($script:EmulatorPackages.Values) + @('uk.sirous.dinoreader', 'dev.mobile.maestro', 'dev.mobile.maestro.test')
}

# Cold boot done and nothing installed by the caller yet: uninstall the dev packages, then `emu avd snapshot save`. A failed save is a warning only (the next boot is cold again).
function Save-CleanSnapshot {
    $ErrorActionPreference = 'Continue'
    foreach ($cmd in (Get-CleanStateCommands (Get-CleanSnapshotPackages))) { $null = & $script:Adb -s $script:Serial shell $cmd 2>&1 }
    $out = "$(& $script:Adb -s $script:Serial emu avd snapshot save $script:CleanSnapshotName 2>&1 | Out-String)"
    if ($out -match 'KO' -or -not (Test-CleanSnapshotExists $script:AvdName)) { Write-Host "SNAPSHOT: could not save $($script:CleanSnapshotName) for $($script:AvdName) ($($out.Trim())); the next boot is cold again."; return }
    Write-Host "SNAPSHOT: saved $($script:CleanSnapshotName) for $($script:AvdName); later boots resume from it (start-warm.ps1 -ColdBoot skips it)."
}

# Pure: whether a snapshot that did not boot is deleted. A failed resume with no build or other emulator running says the snapshot is bad; one that failed under load
# (a build runs beside the boot) is only slow, so it is kept until the resume has failed twice in a row.
function Resolve-SnapshotRemoval([bool]$UnderLoad, [int]$ConsecutiveFailures) {
    return ((-not $UnderLoad) -or $ConsecutiveFailures -ge 2)
}

# A snapshot that did not boot (see Resolve-SnapshotRemoval) is deleted, so the cold boot that follows saves a fresh one (Test-CleanSnapshotExists is then false) instead of paying the failed resume every time.
function Remove-CleanSnapshot([string]$AvdName, [string]$AvdHome = (Get-AvdHome)) {
    $dir = Join-Path $AvdHome "$AvdName.avd\snapshots\$($script:CleanSnapshotName)"
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue }
}

# The snapshot restored the disk as it was: no dev app is installed, so the 'installed key' files (install-debug.ps1) must not claim one is.
function Clear-InstalledKeys {
    foreach ($app in @('rtst', 'dino')) { Remove-InstalledKey $app }
}

# ---- item 3 / item 5: the emulator boot as a detached background process, so the boot overlaps a build and holds no handle of the caller
# The boot runs beside a build, so start-warm.ps1 is told (-UnderLoad) that a slow snapshot resume is not proof of a bad snapshot.
function Start-EmulatorBootInBackground([int]$InstanceNumber, [switch]$ColdBoot) {
    $null = New-Item -ItemType Directory -Force $script:StateDir
    $log = Join-Path $script:StateDir "boot-$InstanceNumber.log"
    $pwsh = (Get-Process -Id $PID).Path
    $arguments = "-NoProfile -File `"$(Join-Path $PSScriptRoot 'start-warm.ps1')`" -Instance $InstanceNumber" + $(if ($ColdBoot) { ' -ColdBoot' } else { '' }) + ' -UnderLoad'
    $process = Start-DetachedProcess $pwsh $arguments $log "$log.err"
    return $process | Add-Member -NotePropertyName Log -NotePropertyValue $log -PassThru
}

# Waits for a background boot. With -TimeoutSeconds the wait is bounded: returns $false when the boot is still running then (the caller stops it); $true once it ended.
function Wait-EmulatorBoot($Boot, [int]$TimeoutSeconds = 0) {
    if (-not $Boot.HasExited) { Write-Host "Waiting for the emulator to finish booting (up to ~4 min)..." }
    if ($TimeoutSeconds -gt 0) { if (-not $Boot.WaitForExit($TimeoutSeconds * 1000)) { return $false } } else { $Boot.WaitForExit() }
    $null = $Boot.Handle
    Get-Content $Boot.Log -ErrorAction SilentlyContinue | Write-Host
    if ($null -ne $Boot.ExitCode -and $Boot.ExitCode -ne 0) { throw "start-warm.ps1 failed (exit $($Boot.ExitCode)): $(Get-Content "$($Boot.Log).err" -Raw -ErrorAction SilentlyContinue)" }
    return $true
}

# ---- item 1: an app that crashed or hit an ANR during a flow
# Pure: the lines of `logcat -b crash -d` (plus the ActivityManager ANR lines) that name this package. Empty when the app did not die.
function Find-AppCrashLines([string]$CrashLog, [string]$AnrLog, [string]$Package) {
    $pkg = [regex]::Escape($Package)
    $lines = @()
    $crash = @("$CrashLog" -split "`r?`n")
    for ($i = 0; $i -lt $crash.Count; $i++) {
        if ($crash[$i] -notmatch "Process:\s*$pkg(\s|,|$)") { continue }
        $from = if ($i -gt 0 -and $crash[$i - 1] -match 'FATAL EXCEPTION') { $i - 1 } else { $i }
        $lines += @($crash[$from..([Math]::Min($i + 2, $crash.Count - 1))])
    }
    $lines += @("$CrashLog" -split "`r?`n" | Where-Object { $_ -match "Fatal signal .*pid \d+ \($pkg\)" -or $_ -match ">>> $pkg <<<" })   # a native crash (a Mono SIGSEGV) has no "Process:" line
    $lines += @("$AnrLog" -split "`r?`n" | Where-Object { $_ -match "ANR in $pkg" -or $_ -match "am_anr.*$pkg" })
    return @($lines | Where-Object { $_ -and $_.Trim() } | Select-Object -First 8)
}

# Slice 247: the raw crash-buffer and ANR text of the flow (logcat is cleared at the flow's start), for the watch, the retry decision and the report.
function Get-FlowCrashLogs {
    $ErrorActionPreference = 'Continue'
    $crash = Invoke-AdbBounded @('-s', $script:Serial, 'logcat', '-b', 'crash', '-d')
    $anr = Invoke-AdbBounded @('-s', $script:Serial, 'logcat', '-b', 'system,events', '-d', '-s', 'ActivityManager:E', 'am_anr:I')
    return [pscustomobject]@{ Crash = "$crash"; Anr = "$anr" }
}

function Get-FlowCrashLines([string]$Package) {
    $ErrorActionPreference = 'Continue'
    $crash = Invoke-AdbBounded @('-s', $script:Serial, 'logcat', '-b', 'crash', '-d')
    $anr = Invoke-AdbBounded @('-s', $script:Serial, 'logcat', '-b', 'system,events', '-d', '-s', 'ActivityManager:E', 'am_anr:I')
    return @(Find-AppCrashLines "$crash" "$anr" $Package)
}

# ---- Slice 247 items e + c: the in-run crash watch and the one retry of a Mono SIGSEGV at launch
$script:CrashWatchPollSeconds = 2      # the watch looks at the crash buffer this often, so the Maestro tree is killed well inside 10 s of the crash
$script:MonoRetryMaxUptimeSeconds = 20 # a Mono SIGSEGV this soon after the process started is a bad cold start, retried once

# Pure: the raw crash-buffer and ANR text of a flow, as the watch and the failure report read them.
function Test-AppCrashed([string]$CrashLog, [string]$AnrLog, [string]$Package) {
    return (@(Find-AppCrashLines $CrashLog $AnrLog $Package).Count -gt 0)
}

# Pure: "Process uptime: 6s" of the tombstone header after the app's Fatal signal line, or $null when the log has none.
function Get-CrashProcessUptime([string]$CrashLog, [string]$Package) {
    $block = Get-PackageSigsegvBlock $CrashLog $Package
    if (-not $block) { return $null }
    $m = [regex]::Match($block, 'Process uptime:\s*(\d+)s')
    if (-not $m.Success) { return $null }
    return [int]$m.Groups[1].Value
}

# Pure: the text of the first native SIGSEGV block (from its "Fatal signal 11 (SIGSEGV)" line to the next such line) that names this package. The signal line shows a truncated
# process name (".dinoreader.dbg"), so the package is read from the ">>> <package> <<<" or "Cmdline: <package>" line of the block. $null when there is none.
function Get-PackageSigsegvBlock([string]$CrashLog, [string]$Package) {
    $pkg = [regex]::Escape($Package)
    $found = $null
    foreach ($block in @("$CrashLog" -split '(?=Fatal signal 11 \(SIGSEGV\))' | Where-Object { $_ -match '^Fatal signal 11 \(SIGSEGV\)' })) {
        if ($block -match ">>> $pkg <<<" -or $block -match "Cmdline:\s*$pkg\b" -or $block -match "pid \d+ \($pkg\)") { $found = $block }   # the last one: the crash that ended the flow, not an earlier one of a relaunch
    }
    return $found
}

# The crash probe of the watch: a closure over its own locals ($script: is empty inside a closure), reading the crash buffer of one device. $Seen.CrashLog keeps the text that showed
# the crash, so the flow's verdict and the retry rule use what the watch saw even when a later adb read fails. $Reader takes the serial and returns the buffer text (a test passes its own).
function New-AppCrashProbe([string]$Serial, [string]$Package, [hashtable]$Seen, [scriptblock]$Reader = { param($s) Invoke-AdbBounded @('-s', $s, 'logcat', '-b', 'crash', '-d') }) {
    $isCrashed = ${function:Test-AppCrashed}   # a closure cannot see script-scope functions, so the function itself is captured
    return {
        $text = "$(& $Reader $Serial)"
        $crashed = & $isCrashed $text '' $Package
        if ($crashed) { $Seen.CrashLog = $text }
        return $crashed
    }.GetNewClosure()
}

# Pure: true for a Mono native SIGSEGV (libmonosgen in the backtrace or the .NET thread-pool thread) of this package within MaxUptime seconds of the process start. Any other crash is not retried.
function Test-RetryableMonoCrash([string]$CrashLog, [string]$Package, [int]$MaxUptime = 20) {
    $block = Get-PackageSigsegvBlock $CrashLog $Package
    if (-not $block) { return $false }
    if ($block -notmatch 'libmonosgen|\.NET TP Worker') { return $false }
    $uptime = Get-CrashProcessUptime $CrashLog $Package
    return ($null -ne $uptime -and $uptime -le $MaxUptime)
}

# Pure: one summary line per flow that was crash-retried (even when the second run passed), with the first run's time, so the retry stays visible at the end of the run.
function Format-RetrySummary([object[]]$Results) {
    return @(@($Results) | Where-Object { $_.PSObject.Properties['Retried'] -and $_.Retried } | ForEach-Object { 'crash-retried {0} (first run {1:n1} s, counted in the row): {2}' -f $_.Name, $_.FirstRunSeconds, $_.Retried })
}

# Pure: the line printed when a flow's first run crashed at launch and was run again.
function Format-CrashRetryLine([string]$FlowName, [string]$CrashLine) {
    return ('CRASH-RETRIED {0}: the app died with a Mono SIGSEGV within {1} s of its start; the flow ran once more. First run: {2}' -f $FlowName, $script:MonoRetryMaxUptimeSeconds, $CrashLine)
}

# Kills a process and every child (Maestro is maestro.bat -> cmd -> java), then reports whether it is gone.
function Stop-ProcessTree([int]$ProcessId) {
    & taskkill.exe /T /F /PID $ProcessId 2>&1 | Out-Null
}

# Pure: the "Maestro failed:" text, the last 5 lines of stderr and of stdout, each labelled (box-drawing progress lines left out), so neither stream's failure line is lost.
function Format-MaestroFailure([string]$Stderr, [string]$Stdout) {
    # Box-drawing characters built from code points so this file stays pure ASCII (Windows PowerShell 5.1 reads a BOM-less file as ANSI).
    $boxChars = '[' + [char]0x2502 + [char]0x256D + [char]0x2570 + ']'
    $parts = foreach ($stream in @(@('stderr', $Stderr), @('stdout', $Stdout))) {
        $lines = "$($stream[1])" -split "`r?`n" | Where-Object { $_.Trim() -and $_ -notmatch $boxChars }
        "--- $($stream[0]) (last lines) ---`n" + ((@($lines) | Select-Object -Last 5) -join "`n")
    }
    return "Maestro failed:`n" + ($parts -join "`n")
}

# A probe that throws (adb failed) counts as "no crash this poll": the watch goes on and the Maestro tree is still waited for and killed at the end, never left running.
function Invoke-CrashProbeSafely([scriptblock]$CrashProbe) {
    try { return [bool](& $CrashProbe) }
    catch { Write-Host "WATCH probe error ignored: $($_.Exception.Message)"; return $false }
}

# Waits for a started process; every poll interval it asks $CrashProbe (returns $true once the app crashed). On a crash the tree is killed and $true returned.
# Debug line per branch: WATCH crash / WATCH done, so a device run can tell a killed Maestro from one that ended by itself.
function Wait-ProcessWithCrashWatch($Process, [scriptblock]$CrashProbe, [int]$PollSeconds = 2) {
    while (-not $Process.WaitForExit($PollSeconds * 1000)) {
        if (Invoke-CrashProbeSafely $CrashProbe) {
            Write-Host "WATCH crash: the app crashed while Maestro ran; killing the Maestro process tree (pid $($Process.Id))."
            Stop-ProcessTree $Process.Id
            $null = $Process.WaitForExit(5000)
            return $true
        }
    }
    return $false
}

# ---- Slice 247 item a: per-phase times of one flow from Maestro's commands.json
$script:LaunchCommands = @('launchAppCommand', 'clearStateCommand', 'stopAppCommand', 'killAppCommand', 'clearKeychainCommand')
$script:WaitCommands = @('assertConditionCommand', 'assertTrueCommand', 'waitForAnimationToEndCommand', 'assertNoDefectsWithAiCommand')

# Pure: depth-0 commands of commands.json classified as launch (launchApp/clearState/stopApp), wait (assertCondition = extendedWaitUntil, assertVisible) or action (everything else:
# taps, input, swipes, runFlow). Maestro cannot tell a first-run wizard from the body, so the wizard shows as the slowest wait/action. Returns Launch/Wait/Action seconds,
# Largest (the biggest phase name) and Slowest (the one command that took longest: "<command> <seconds> s <status>").
function Get-FlowPhaseTimes([string]$CommandsJson) {
    $phases = [ordered]@{ Launch = 0.0; Wait = 0.0; Action = 0.0 }
    $slowest = $null
    try { $parsed = ConvertFrom-Json "$CommandsJson" -ErrorAction Stop } catch { $parsed = $null }   # a file cut short by the crash watch's kill: a diagnostic line never breaks the report
    $commands = @($parsed | ForEach-Object { $_ })   # Windows PowerShell 5.1 returns a JSON array as one object; the pipe unrolls it
    foreach ($c in $commands) {
        if (-not $c.command -or -not $c.metadata) { continue }
        $name = @($c.command.PSObject.Properties.Name)[0]
        if ($name -eq 'defineVariablesCommand' -or $name -eq 'applyConfigurationCommand') { continue }
        if ([int]$c.metadata.depth -ne 0) { continue }
        $seconds = [double]$c.metadata.duration / 1000.0
        $phase = if ($script:LaunchCommands -contains $name) { 'Launch' } elseif ($script:WaitCommands -contains $name) { 'Wait' } else { 'Action' }
        $phases[$phase] += $seconds
        if (-not $slowest -or $seconds -gt $slowest.Seconds) { $slowest = [pscustomobject]@{ Name = ($name -replace 'Command$', ''); Seconds = $seconds; Status = "$($c.metadata.status)" } }
    }
    $largest = ($phases.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1)
    return [pscustomobject]@{
        Launch = $phases['Launch']; Wait = $phases['Wait']; Action = $phases['Action']
        Largest = if ($largest -and $largest.Value -gt 0) { $largest.Key } else { 'none' }
        Slowest = if ($slowest) { ('{0} {1:n1} s {2}' -f $slowest.Name, $slowest.Seconds, $slowest.Status) } else { 'none' }
    }
}

# Pure: `PHASES <flow>: launch 4.8 s, wait 30.4 s, action 0.0 s, other 3.1 s (Maestro start-up, log waits); largest: wait; slowest: assertCondition 25.2 s FAILED`.
function Format-FlowPhases([string]$FlowName, $Phases, [double]$TotalSeconds) {
    $other = [Math]::Max(0.0, $TotalSeconds - $Phases.Launch - $Phases.Wait - $Phases.Action)
    return ('PHASES {0}: launch {1:n1} s, wait {2:n1} s, action {3:n1} s, other {4:n1} s (Maestro start-up, driver, log markers); largest: {5}; slowest: {6}' -f $FlowName, $Phases.Launch, $Phases.Wait, $Phases.Action, $other, $Phases.Largest, $Phases.Slowest)
}

# The newest commands*.json Maestro wrote under the debug-output folder since $Since, or $null.
function Find-FlowCommandsFile([string]$MaestroOut, [datetime]$Since) {
    $hit = Get-ChildItem $MaestroOut -Recurse -Filter 'commands*.json' -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $Since } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($hit) { return $hit.FullName }
    return $null
}

# ---- item 4: which flows go to the second emulator
$script:SplitMinimumFlows = 4
$script:SplitBootSeconds = 180   # the second emulator's boot is waited for this long; then its flows run on emulator 1 instead
# Pure: with the split asked for (opt-in: two emulators overloaded the PC, Slice 207) and $SplitMinimumFlows or more flows, the second half (rounded down) goes to instance 2; else none.
function Split-FlowNames([string[]]$Names, [bool]$SplitAsked) {
    $all = @($Names)
    if (-not $SplitAsked -or $all.Count -lt $script:SplitMinimumFlows) { return [pscustomobject]@{ First = $all; Second = @() } }
    $half = [int][Math]::Floor($all.Count / 2)
    return [pscustomobject]@{ First = @($all[0..($all.Count - $half - 1)]); Second = @($all[($all.Count - $half)..($all.Count - 1)]) }
}

# Pure: the flow names of a -Flow value (a name, a path, or a comma list; split on commas only, so a path with a space stays one flow).
function Split-FlowList([string[]]$Value) {
    return @(@($Value) | ForEach-Object { "$_" -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ } | ForEach-Object { Get-FlowName $_ })
}
