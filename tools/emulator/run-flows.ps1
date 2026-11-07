<#
.SYNOPSIS
  Runs the Maestro flows of one app (.maestro/<app>/*.yaml) on the warm emulator and prints pass/fail only (Slice 162).
.DESCRIPTION
  Maestro is found in $env:MAESTRO_HOME, else <worktrees folder>\.tools\maestro (shared by all worktrees).
  A flow may start with comment lines `# expect-log: <text>`: after the flow ends, each text must appear in the
  RTST-TEST logcat lines (the Debug-only markers) within -LogTimeoutSeconds. Failures print the last lines of Maestro's output.
  The debug app must already be installed (install-debug.ps1 -App rtst|dino); -Install does that first.
  Slice 180: -App auto runs only the apps the change touches (tools/test-targets.ps1) and prints the ones it skipped and why;
  -Instance 1|2|3|tablet picks the emulator (default 1; start it with start-warm.ps1 -Instance n; 3 or tablet is the tablet AVD RTST_Tablet).
  Slice 242: -NoBuild (or RTST_NO_BUILD=1) starts no build anywhere in the run: a cache miss prints the "cache missed" line and exits 6 (with -App auto: 6 unless a run really failed, then 1).
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
  Slice 241: (1) when a failed flow shows the app crashed or hit an ANR (crash buffer / ActivityManager), the remaining flows are NOT RUN (reported with the crash lines) instead of each waiting out
  its timeouts. (2) The emulator boots from its clean snapshot (start-warm.ps1; -ColdBoot skips it). (3) On a build cache miss the emulator boots while the APK builds. (4) One call runs every requested
  flow (-Flow takes a comma list); with -Split (or RTST_SPLIT=1) and 4 or more flows the second half runs at the same time on emulator 2 (booted and installed then, stopped after). The split is opt-in
  (default: one emulator, Slice 207's limit); emulator 2 gets 180 s to boot, then its flows run on emulator 1 instead.
#>
param(
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino', 'auto')][string]$App,
    [ValidateSet('1', '2', '3', 'tablet')][string]$Instance = '1',   # Slice 242: 3 or 'tablet' is the tablet AVD RTST_Tablet (new-avd.ps1 -Tablet)
    [switch]$NoBuild,   # Slice 242: start no build; on a cache miss print `cache missed for <app>, key <k>: run install-debug.ps1 -BuildOnly first` and exit 6 (also RTST_NO_BUILD=1)
    [string[]]$Flow,   # one flow name or path, or a comma list (Slice 241)
    [switch]$Split,   # Slice 241: opt in to the second emulator for 4+ flows (default one emulator; RTST_SPLIT=1 does the same)
    [switch]$ColdBoot,   # Slice 241: boot without the clean snapshot
    [switch]$Install,
    [int]$LogTimeoutSeconds = 40,
    # Slice 207: how long to wait for another run on the same emulator to end before giving up (exit 4, naming the holder).
    [int]$LeaseWaitMinutes = 15
)
$ErrorActionPreference = 'Stop'
# A script error ends the run with exit 1 (after the finally blocks), so a run that errored never looks green to a caller that reads the exit code.
trap { Write-Host "run-flows.ps1: $($_.Exception.Message)"; exit 1 }
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'build-cache.ps1')
. (Join-Path $PSScriptRoot 'layout-check.lib.ps1')   # Slice 267: Get-LayoutProblems for the `# layout-check` flows (Slice 297: the param-less library, not the CLI wrapper)
$Instance = Resolve-EmulatorInstance $Instance   # 'tablet' -> '3'
Set-EmulatorInstance $Instance

# Slice 207: a flow with a wrong command name, a bad escape or an unexplained clearState fails here, before any build or device time is spent.
& (Join-Path $PSScriptRoot 'lint-flows.ps1')
if ($LASTEXITCODE -ne 0) { Write-Host 'The flow lint failed (see above): fix the flows first. Nothing was built or run.'; exit 1 }

# Slice 284 item 3: this run marks its worktree as busy with its own uniquely named file in the git dir (the agents' `device-test-running*` marks), removed in the outer finally
# below; two runs on one worktree each remove only their own. Same two functions in tools/desktop-vm/Invoke-DesktopTest.ps1 (checked by tools/emulator/test-worktree-mark.ps1).
function New-WorktreeRunMark([string]$Owner, [string]$Root) {
    try {
        $gitDir = (& git -C $Root rev-parse --absolute-git-dir 2>$null)
        if ($LASTEXITCODE -ne 0 -or -not $gitDir) { return $null }
        # Slice 284 design (two strikes): a mark is stale only if its pid is dead, or its StartTime is readable and later than the mark's write time (a reused pid).
        # An unreadable StartTime (access denied) counts as live. Each mark is judged in its own try, and our own mark never depends on reading other processes.
        foreach ($old in @(Get-ChildItem -LiteralPath $gitDir -Filter 'device-test-running-*-*-*' -File -ErrorAction SilentlyContinue)) {
            try {
                if ($old.Name -notmatch '^device-test-running-.+-(\d+)-[0-9a-f]{8}$') { continue }
                $markOwner = Get-Process -Id ([int]$Matches[1]) -ErrorAction SilentlyContinue
                $stale = -not $markOwner
                if ($markOwner) { try { $stale = $markOwner.StartTime -gt $old.LastWriteTime } catch { $stale = $false } }
                if ($stale) { Remove-Item -LiteralPath $old.FullName -Force -ErrorAction SilentlyContinue }
            } catch { continue }
        }
    } catch { return $null }
    $path = Join-Path $gitDir ("device-test-running-$Owner-$PID-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
    try { Set-Content -LiteralPath $path -Value "$Owner pid $PID started $((Get-Date).ToString('o'))" } catch { Write-Warning "Run mark not written ($($_.Exception.Message)); the run goes on without one."; return $null }
    return $path
}
function Remove-WorktreeRunMark([string]$Path) {
    if ($Path) { Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue }
}
$runMark = $null
# Slice 242: the switch must reach every nested install-debug.ps1 call (restart, stale-build install) and every child run: the variable carries it.
$priorNoBuild = $env:RTST_NO_BUILD
if ($NoBuild.IsPresent) { $env:RTST_NO_BUILD = '1' }
# The variable is restored when the script ends (also on exit), so it never leaks into the calling session. The block below is not re-indented, to keep the diff small.
try {
$runMark = New-WorktreeRunMark 'run-flows' $PSScriptRoot

# -App auto: one run per touched app, each through this same script, so a failing app does not hide the next one.
if ($App -eq 'auto') {
    $touched = @(& (Join-Path $PSScriptRoot '..\test-targets.ps1') -Platform android -Raw)
    if ($touched.Count -eq 0) { Write-Host 'NO ANDROID APP IS TOUCHED BY THIS CHANGE: no flows ran. That is a skip, not a test pass.'; exit 0 }
    $worst = 0
    foreach ($name in $touched) {
        $flowArgs = if ($Flow) { @('-Flow', ((Split-FlowList $Flow) -join ',')) } else { @() }
        & $PSCommandPath -App $name -Instance $Instance -Install:$Install -NoBuild:$NoBuild -Split:$Split -ColdBoot:$ColdBoot -LogTimeoutSeconds $LogTimeoutSeconds @flowArgs
        if ($LASTEXITCODE -eq $script:EmulatorExitCacheMissed -and $worst -eq 0) { $worst = $script:EmulatorExitCacheMissed }   # "build first" stays distinct from a failure...
        elseif ($LASTEXITCODE -ne 0) { $worst = 1 }   # ...but a real failure of any app wins
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

# Slice 269: the flow's RTST-TEST lines are kept in a file per flow before the next flow clears logcat, so a measurement trace is not lost.
function Save-FlowLogcat([string]$FlowName) {
    try {
        $dir = Join-Path $script:StateDir "logcat-i$Instance"
        $null = New-Item -ItemType Directory -Force $dir
        $path = Join-Path $dir "$App-$FlowName.log"   # Slice 275 item 5: the RTST and Dino flows share names (usage-fallback-swallowed), so the app is in the file name
        Set-Content -Path $path -Value (Get-TestLog) -Encoding UTF8   # explicit: Windows PowerShell 5.1 would write the ANSI page and garble Farsi / Norwegian trace text
        Write-Host "LOGCAT kept for ${FlowName}: $path"
    } catch { Write-Host "LOGCAT could not be kept for ${FlowName}: $($_.Exception.Message)" }
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
    Close-SystemDialogs   # Slice 253 item 1: a System UI "isn't responding" dialog that is already up is closed before the flow, not after it fails
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
        & (Join-Path $PSScriptRoot 'start-warm.ps1') -Instance $Instance -ColdBoot | Out-Null   # a dead system_server: not from a snapshot taken on the same image
    } finally { Pop-Location }
    $installed = & $script:Adb -s $script:Serial shell pm list packages $packages[$App] 2>&1 | Out-String
    $stale = (Get-InstalledKey $App) -ne (Get-BuildKey)  # a restart on a different baseline: the installed build is not the current one
    if ($stale -or -not ($installed -match [regex]::Escape($packages[$App]))) {
        $null = & (Join-Path $PSScriptRoot 'install-debug.ps1') -App $App -Instance $Instance
        Exit-OnNoBuildMiss
    }
}

# Slice 242: with -NoBuild a nested install-debug.ps1 that missed the cache exits 6; the run stops with that code instead of carrying on with a stale or missing app.
function Exit-OnNoBuildMiss {
    if ($LASTEXITCODE -eq $script:EmulatorExitCacheMissed) { exit $script:EmulatorExitCacheMissed }
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
    if (Test-RunnerStuck $r) { return $r }   # Slice 301: the one predicate before every launch; a stuck Maestro tree is never joined by a second one
    if (-not $r.Problem) { $script:UnhealthyCount = 0; return $r }  # a flow that passed: the emulator is fine, so only consecutive deaths count
    if (Confirm-EmulatorHealthy "after the failed flow $($File.BaseName)") { $script:UnhealthyCount = 0; return $r }
    Write-Host "The failure of $($File.BaseName) came with a dead emulator, so it says nothing about the app; running the flow again."
    $rerun = Invoke-Flow $Maestro $File
    if (-not (Get-EmulatorHealthProblem)) { $script:UnhealthyCount = 0 }  # the rerun ended on a healthy emulator: this death is over, so a later separate one is the first again
    return $rerun
}

# Slice 269: one known-good flow (preflight.yaml: Android's Settings) before the real ones. A 5-hour-old emulator cost 48 minutes and 7 diagnosis runs once, and the Maestro driver died right
# after a snapshot boot twice; a restart fixed both in seconds. A failed or hung preflight cold-boots the emulator (Restart-Emulator) and tries once more; a second failure stops the run (exit 5).
$script:PreflightFlow = Join-Path $PSScriptRoot 'preflight.yaml'
$script:PreflightTimeoutSeconds = 120
$script:PreflightFirstTimeoutSeconds = 300   # the first attempt may be the one that installs the Maestro driver (see Invoke-FlowBody), which takes longer than a plain flow

function Test-MaestroPreflight([string]$Maestro, [int]$TimeoutSeconds = $script:PreflightTimeoutSeconds) {
    $ErrorActionPreference = 'Continue'
    $env:JAVA_TOOL_OPTIONS = "-Duser.home=$($script:MaestroHome)"
    $p = $null
    try {
        $argLine = "--device $($script:Serial) test --no-reinstall-driver `"$($script:PreflightFlow)`""
        $p = Start-Process -FilePath $Maestro -ArgumentList $argLine -NoNewWindow -PassThru -RedirectStandardOutput (Join-Path $script:StateDir "preflight-i$Instance.out") -RedirectStandardError (Join-Path $script:StateDir "preflight-i$Instance.err")
        $null = $p.Handle
        if (-not $p.WaitForExit($TimeoutSeconds * 1000)) {
            & taskkill.exe /T /F /PID $p.Id 2>&1 | Out-Null
            Write-Host "PREFLIGHT timed out after $TimeoutSeconds s (the Maestro driver or the emulator hangs)."
            return $false
        }
        # A null ExitCode (a .bat wrapper whose handle was not cached) is a failure, never a pass.
        return ($null -ne $p.ExitCode -and $p.ExitCode -eq 0)
    } finally {
        # Whatever ended the try (timeout, a thrown wait), the Maestro tree this call started must not outlive it.
        if ($p -and -not $p.HasExited) { & taskkill.exe /T /F /PID $p.Id 2>&1 | Out-Null }
        Remove-Item Env:JAVA_TOOL_OPTIONS -ErrorAction SilentlyContinue
    }
}

function Confirm-EmulatorPreflight([string]$Maestro) {
    if (Test-MaestroPreflight $Maestro $script:PreflightFirstTimeoutSeconds) { Write-Host 'PREFLIGHT passed: the emulator and the Maestro driver answer.'; return }
    Write-Host 'PREFLIGHT failed: cold-booting the emulator and trying once more.'
    Restart-Emulator
    if (Test-MaestroPreflight $Maestro $script:PreflightFirstTimeoutSeconds) { Write-Host 'PREFLIGHT passed after the cold boot.'; return }
    Write-Host "PREFLIGHT failed again after a cold boot. Stopping: no flow result on this emulator would mean anything (output: $(Join-Path $script:StateDir "preflight-i$Instance.err"))."
    exit $script:ExitEmulatorUnhealthy
}

# Slice 267: a flow header `# layout-check` asks for the overlap / cut-off check on the screen the flow ends on; `# font-scale: 1.3` runs the flow at that system font scale (restored after it).
function Test-FlowWantsLayoutCheck([string]$FlowFile) { return [bool](Get-Content $FlowFile | Where-Object { $_ -match '^#\s*layout-check\s*$' } | Select-Object -First 1) }

function Get-FlowFontScale([string]$FlowFile) {
    $line = Get-Content $FlowFile | Where-Object { $_ -match '^#\s*font-scale:\s*([0-9.]+)\s*$' } | Select-Object -First 1
    if ($line -and $line -match '([0-9.]+)\s*$') { return $Matches[1] }
    return $null
}

function Set-FontScale([string]$Scale) { & $script:Adb -s $script:Serial shell settings put system font_scale $Scale | Out-Null }

# The scale the device had before a flow changed it ('null' = never set), so the flow's end puts that back and not a fixed 1.0.
function Get-FontScale { return ("$(& $script:Adb -s $script:Serial shell settings get system font_scale)").Trim() }

function Restore-FontScale([string]$Previous) {
    if ($Previous -and $Previous -ne 'null') { Set-FontScale $Previous; return }
    & $script:Adb -s $script:Serial shell settings delete system font_scale | Out-Null
}

function Invoke-Flow([string]$Maestro, [System.IO.FileInfo]$File) {
    $ErrorActionPreference = 'Continue'
    $cellular = (Get-FlowNeeds $File.FullName) -contains 'cellular-only'
    $fontScale = Get-FlowFontScale $File.FullName
    Wake-Emulator
    if ($cellular) { Set-CellularOnly }
    $previousScale = if ($fontScale) { Get-FontScale } else { $null }
    if ($fontScale) { Set-FontScale $fontScale }
    try { return Invoke-FlowBody $Maestro $File }
    finally {
        if ($cellular) { Restore-Wifi }  # a failed flow never leaves Wi-Fi off for the next one
        if ($fontScale) { Restore-FontScale $previousScale }  # ...nor a font scale on; the device's own value comes back
    }
}

# Slice 267: the screen the flow ended on, read from a uiautomator dump; returns the problem lines (empty = fine).
function Get-FlowLayoutProblems([string]$FlowName) {
    $dump = Get-UiDumpXml
    return @(Get-LayoutProblems $dump $FlowName)
}

function Invoke-FlowBody([string]$Maestro, [System.IO.FileInfo]$File) {
    $ErrorActionPreference = 'Continue'
    & $script:Adb -s $script:Serial logcat -b all -c   # every buffer: the crash/events buffers must not carry an earlier flow's ANR into this flow's verdict
    $sw = [Diagnostics.Stopwatch]::StartNew()
    # Applied to the Maestro call only (a JVM reads JAVA_TOOL_OPTIONS whatever the launcher does with MAESTRO_OPTS); the Android build's Java must keep the real user.home.
    $env:JAVA_TOOL_OPTIONS = "-Duser.home=$($script:MaestroHome)"
    # --no-reinstall-driver only skips a reinstall: Maestro still installs its driver when it is missing, which is how the first flow of a run (and the rerun after a restart) gets it.
    $startedAt = Get-Date
    try { $run = Invoke-MaestroWatched $Maestro $File }
    finally { Remove-Item Env:JAVA_TOOL_OPTIONS -ErrorAction SilentlyContinue }
    $exit = $run.ExitCode
    $phasesLine = Get-FlowPhasesLine $File.BaseName $startedAt $sw.Elapsed.TotalSeconds
    $problem = $null
    if ($exit -ne 0) { $problem = Format-MaestroFailure $run.Stderr $run.Stdout }
    else {
        $missing = Wait-LogMarkers @(Get-ExpectedLogs $File.FullName)
        if ($missing.Count -gt 0) { $problem = "Missing log marker(s) under tag ${logTag}: " + ($missing -join '; ') }
        elseif (Test-FlowWantsLayoutCheck $File.FullName) {
            $layout = @(Get-FlowLayoutProblems $File.BaseName)
            if ($layout.Count -gt 0) { $problem = "Layout check failed:`n" + (($layout | ForEach-Object { "  $_" }) -join "`n") }
        }
    }
    Save-FlowLogcat $File.BaseName
    # Slice 241 item 1: a failed flow is checked for an app crash / ANR, so the caller can stop instead of running the rest into the same dead app.
    $crash = @()
    $crashLog = ''
    if ($problem) {
        $logs = Get-FlowCrashLogs
        $crashLog = if ($logs.Crash.Trim()) { $logs.Crash } else { $run.CrashLog }   # a failed adb read after the kill must not hide the crash the watch saw
        $crash = @(Find-AppCrashLines $crashLog $logs.Anr $packages[$App])
    }
    if ($run.Killed) { $problem += "`nMaestro was killed by the crash watch as soon as the app crashed (Slice 247)." }
    if ($run.RunnerStuck) { $problem += "`n" + (Format-RunnerStuckLine $run.StuckPid $script:CrashKillWaitSeconds) }
    if ($crash.Count -gt 0) { $problem += "`nAPP CRASH/ANR during the flow:`n" + (($crash | ForEach-Object { "  $_" }) -join "`n") }
    return [pscustomobject]@{ Name = $File.BaseName; Seconds = $sw.Elapsed.TotalSeconds; Problem = $problem; Crash = $crash; CrashLog = $crashLog; NotRun = $null; Phases = $phasesLine; Retried = $null; FirstRunSeconds = 0.0; RunnerStuck = [bool]$run.RunnerStuck; StuckPid = [int]$run.StuckPid; RetryLogs = @() }
}

# Slice 247 item e: Maestro runs as a child process; every 2 s the crash buffer is read for this app, and a crash kills the whole Maestro tree (maestro.bat, cmd, java), so a launch
# crash ends the flow within seconds instead of after its 30-60 s timeouts. Output goes to files (read back after), the exit code of a killed run is -1.
function Invoke-MaestroWatched([string]$Maestro, [System.IO.FileInfo]$File) {
    $outFile = Join-Path $script:StateDir "maestro-i$Instance.out"
    $errFile = Join-Path $script:StateDir "maestro-i$Instance.err"
    $null = New-Item -ItemType Directory -Force $script:StateDir
    $argLine = "--device $($script:Serial) test --no-reinstall-driver --debug-output `"$($script:MaestroOut)`" `"$($File.FullName)`""
    $p = Start-Process -FilePath $Maestro -ArgumentList $argLine -NoNewWindow -PassThru -RedirectStandardOutput $outFile -RedirectStandardError $errFile
    $null = $p.Handle
    $seen = @{ CrashLog = '' }
    $probe = New-AppCrashProbe $script:Serial $packages[$App] $seen
    $killed = Wait-ProcessWithCrashWatch $p $probe $script:CrashWatchPollSeconds
    $end = Resolve-MaestroEnd $p $killed   # Slice 301: every exit path ends in the same bounded wait-and-rekill; a tree still alive after it is RunnerStuck (common.ps1)
    # The two streams stay apart: the "Maestro failed:" text quotes the last lines of each, labelled, so neither stream's failure line is pushed out by the other's noise.
    $stdout = "$(Get-Content $outFile -Raw -ErrorAction SilentlyContinue)"
    $stderr = "$(Get-Content $errFile -Raw -ErrorAction SilentlyContinue)"
    return [pscustomobject]@{ Stdout = $stdout; Stderr = $stderr; ExitCode = $end.ExitCode; Killed = $killed; RunnerStuck = $end.RunnerStuck; StuckPid = $end.StuckPid; CrashLog = $seen.CrashLog }
}

# Slice 247 item a: the PHASES line of the flow just run (from Maestro's commands.json), printed after the PASS/FAIL line; $null when Maestro wrote none.
function Get-FlowPhasesLine([string]$FlowName, [datetime]$Since, [double]$TotalSeconds) {
    $file = Find-FlowCommandsFile $script:MaestroOut $Since
    if (-not $file) { return $null }
    return (Format-FlowPhases $FlowName (Get-FlowPhaseTimes (Get-Content $file -Raw)) $TotalSeconds)
}

# Slice 247 item c: a flow whose app died with a Mono SIGSEGV within 20 s of its start runs once more (the first run is reported as crash-retried); any other crash, or a second one, fails as before.
function Invoke-FlowCrashRetry([string]$Maestro, [System.IO.FileInfo]$File) {
    $runFlow = New-GuardedFlowRunBlock ${function:Invoke-GuardedFlow} $Maestro $File   # Slice 301 review: a closure cannot see this script's functions, so the function itself is passed in
    $isRetryable = { param($CrashLog) Test-RetryableMonoCrash $CrashLog $packages[$App] $script:MonoRetryMaxUptimeSeconds }
    return Invoke-CrashRetryFlow $File.BaseName (Join-Path $script:StateDir "logcat-i$Instance") $App $runFlow $isRetryable   # logic and tests: common.ps1 / test-speed.ps1
}

# Slice 207: the APK is built (or found in the cache) before the emulator lease is taken, so a cold build never holds the emulator or runs a waiter's wait out.
# Nothing is built when the installed app already matches the current inputs and -Install is not asked for.
# Slice 241 item 3: on a cache miss the emulator boots (detached, in the background) while the APK builds, and both are waited for before the install.
function Build-BeforeLease {
    $key = Get-BuildKey
    $needed = $Install -or ((Get-InstalledKey $App) -ne $key)
    if ($needed -and -not (Find-CachedBuild (Get-NoBuildLookupDir (Get-BuildDir $App (Get-EmulatorBuildPlatform $App) $key)) '*-Signed.apk')) {
        if (Test-NoBuildRequested $NoBuild) { Write-EmulatorNoBuildMiss $App $key; exit $script:EmulatorExitCacheMissed }
        $boot = if (Test-EmulatorPresent) { $null } else { Start-EmulatorBootInBackground $Instance -ColdBoot:$ColdBoot }
        if ($boot) { Write-Host "Cache miss: building the $App APK while $($script:Serial) boots." }
        $built = $false
        try { & (Join-Path $PSScriptRoot 'install-debug.ps1') -App $App -Instance $Instance -BuildOnly; $built = $true }
        finally {
            # A failed build shows its own error: the boot is not waited for and its failure never replaces the build's, but the boot this call started is stopped.
            if ($boot -and $built) { Wait-EmulatorBoot $boot }
            elseif ($boot) { Stop-BackgroundBoot $boot $Instance }
        }
    }
}

function Find-RequestedFlows([string]$FlowDir) {
    if (-not $Flow) { return @(Find-FlowFiles $FlowDir '') }
    $found = @()
    $missing = @()
    foreach ($name in (Split-FlowList $Flow)) {
        $hit = @(Find-FlowFiles $FlowDir $name)
        if ($hit.Count -eq 0) { $missing += $name }
        $found += $hit
    }
    if ($missing.Count -gt 0) { throw "No flow named $($missing -join ', ') in $FlowDir (-Flow takes flow names, e.g. record-page-shows-words, or paths). Nothing was run." }
    return $found
}

# Slice 241: stops a detached emulator boot this run started (the start-warm wrapper first, so it cannot start the emulator after stop.ps1 ran, then the emulator).
function Stop-BackgroundBoot($Boot, [int]$InstanceNumber) {
    if (-not $Boot.HasExited) { & taskkill.exe /T /F /PID $Boot.Id 2>&1 | Out-Null }
    & (Join-Path $PSScriptRoot 'stop.ps1') -Instance $InstanceNumber | Out-Null
}

# Slice 241 item 4: the second half of the flows on emulator 2, as a detached run-flows call (output to a file). Returns the process, or $null when nothing goes there.
function Start-SecondHalf($Names, $Boot) {
    if ($Names.Count -eq 0) { return $null }
    if (-not $Boot) { $null = & (Join-Path $PSScriptRoot 'new-avd.ps1'); $Boot = Start-EmulatorBootInBackground 2 }
    # Never start a flows run while the other emulator is still booting (Maestro: device not connected). The wait is bounded: a boot that hangs or fails sends these
    # flows to emulator 1 (a $null return), and the caller releases emulator 2.
    try { $booted = Wait-EmulatorBoot $Boot $script:SplitBootSeconds } catch { $booted = $false; Write-Host "SPLIT: emulator 2 failed to boot ($($_.Exception.Message))." }
    if (-not $booted) {
        Write-Host "SPLIT: emulator 2 was not ready in $($script:SplitBootSeconds) s; these flows run on this emulator instead: $($Names -join ', ')."
        Stop-BackgroundBoot $Boot 2
        return $null
    }
    $log = Join-Path $script:StateDir 'split-i2.log'
    # This detached call is a whole run-flows run on emulator-5556: it takes its own lease and runs Enable-DialogGuard / Restore-DialogGuard on that serial itself.
    $arguments = "-NoProfile -File `"$PSCommandPath`" -App $App -Instance 2 -Flow `"$($Names -join ',')`" -LogTimeoutSeconds $LogTimeoutSeconds"
    Write-Host "SPLIT: $($Names.Count) flow(s) run on emulator-5556 at the same time: $($Names -join ', ') (output $log)."
    return Start-DetachedProcess (Get-Process -Id $PID).Path $arguments $log "$log.err"
}

function Wait-SecondHalf($Process) {
    $Process.WaitForExit()
    $null = $Process.Handle
    $log = Join-Path $script:StateDir 'split-i2.log'
    Write-Host '--- emulator 2 ---'
    Get-Content $log -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "[i2] $_" }
    Get-Content "$log.err" -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "[i2 err] $_" }
    return [int]$Process.ExitCode
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
        $script:DialogGuardLeaseLost = $true   # Restore-DialogGuard (in the finally) then leaves the setting to the run that holds the lease
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
    # Slice 241 item 4: the flows are known now, so a split's second emulator boots while this one installs.
    $flowDir = Join-Path $script:RepoRoot ".maestro\$App"
    $files = @(Find-RequestedFlows $flowDir)   # Slice 225: -Flow also finds a flow under needs-file/ and accepts a path
    if (-not $files) { throw "No flows found in $flowDir$(if ($Flow) { " named $((Split-FlowList $Flow) -join ', ')" }) (-Flow takes flow names, e.g. record-page-shows-words, or paths)." }
    $splitAsked = ($Split.IsPresent -or $env:RTST_SPLIT -eq '1') -and $Instance -eq 1
    $splitPlan = Split-FlowNames @($files | ForEach-Object { $_.BaseName }) $splitAsked
    $boot2 = $null
    $secondRun = $null
    if ($splitPlan.Second.Count -gt 0) { $null = & (Join-Path $PSScriptRoot 'new-avd.ps1'); $boot2 = Start-EmulatorBootInBackground 2; $priorTwoEmulators = $env:RTST_ALLOW_TWO_EMULATORS; $env:RTST_ALLOW_TWO_EMULATORS = '1' }
    if ($Install) { & (Join-Path $PSScriptRoot 'install-debug.ps1') -App $App -Instance $Instance }
    else { & (Join-Path $PSScriptRoot 'start-warm.ps1') -Instance $Instance -ColdBoot:$ColdBoot | Out-Null }

    # Slice 215: health first. A dead system_server must be reported and restarted here, not surface below as "not installed" (2026-10-31 kill test).
    $null = Confirm-EmulatorHealthy 'before the install check'

    # Without -Install the installed app must be the build for the current inputs; a stale one is replaced (from the cache when it exists).
    if (-not $Install) {
        $currentKey = Get-BuildKey
        if ((Get-InstalledKey $App) -ne $currentKey) {
            Write-Host "The installed $App build is not the one for the current inputs (key $currentKey); installing it first."
            & (Join-Path $PSScriptRoot 'install-debug.ps1') -App $App -Instance $Instance
            Exit-OnNoBuildMiss
        }
    }

    $ErrorActionPreference = 'Continue'
    $installed = & $script:Adb -s $script:Serial shell pm list packages $packages[$App]
    $ErrorActionPreference = 'Stop'
    $installProblem = Resolve-InstallProblem (Join-PmLines $installed) $packages[$App]   # Slice 225 item 4: one line per package, so two matching packages are both seen
    if ($installProblem -and $installProblem -like 'EMULATOR UNHEALTHY*') { Write-Host $installProblem; $null = Confirm-EmulatorHealthy 'at the install check'; $installed = & $script:Adb -s $script:Serial shell pm list packages $packages[$App]; $installProblem = Resolve-InstallProblem (Join-PmLines $installed) $packages[$App] }
    if ($installProblem) { throw "$installProblem; run install-debug.ps1 -App $App (or pass -Install)." }

    $total = [Diagnostics.Stopwatch]::StartNew()
    $stuckPid = 0
    $activity = Start-FlowsActivity "flows-$App-i$Instance" $leaseOwner
    try {
        Assert-LeaseHeld $leaseOwner 'while this run waited for a free slot'  # the wait for the quiet machine can be long
        Clear-AnrDialog 'before the flows'
        Confirm-EmulatorPreflight $maestro   # Slice 269
        Enable-DialogGuard   # Slice 253 item 1: every AVD; put back in the finally below (a leftover 1 from a killed run is cleared)
        $secondRun = Start-SecondHalf @($splitPlan.Second) $boot2
        $myNames = @($splitPlan.First)
        if (@($splitPlan.Second).Count -gt 0 -and -not $secondRun) { $myNames += @($splitPlan.Second) }   # emulator 2 did not come up: its flows run here
        $results = New-Object System.Collections.Generic.List[object]
        $crashedIn = $null
        $stopLine = ''
        foreach ($file in @($files | Where-Object { $myNames -contains $_.BaseName })) {
            if ($crashedIn) {   # Slice 241 item 1: the app is dead (or, Slice 301, the Maestro tree is stuck); running on only waits out each flow's timeouts
                Write-Host ("{0,-5} {1} ({2})" -f 'NOTRUN', $file.BaseName, $stopLine)
                $results.Add([pscustomobject]@{ Name = $file.BaseName; Seconds = 0; Problem = $null; Crash = @(); NotRun = $stopLine })
                continue
            }
            Assert-LeaseHeld $leaseOwner 'during the flows'  # renewed between flows
            $r = Invoke-FlowCrashRetry $maestro $file
            Write-Host ("{0,-5} {1} ({2:n1} s){3}" -f $(if ($r.Problem) { 'FAIL' } else { 'PASS' }), $r.Name, $r.Seconds, $(if ($r.Retried) { ' [crash-retried]' } else { '' }))
            if ($r.Phases) { Write-Host $r.Phases }
            if ($r.Problem) { Write-Host ($r.Problem -replace '(?m)^', '      ') }
            $reason = Get-RunStopReason $r   # Slice 301: a stuck Maestro tree (it may still hold the device) or a crash ends the run; the stuck one is named as that, never as an app crash
            if ($reason) { $crashedIn = $r.Name; $stopLine = Format-RunStopLine $r.Name $reason; Write-Host $stopLine; Write-Host 'The remaining flows on this emulator are not run.' }
            if (Test-RunnerStuck $r) { $stuckPid = $r.StuckPid }
            $results.Add($r)
        }
        if (-not $stuckPid) { Clear-AnrDialog 'after the flows' }
    } finally {
        if ($stuckPid) { Stop-ProcessTree $stuckPid }   # Slice 301: one last kill of the tree that would not exit
        Restore-DialogGuard; Stop-Activity $activity
    }
    $failed = @($results | Where-Object { $_.Problem }).Count
    $notRun = @($results | Where-Object { $_.NotRun }).Count
    Write-Host (Format-FlowsSummary $App $results.ToArray() $total.Elapsed.TotalSeconds)
    foreach ($crashSummary in @(Format-CrashSummary $results.ToArray())) { Write-Host $crashSummary }
    foreach ($retrySummary in @(Format-RetrySummary $results.ToArray())) { Write-Host $retrySummary }
    foreach ($logLine in @(Format-RetryLogLines $results.ToArray())) { Write-Host $logLine }
    foreach ($stuckSummary in @(Format-RunnerStuckSummary $results.ToArray())) { Write-Host $stuckSummary }
    $secondExit = if ($secondRun) { Wait-SecondHalf $secondRun } else { 0 }
    if ($failed -gt 0 -or $notRun -gt 0 -or $secondExit -ne 0) { exit 1 }
} finally {
    if ($leaseTaken) { Remove-Lease $script:Serial $leaseOwner 0 'emulator' | Out-Null }
    if ($boot2) { if ($null -eq $priorTwoEmulators) { Remove-Item Env:RTST_ALLOW_TWO_EMULATORS -ErrorAction SilentlyContinue } else { $env:RTST_ALLOW_TWO_EMULATORS = $priorTwoEmulators }; & (Join-Path $PSScriptRoot 'stop.ps1') -Instance 2 | Out-Null }   # only a split boots emulator 2, so only a split stops it
}
} finally {
    try { Remove-WorktreeRunMark $runMark } catch { Write-Warning "Run mark removal failed: $($_.Exception.Message)" }
    try { if ($null -eq $priorNoBuild) { Remove-Item Env:RTST_NO_BUILD -ErrorAction SilentlyContinue } else { $env:RTST_NO_BUILD = $priorNoBuild } } catch { Write-Warning "Restoring RTST_NO_BUILD failed: $($_.Exception.Message)" }
}
