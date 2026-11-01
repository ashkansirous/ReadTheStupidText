<#
.SYNOPSIS
  Runs the Maestro flows of one app (.maestro/<app>/*.yaml) on the warm emulator and prints pass/fail only (Slice 162).
.DESCRIPTION
  Maestro is found in $env:MAESTRO_HOME, else <worktrees folder>\.tools\maestro (shared by all worktrees).
  A flow may start with comment lines `# expect-log: <text>`: after the flow ends, each text must appear in the
  RTST-TEST logcat lines (the Debug-only markers) within -LogTimeoutSeconds. Failures print the last lines of Maestro's output.
  The debug app must already be installed (install-debug.ps1 -App rtst|dino); -Install does that first.
  Slice 180: -App auto runs only the apps the change touches (tools/test-targets.ps1) and prints the ones it skipped and why;
  -Instance 1|2 picks the emulator (default 1; start it with start-warm.ps1 -Instance n).
  Slice 212: before every flow the emulator is woken and its keyguard dismissed; a flow whose header says `# needs: cellular-only` runs with Wi-Fi off (mobile data
  on) and Wi-Fi is switched back on after it, also when it fails.
  Slice 207: lint-flows.ps1 runs first and stops the run when it finds a problem. A lease on the emulator (tools/lease-common.ps1, owner from
  $env:RTST_DEVICE_OWNER, else $env:RTST_PHONE_OWNER, else run-flows-<pid>) keeps two runs off one serial: the second prints `LEASE waiting on <owner>`
  once a minute for up to -LeaseWaitMinutes (default 15), then exits 4 naming the holder.
  Slice 215: before every flow, and after a failed one, `pm path android` must answer and sys.boot_completed must be 1. If not, it prints `EMULATOR UNHEALTHY: <what>`, saves
  evidence (crash log, dumpsys meminfo, host free RAM and the qemu working set) under maestro-out\i<n>\unhealthy-*, restarts the emulator from a neutral folder and runs the flow once more.
  The second unhealthy check of a run exits 5 naming it.
  Slice 225: -Flow takes a flow name (`-Flow record-page-shows-words`); a path to the yaml is reduced to its name. It also finds a flow under .maestro/<app>/needs-file/ (run by name only; push its file first).
  The install check reads `pm list packages` one line per package; a stale Maestro driver is force-stopped before the ANR-dialog UI dump.
#>
param(
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino', 'auto')][string]$App,
    [ValidateSet(1, 2)][int]$Instance = 1,
    [string]$Flow,
    [switch]$Install,
    [int]$LogTimeoutSeconds = 40,
    # Slice 207: how long to wait for another run on the same emulator to end before giving up (exit 4, naming the holder).
    [int]$LeaseWaitMinutes = 15
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'build-cache.ps1')
Set-EmulatorInstance $Instance

# Slice 207: a flow with a wrong command name, a bad escape or an unexplained clearState fails here, before any build or device time is spent.
& (Join-Path $PSScriptRoot 'lint-flows.ps1')
if ($LASTEXITCODE -ne 0) { Write-Host 'The flow lint failed (see above): fix the flows first. Nothing was built or run.'; exit 1 }

# -App auto: one run per touched app, each through this same script, so a failing app does not hide the next one.
if ($App -eq 'auto') {
    $touched = @(& (Join-Path $PSScriptRoot '..\test-targets.ps1') -Platform android -Raw)
    if ($touched.Count -eq 0) { Write-Host 'NO ANDROID APP IS TOUCHED BY THIS CHANGE: no flows ran. That is a skip, not a test pass.'; exit 0 }
    $worst = 0
    foreach ($name in $touched) {
        $flowArgs = if ($Flow) { @('-Flow', (Get-FlowName $Flow)) } else { @() }
        & $PSCommandPath -App $name -Instance $Instance -Install:$Install -LogTimeoutSeconds $LogTimeoutSeconds @flowArgs
        if ($LASTEXITCODE -ne 0) { $worst = 1 }
    }
    exit $worst
}

$packages = $script:EmulatorPackages
$logTag = 'RTST-TEST'

function Find-Maestro {
    $candidates = @()
    if ($env:MAESTRO_HOME) { $candidates += Join-Path $env:MAESTRO_HOME 'bin\maestro.bat' }
    $candidates += Join-Path (Split-Path $script:StateDir -Parent) '.tools\maestro\bin\maestro.bat'
    $found = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $found) { throw "Maestro not found. Unzip maestro.zip (github.com/mobile-dev-inc/maestro/releases) to $($candidates[-1] -replace '\\bin\\maestro.bat$') or set MAESTRO_HOME." }
    return $found
}

function Get-ExpectedLogs([string]$FlowFile) {
    Get-Content $FlowFile | Where-Object { $_ -match '^#\s*expect-log:\s*(.+)$' } | ForEach-Object { $Matches[1].Trim() }
}

function Get-TestLog {
    $ErrorActionPreference = 'Continue'
    return (& $script:Adb -s $script:Serial logcat -d -s $logTag 2>&1 | Out-String)
}

# Whole-token match: `PAGE 1` must not match `PAGE 10`.
function Test-LogMarker([string]$Log, [string]$Marker) {
    return $Log -match ('(?<!\S)' + [regex]::Escape($Marker) + '(?!\S)')
}

# Returns the expected texts still missing from the marker log after the timeout.
function Wait-LogMarkers([string[]]$Expected) {
    $deadline = (Get-Date).AddSeconds($LogTimeoutSeconds)
    do {
        $log = Get-TestLog
        $missing = @($Expected | Where-Object { -not (Test-LogMarker $log $_) })
        if ($missing.Count -eq 0) { return @() }
        Start-Sleep -Milliseconds 700
    } while ((Get-Date) -lt $deadline)
    return $missing
}

# "<app> isn't responding" dialogs (System UI ANR after a CPU-starved boot) block every later flow until the emulator restarts.
# Returns the dialog's title text when one is on screen, else $null.
function Get-AnrDialogXml {
    $ErrorActionPreference = 'Continue'
    $xml = Get-UiDumpXml   # Slice 225 item 12: force-stops a stale Maestro driver first (its "Killed" breaks the dump)
    if ($xml -match "isn't responding|aerr_wait|aerr_close") { return $xml }
    return $null
}

function Get-NodeCenter([string]$Xml, [string]$ResourceId) {
    $m = [regex]::Match($Xml, 'resource-id="' + [regex]::Escape($ResourceId) + '"[^>]*?bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"')
    if (-not $m.Success) { return $null }
    $g = $m.Groups
    return @([int](([int]$g[1].Value + [int]$g[3].Value) / 2), [int](([int]$g[2].Value + [int]$g[4].Value) / 2))
}

# Taps "Wait" (else "Close app") on an ANR dialog and says so; throws, naming the dialog, when it cannot clear it.
function Clear-AnrDialog([string]$When) {
    $ErrorActionPreference = 'Continue'
    for ($try = 1; $try -le 3; $try++) {
        $xml = Get-AnrDialogXml
        if (-not $xml) { return }
        $point = (Get-NodeCenter $xml 'android:id/aerr_wait'), (Get-NodeCenter $xml 'android:id/aerr_close') | Where-Object { $_ } | Select-Object -First 1
        if ($point) { & $script:Adb -s $script:Serial shell input tap $point[0] $point[1] | Out-Null }
        else { & $script:Adb -s $script:Serial shell input keyevent KEYCODE_BACK | Out-Null }
        Write-Host "Dismissed an 'isn't responding' (ANR) dialog $When on $($script:Serial)."
        Start-Sleep -Seconds 2
    }
    if (Get-AnrDialogXml) { throw "An 'isn't responding' (ANR) dialog is on screen on $($script:Serial) $When and could not be dismissed; restart the emulator (stop.ps1 -Instance $Instance, then start-warm.ps1)." }
}

# Slice 212: the values a flow's `# needs:` header line may carry (lint-flows.ps1 rejects any other); a flow with one runs under that network state.
function Get-FlowNeeds([string]$FlowFile) {
    $line = Get-Content $FlowFile | Where-Object { $_ -match '^#\s*needs:\s*(.+)$' } | Select-Object -First 1
    if (-not $line) { return @() }
    return @(($line -replace '^#\s*needs:\s*', '') -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

# Slice 212: after start-warm the emulator sits on the lock screen and the first flow would fail at its first assert; wake it and dismiss the keyguard before every flow.
function Wake-Emulator {
    & $script:Adb -s $script:Serial shell input keyevent KEYCODE_WAKEUP | Out-Null
    & $script:Adb -s $script:Serial shell wm dismiss-keyguard | Out-Null
}

# Slice 212: `# needs: cellular-only` = Wi-Fi off and mobile data on, so the Wi-Fi-only download card shows "Download now" instead of starting the download.
function Set-CellularOnly {
    & $script:Adb -s $script:Serial shell svc wifi disable | Out-Null
    $data = (& $script:Adb -s $script:Serial shell settings get global mobile_data | Out-String).Trim()
    if ($data -ne '1') { & $script:Adb -s $script:Serial shell svc data enable | Out-Null }
}

function Restore-Wifi { & $script:Adb -s $script:Serial shell svc wifi enable | Out-Null }

# Slice 215: restarts the emulator from a neutral folder (never from inside a worktree, so the emulator process never holds one open) and reinstalls
# the app when the restart lost it. Returns after the boot; the caller re-checks the health.
function Restart-Emulator {
    $null = New-Item -ItemType Directory -Force $script:StateDir
    Push-Location $script:StateDir
    try {
        Write-Host "EMULATOR RESTART: stopping and starting $($script:Serial) from $($script:StateDir)."
        & (Join-Path $PSScriptRoot 'stop.ps1') -Instance $Instance | Out-Null
        & (Join-Path $PSScriptRoot 'start-warm.ps1') -Instance $Instance | Out-Null
    } finally { Pop-Location }
    $installed = & $script:Adb -s $script:Serial shell pm list packages $packages[$App] 2>&1 | Out-String
    $stale = (Get-InstalledKey $App) -ne (Get-BuildKey)  # a restart on a different baseline: the installed build is not the current one
    if ($stale -or -not ($installed -match [regex]::Escape($packages[$App]))) { $null = & (Join-Path $PSScriptRoot 'install-debug.ps1') -App $App -Instance $Instance }
}

# Slice 215: the health check before a flow and after a failed one. Returns $true when the emulator is healthy now (after one restart when it was not).
# The second consecutive unhealthy check (the count resets when a flow ends on a healthy emulator) stops it (exit 5) naming the problem: the emulator keeps dying, and a flow result on it means nothing.
function Confirm-EmulatorHealthy([string]$When) {
    $problem = Get-EmulatorHealthProblem
    if (-not $problem) { return $true }
    $script:UnhealthyCount++
    Write-Host "EMULATOR UNHEALTHY ($When): $problem"
    Save-EmulatorEvidence (Join-Path $script:MaestroOut ("unhealthy-{0}-{1}" -f (Get-Date -Format 'yyyyMMdd-HHmmss'), $script:UnhealthyCount))
    if ($script:UnhealthyCount -ge 2) { Write-Host "EMULATOR UNHEALTHY twice in this run (last: $problem). Stopping: restart it by hand and look at the evidence folder under $($script:MaestroOut)."; exit $script:ExitEmulatorUnhealthy }
    Restart-Emulator
    $again = Get-EmulatorHealthProblem
    if ($again) { $script:UnhealthyCount++; Write-Host "EMULATOR UNHEALTHY after the restart: $again. Stopping."; exit $script:ExitEmulatorUnhealthy }
    Write-Host "Emulator healthy again after the restart; the flow runs once more."
    return [bool]$false
}

# One flow with the health guard: checked before it; when that check restarted the emulator the flow still runs once. A failed flow is checked again, so an
# emulator death is reported as that (and the flow rerun once on the restarted emulator), never as a flow failure.
function Invoke-GuardedFlow([string]$Maestro, [System.IO.FileInfo]$File) {
    $null = Confirm-EmulatorHealthy "before $($File.BaseName)"
    $r = Invoke-Flow $Maestro $File
    if (-not $r.Problem) { $script:UnhealthyCount = 0; return $r }  # a flow that passed: the emulator is fine, so only consecutive deaths count
    if (Confirm-EmulatorHealthy "after the failed flow $($File.BaseName)") { $script:UnhealthyCount = 0; return $r }
    Write-Host "The failure of $($File.BaseName) came with a dead emulator, so it says nothing about the app; running the flow again."
    $rerun = Invoke-Flow $Maestro $File
    if (-not (Get-EmulatorHealthProblem)) { $script:UnhealthyCount = 0 }  # the rerun ended on a healthy emulator: this death is over, so a later separate one is the first again
    return $rerun
}

function Invoke-Flow([string]$Maestro, [System.IO.FileInfo]$File) {
    $ErrorActionPreference = 'Continue'
    $cellular = (Get-FlowNeeds $File.FullName) -contains 'cellular-only'
    Wake-Emulator
    if ($cellular) { Set-CellularOnly }
    try { return Invoke-FlowBody $Maestro $File }
    finally { if ($cellular) { Restore-Wifi } }  # a failed flow never leaves Wi-Fi off for the next one
}

function Invoke-FlowBody([string]$Maestro, [System.IO.FileInfo]$File) {
    $ErrorActionPreference = 'Continue'
    & $script:Adb -s $script:Serial logcat -c
    $sw = [Diagnostics.Stopwatch]::StartNew()
    # Applied to the Maestro call only (a JVM reads JAVA_TOOL_OPTIONS whatever the launcher does with MAESTRO_OPTS); the Android build's Java must keep the real user.home.
    $env:JAVA_TOOL_OPTIONS = "-Duser.home=$($script:MaestroHome)"
    # --no-reinstall-driver only skips a reinstall: Maestro still installs its driver when it is missing, which is how the first flow of a run (and the rerun after a restart) gets it.
    try { $output = & $Maestro --device $script:Serial test --no-reinstall-driver --debug-output $script:MaestroOut $File.FullName 2>&1 | Out-String }
    finally { Remove-Item Env:JAVA_TOOL_OPTIONS -ErrorAction SilentlyContinue }
    $exit = $LASTEXITCODE
    $problem = $null
    if ($exit -ne 0) {
        $lines = $output -split "`r?`n" | Where-Object { $_.Trim() -and $_ -notmatch '[│╭╰]' }
        $problem = "Maestro failed:`n" + (($lines | Select-Object -Last 10) -join "`n")
    }
    else {
        $missing = Wait-LogMarkers @(Get-ExpectedLogs $File.FullName)
        if ($missing.Count -gt 0) { $problem = "Missing log marker(s) under tag ${logTag}: " + ($missing -join '; ') }
    }
    return [pscustomobject]@{ Name = $File.BaseName; Seconds = $sw.Elapsed.TotalSeconds; Problem = $problem }
}

# Slice 207: the APK is built (or found in the cache) before the emulator lease is taken, so a cold build never holds the emulator or runs a waiter's wait out.
# Nothing is built when the installed app already matches the current inputs and -Install is not asked for.
function Build-BeforeLease {
    $key = Get-BuildKey
    $needed = $Install -or ((Get-InstalledKey $App) -ne $key)
    if ($needed -and -not (Find-CachedBuild (Get-BuildDir $App (Get-EmulatorBuildPlatform $App) $key) '*-Signed.apk')) {
        & (Join-Path $PSScriptRoot 'install-debug.ps1') -App $App -Instance $Instance -BuildOnly
    }
}

# Renews the lease; stops the run (exit 4) when another run has taken the emulator since, because two runs on one serial is what the lease prevents.
# Takes the activity slot, but in short waits with the lease renewed between them: the wait for a free slot can outlast the lease (30 min).
function Start-FlowsActivity([string]$Kind, [string]$Owner) {
    $deadline = (Get-Date).AddMinutes($script:ActivityWaitMinutes)
    while ($true) {
        try { return (Start-Activity $Kind $script:ActivityWaitSliceMinutes) }
        catch {
            if ((Get-Date) -ge $deadline -or "$($_.Exception.Message)" -notmatch '^Not starting ') { throw }
            Assert-LeaseHeld $Owner 'while this run waited for a free slot'
        }
    }
}

function Assert-LeaseHeld([string]$Owner, [string]$When) {
    if (-not (Confirm-DeviceLease $script:Serial $Owner $script:EmulatorLeaseMinutes 'emulator')) {
        Write-Host "LEASE lost: another run took $($script:Serial) $When. Stopping so two runs never share one emulator."
        exit $script:ExitLeaseHeld
    }
}

$script:UnhealthyCount = 0  # Slice 215: emulator deaths seen in this run; the second one stops it (exit 5)
$script:ActivityWaitMinutes = 90
$script:ActivityWaitSliceMinutes = 10
Build-BeforeLease
# One run on an emulator at a time. A second run waits for the first (a clearState in one would wipe the other's book); taken right before the install
# and released in the finally below (only when this run took it: a waiter that gave up must not remove the holder's lease).
$leaseOwner = Get-DeviceLeaseOwner 'run-flows'
$leaseTaken = $false
try {
    $heldBy = Wait-ForLease $script:Serial $leaseOwner $script:EmulatorLeaseMinutes $LeaseWaitMinutes 'emulator'
    if ($heldBy) { Write-Host "LEASE held: $($script:Serial) is used by $heldBy; waited $LeaseWaitMinutes min. Not running flows."; exit $script:ExitLeaseHeld }
    $leaseTaken = $true
    $maestro = Find-Maestro
    $env:MAESTRO_CLI_NO_ANALYTICS = '1'
    # Maestro keeps its state and logs under the Java user.home: put them in the shared tools folder, not the user profile.
    $toolsDir = Split-Path (Split-Path (Split-Path $maestro -Parent) -Parent) -Parent
    $script:MaestroOut = Join-Path $toolsDir "maestro-out\i$Instance"
    # One Java user.home per emulator instance, so two flows runs at once never share Maestro's port forwarding or state.
    $script:MaestroHome = Join-Path $toolsDir "maestro-home\i$Instance"
    $null = New-Item -ItemType Directory -Force $script:MaestroOut, $script:MaestroHome
    Write-Host "Instance ${Instance}: Maestro home $($script:MaestroHome)"
    $env:MAESTRO_OPTS = "-Duser.home=$($script:MaestroHome)"
    $env:MAESTRO_CLI_NO_ANALYTICS_NOTIFICATION = '1'
    if ($Install) { & (Join-Path $PSScriptRoot 'install-debug.ps1') -App $App -Instance $Instance }
    else { & (Join-Path $PSScriptRoot 'start-warm.ps1') -Instance $Instance | Out-Null }

    # Slice 215: health first. A dead system_server must be reported and restarted here, not surface below as "not installed" (2026-10-31 kill test).
    $null = Confirm-EmulatorHealthy 'before the install check'

    # Without -Install the installed app must be the build for the current inputs; a stale one is replaced (from the cache when it exists).
    if (-not $Install) {
        $currentKey = Get-BuildKey
        if ((Get-InstalledKey $App) -ne $currentKey) {
            Write-Host "The installed $App build is not the one for the current inputs (key $currentKey); installing it first."
            & (Join-Path $PSScriptRoot 'install-debug.ps1') -App $App -Instance $Instance
        }
    }

    $ErrorActionPreference = 'Continue'
    $installed = & $script:Adb -s $script:Serial shell pm list packages $packages[$App]
    $ErrorActionPreference = 'Stop'
    $installProblem = Resolve-InstallProblem (Join-PmLines $installed) $packages[$App]   # Slice 225 item 4: one line per package, so two matching packages are both seen
    if ($installProblem -and $installProblem -like 'EMULATOR UNHEALTHY*') { Write-Host $installProblem; $null = Confirm-EmulatorHealthy 'at the install check'; $installed = & $script:Adb -s $script:Serial shell pm list packages $packages[$App]; $installProblem = Resolve-InstallProblem (Join-PmLines $installed) $packages[$App] }
    if ($installProblem) { throw "$installProblem; run install-debug.ps1 -App $App (or pass -Install)." }

    $flowDir = Join-Path $script:RepoRoot ".maestro\$App"
    $files = @(Find-FlowFiles $flowDir $Flow)   # Slice 225: -Flow also finds a flow under needs-file/ and accepts a path
    if (-not $files) { throw "No flows found in $flowDir$(if ($Flow) { " named $(Get-FlowName $Flow) (-Flow takes a flow name, e.g. record-page-shows-words, not a path)" })." }

    $total = [Diagnostics.Stopwatch]::StartNew()
    $activity = Start-FlowsActivity "flows-$App-i$Instance" $leaseOwner
    try {
        Assert-LeaseHeld $leaseOwner 'while this run waited for a free slot'  # the wait for the quiet machine can be long
        Clear-AnrDialog 'before the flows'
        $results = foreach ($file in $files) {
            Assert-LeaseHeld $leaseOwner 'during the flows'  # renewed between flows
            $r = Invoke-GuardedFlow $maestro $file
            Write-Host ("{0,-5} {1} ({2:n1} s)" -f $(if ($r.Problem) { 'FAIL' } else { 'PASS' }), $r.Name, $r.Seconds)
            if ($r.Problem) { Write-Host ($r.Problem -replace '(?m)^', '      ') }
            $r
        }
        Clear-AnrDialog 'after the flows'
    } finally { Stop-Activity $activity }
    $failed = @($results | Where-Object { $_.Problem }).Count
    Write-Host ("{0}: {1} passed, {2} failed in {3:n1} s." -f $App, (@($results).Count - $failed), $failed, $total.Elapsed.TotalSeconds)
    if ($failed -gt 0) { exit 1 }
} finally { if ($leaseTaken) { Remove-Lease $script:Serial $leaseOwner 0 'emulator' | Out-Null } }
