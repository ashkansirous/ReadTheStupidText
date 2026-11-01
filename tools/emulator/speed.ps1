# Slice 241: faster emulator runs. Dot-sourced by common.ps1, not run. Pure parts are covered by test-speed.ps1.
. (Join-Path $PSScriptRoot '..\detached-process.ps1')

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

function Get-FlowCrashLines([string]$Package) {
    $ErrorActionPreference = 'Continue'
    $crash = Invoke-AdbBounded @('-s', $script:Serial, 'logcat', '-b', 'crash', '-d')
    $anr = Invoke-AdbBounded @('-s', $script:Serial, 'logcat', '-b', 'system,events', '-d', '-s', 'ActivityManager:E', 'am_anr:I')
    return @(Find-AppCrashLines "$crash" "$anr" $Package)
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
