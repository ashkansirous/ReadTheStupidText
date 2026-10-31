<#
.SYNOPSIS
  Runs named checks on the real phone inside a fixed time budget and prints one summary table (Slice 213).
.DESCRIPTION
  run-checks.ps1 -App rtst|dino -Checks cold-start,highlight [-Budget 10] [-Runs 3] [-Reinstall] [-Serial s]
  run-checks.ps1 -Build -App rtst|dino [-Variant debug|aot]      (default debug) builds (or finds) the APK for the current inputs in the build cache, then exits.
  run-checks.ps1 -Prepare -App rtst|dino [-Variant debug|aot]    (builds the APK first when it is not cached) puts the voice pack on the dev app, OUTSIDE any budget (like -Build), then exits. A pack is installed when files/packs/<id>/active exists
                                                                 and the version folder it names has pack.json (Test-PackInstalled). Steps: take the lease, install the cached APK as a run does, deletes the stale
                                                                 files/packs/<id>-*.zip.partial of the needed pack, restore a verified aside copy if one exists, else launch the dev app in the foreground (skip-first-run) and require a
                                                                 `PACKDL start` line within 60 s (else FAIL quoting the app's last 10 RTST-TEST lines), then wait until Test-PackInstalled is true (progress every 30 s; a .partial
                                                                 not growing for 2 minutes is a FAIL), then save a verified aside copy of the installed pack folders only (never a .partial; newest 3 kept). Exit 0 ready, 1 failed.
  Builds (Slice 213): at the start the runner starts BOTH builds in the background (run-checks.ps1 -Build as child processes; the plain Debug build and the AOT build), each skipped when
  cached for the current build inputs. Each writes <worktrees>.phoneuild-<app>-<variant>.json (state queued/building/ok/failed, start time, heartbeat every 30 s, last log line, log path)
  and a log next to it; a build whose log does not grow for 5 minutes is failed (stalled) and stopped. The two builds of one checkout run at the same time: each has its own `dotnet build --artifacts-path <worktree>\.artifacts\<app>-<variant>` (restore too), so they share no obj folder and touch nothing under src\*\obj or src\*in. Each check declares its build in the catalogue: cold-start needs 'aot' (every timing does) and WAITS for it (what it waits for is printed every 30 s); the others take
  whichever build is ready first, AOT preferred when it is ready. A run swaps on the phone at most once, Debug -> AOT, never back. A failed or stalled build FAILs the checks that need it with the
  build's last 20 log lines; the runner never skips for a missing build and never falls back silently. The budget covers the checks, not the time spent only waiting for a build.
  <worktrees>.phoneun-status.json (each build, each check, installedVariant, updatedUtc) is rewritten at every step, every 30 s while waiting and by the checks' wait loops, so a stuck or failed build is
  visible from outside. Every timing and every table row carries its variant (aot/debug).
  Order: take the phone lease (the same per-phone lease as lease.ps1, owner $env:RTST_DEVICE_OWNER, else $env:RTST_PHONE_OWNER, else run-checks-<pid>; it is
  exported as RTST_PHONE_OWNER for the scripts it starts and released at the end unless the caller already held it; a lease the caller holds is only ever extended,
  never shortened) -> for each check, once its build is ready: install the cached APK when the phone has none or run-checks itself installed another build or variant (adb install -r, data stays; the phone keeps a
  marker of what run-checks installed; an app run-checks did not install is never replaced silently, every check is skipped unless -Reinstall) -> on the first install preflight
  (run with -Fix: stay-awake is the one phone setting the runner changes; only a lost connection, no charger, a secure or unreadable lock screen or no free space stop the run, printed as one `preflight: GO` / `preflight: STOP - <reasons>` line; other FAILs are warnings on that line) -> after every install the setup on the dev install (packs are checked with Test-PackInstalled and restored from the aside copy when an install lost them) -> the quiet machine before the first AOT (timing) check -> the check.
  Setup (dev package only): grant POST_NOTIFICATIONS, the no-usage marker, put the voice packs back from the
  aside copy when they were moved (reset-dev-app.ps1 keeps them there). A timed run NEVER downloads: pack installed -> go; not installed but an aside copy exists -> it is restored and verified with Test-PackInstalled; else the checks that need a pack are SKIPPED `no pack: run -Prepare first`. The first-run skip is part of each check's launch. -Reinstall installs again even over the same build and prints adb's exit code and the lastUpdateTime change; an install that did not happen fails the stage with adb's output.
  The budget (-Budget minutes, default 10) runs from the moment the runner starts: the lease wait and the quiet-machine wait (each capped at 3 minutes, then the
  check is SKIPPED `waited too long for <holder>`), the install and the preflight all count against it, the wait for a build does not. Preflight is GO only on its explicit `PREFLIGHT GO` line (a
  NO-GO whose only FAILs are non-fatal checks is a warning); anything else stops the run. A check starts only when its estimate fits what is left, and is stopped when the time is gone
  (TIMEOUT); every check not run is listed with its reason. Exit: 0 all passed, 1 a check failed or timed out, 2 no phone, 3 some checks skipped, 4 the phone is leased
  by someone else, 5 preflight NO-GO, 6 the build failed, 7 the runner itself broke (install, setup, a dropped link). Every path prints the table, with each check's
  outcome or its SKIP reason; cold-start reports MEASURED (numbers, no pass criterion), never PASS. Wireless adb only. Self-test with no device: test-run-checks.ps1.
  Checks live in tools/phone/checks/<name>.ps1 (a check is added as a file there and a line in the catalogue in checks/_lib.ps1, not as a brief).
#>
param(
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [string[]]$Checks,
    [ValidateRange(1, 120)][int]$Budget = 10,
    [ValidateSet('aot', 'debug')][string]$Variant = 'debug',   # only for -Build and -Prepare; a run takes each check's own variant
    [switch]$Build,
    [switch]$Prepare,
    [int]$Runs = 3,
    [switch]$Reinstall,
    [string]$Serial
)
$script:RunStart = Get-Date   # the budget clock: it runs from here, so every wait before the checks counts
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'checks\_lib.ps1')
. (Join-Path $PSScriptRoot 'checks\_device.ps1')
. (Join-Path $PSScriptRoot '..\emulator\build-cache.ps1')

$package = Get-DevPackage $App
$script:Projects = @{ rtst = 'src\ReadTheStupidText.Mobile\ReadTheStupidText.Mobile.csproj'; dino = 'src\DinoReader.App\DinoReader.App.csproj' }
$script:ApkFilter = '*-Signed.apk'
$script:Rid = 'android-arm64'
$script:Platform = Get-CachePlatform $Variant
$script:WaitedSeconds = 0          # time spent only waiting for a build: not part of the budget
$script:InstalledVariant = $null  # the variant this run has put on the phone (at most one swap: debug -> aot)
$script:PreflightDone = $false
$script:PacksState = 'Present'
$script:RunFinished = $false
$script:BuildJobs = @{}
$script:LastWaitPrint = [datetime]::MinValue
$script:WaitPollSeconds = 5
$script:AsideRoot = '/sdcard/Download/rtst-packs-aside'   # reset-dev-app.ps1 keeps the moved packs here
$script:TransferSeconds = 900
$script:MinStepSeconds = 30   # install and pack restore need at least this much budget, else they are skipped
$script:NoBudgetReason = 'no budget left for install/restore'
$script:LeaseExtraMinutes = 15
$script:PrepareLeaseMinutes = 45   # -Prepare may take a long download: its lease is held that long
$script:PreflightScript = Join-Path $PSScriptRoot 'preflight.ps1'
$script:Notifications = 'android.permission.POST_NOTIFICATIONS'
$script:InstallMarkerPath = "$($script:InstallMarkerPrefix)$package"
$script:LeaseLostReason = 'the phone lease was taken by someone else; stopped'

# ---- builds (a run starts them in the background; -Build runs one in the foreground, and is what the background processes run)
$script:BuildProcessLogSuffix = '.proc.log'

# <worktrees>\.phone: the lease files, the build status files and run-status.json live there, readable from outside.
function Get-PhoneStatusDir {
    $dir = Join-Path (Get-WorktreesRoot) '.phone'
    $null = New-Item -ItemType Directory -Force $dir
    return $dir
}
function Get-BuildStatusPath([string]$V) { return Join-Path (Get-PhoneStatusDir) "build-$App-$V.json" }
function Get-BuildLogPath([string]$V) { return Join-Path (Get-PhoneStatusDir) "build-$App-$V.log" }
function Get-RunStatusPath { return Join-Path (Get-PhoneStatusDir) 'run-status.json' }

function Find-VariantApk([string]$V, [string]$Key) {
    $dir = Get-BuildDir $App (Get-CachePlatform $V) $Key
    return Find-CachedBuild $dir $script:ApkFilter
}

# The last line the build wrote (stdout, else stderr), for the status file.
function Get-LastLogLine([string]$V) {
    $log = Get-BuildLogPath $V
    foreach ($path in @($log, "$log.err")) {
        $line = if (Test-Path $path) { Get-Content $path -Tail 1 -ErrorAction SilentlyContinue } else { $null }
        if ($line) { return "$line".Trim() }
    }
    return $null
}

# The build's status file (this variant's): state queued/building/ok/failed, start time, heartbeat, last log line, log path. Written at each change and every 30 s while dotnet runs.
function Write-BuildStatus([string]$V, [string]$State, [string]$ErrorText = $null) {
    $now = Get-Date
    if (-not $script:BuildStartedAt) { $script:BuildStartedAt = $now }
    if ($State -ceq $script:StateBuilding -and -not $script:BuildingStartedAt) { $script:BuildingStartedAt = $now }
    $started = if ($script:BuildingStartedAt) { $script:BuildingStartedAt } else { $script:BuildStartedAt }
    $status = [ordered]@{ app = $App; variant = $V; state = $State; startedUtc = (ConvertTo-DeadlineText $started)
        heartbeatUtc = (ConvertTo-DeadlineText $now); lastLine = (Get-LastLogLine $V); log = (Get-BuildLogPath $V); error = $ErrorText; pid = $PID }
    [IO.File]::WriteAllText((Get-BuildStatusPath $V), ($status | ConvertTo-Json))
}

# dotnet build as a child whose output goes to the build log; the status file is refreshed every 30 s until it ends. Returns its exit code.
function Invoke-DotnetBuild([string]$V, [string[]]$BuildArguments) {
    $log = Get-BuildLogPath $V
    Remove-Item $log, "$log.err" -Force -ErrorAction SilentlyContinue
    $process = Start-Process dotnet -ArgumentList (@('build') + $BuildArguments) -NoNewWindow -PassThru -RedirectStandardOutput $log -RedirectStandardError "$log.err"
    $null = $process.Handle
    while (-not $process.WaitForExit($script:HeartbeatSeconds * 1000)) { Write-BuildStatus $V $script:StateBuilding }
    return $process.ExitCode
}

# Debug build for the phone's ABI (AOT when asked) into the cache; the same command as audio-timing.ps1's Build-PhoneApk, and the same cache name, so the two share builds.
function Build-VariantApk([string]$V, [string]$Dir, [string]$Key) {
    $project = Join-Path $script:RepoRoot $script:Projects[$App]
    $platform = Get-CachePlatform $V
    $artifacts = Get-PhoneArtifactsPath $script:RepoRoot $App $platform
    Assert-BuildPathLength $artifacts   # Slice 216: fail now, not minutes into the build (APT2264)
    Remove-Item $artifacts -Recurse -Force -ErrorAction SilentlyContinue
    $arguments = ConvertTo-ProcessArguments (Get-PhoneBuildArguments $project $artifacts $package $script:Rid ($V -eq $script:VariantAot))   # the one build command, shared with audio-timing.ps1
    $exitCode = Invoke-DotnetBuild $V $arguments
    if ($exitCode -ne 0) { throw "dotnet build exited $exitCode" }
    $built = Find-PhoneArtifactApk $artifacts $script:ApkFilter
    if (-not $built) { throw 'Signed APK not found.' }
    $temp = New-BuildTempDir $Dir
    try { Copy-Item $built.FullName $temp; Save-BuildMeta $temp $App $platform $Key; Publish-BuildDir $temp $Dir }
    finally { Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue }
    Remove-OldBuilds $App $platform
}

# Under the build locks: the key is read again (the files may have changed while waiting for a lock) and the cache looked at again (another agent may have built it).
# Each variant builds into its own --artifacts-path (no shared obj folders), so the checkout lock is per variant: Debug and AOT of one checkout run at the same time.
function Build-UnderLocks([string]$V, [string]$WaitedKey) {
    $platform = Get-CachePlatform $V
    $locks = Enter-BuildLocks $App $platform $WaitedKey $platform
    try {
        $key = Get-BuildKey
        if (Find-VariantApk $V $key) { Write-Host "Cached meanwhile by another agent (key $key)."; return }
        Write-BuildStatus $V $script:StateBuilding
        Write-Host "Building $App $V (key $key); this takes about $($script:BuildMinutes) minutes..."
        $priority = Set-BuildPriority
        try { Build-VariantApk $V (Get-BuildDir $App $platform $key) $key | Out-Host } finally { Restore-ProcessPriority $priority }
    } finally { Exit-BuildLocks $locks }
}

function Invoke-BuildMode {
    $key = Get-BuildKey
    if (Find-VariantApk $Variant $key) { Write-BuildStatus $Variant $script:StateOk; Write-Host "Already cached: $App $Variant for the current build inputs."; return $script:ExitAllPassed }
    $activity = $null
    try {
        Write-BuildStatus $Variant $script:StateQueued
        $activity = Start-Activity "build-$App-$(Get-CachePlatform $Variant)"
        Build-UnderLocks $Variant $key
    } catch { Write-BuildStatus $Variant $script:StateFailed $_.Exception.Message; Write-Host "BUILD FAILED: $($_.Exception.Message)"; return $script:ExitBuildFailed }
    finally { if ($activity) { Stop-Activity $activity } }
    if (-not (Find-VariantApk $Variant (Get-BuildKey))) { Write-BuildStatus $Variant $script:StateFailed 'the cache has no APK for the current inputs'; Write-Host 'The build finished but the cache has no APK for the current inputs (the files changed during the build?).'; return $script:ExitBuildFailed }
    Write-BuildStatus $Variant $script:StateOk
    Write-Host "Built and cached: $App $Variant."
    return $script:ExitAllPassed
}

# ---- the background builds of a run
# Starts the Debug and the AOT build as child processes (run-checks.ps1 -Build), each skipped when its APK is already cached. Returns one record per variant.
function Start-VariantBuilds($Plans) {
    $key = Get-BuildKey
    $script:RunKey = $key   # read once: the monitor looks at the cache every few seconds
    $engine = (Get-Process -Id $PID).Path
    $builds = @{}
    foreach ($v in @($script:VariantDebug, $script:VariantAot)) {
        $record = [pscustomobject]@{ Variant = $v; Process = $null; Final = $null }
        if (Find-VariantApk $v $key) { $record.Final = [pscustomobject]@{ State = $script:StateOk; Failure = $null } }
        else {
            Remove-Item (Get-BuildStatusPath $v) -Force -ErrorAction SilentlyContinue
            $procLog = (Get-BuildLogPath $v) + $script:BuildProcessLogSuffix
            $arguments = @('-NoProfile', '-File', "`"$PSCommandPath`"", '-Build', '-App', $App, '-Variant', $v)
            $record.Process = Start-Process $engine -ArgumentList $arguments -PassThru -WindowStyle Hidden -RedirectStandardOutput $procLog -RedirectStandardError "$procLog.err"
            Write-Host "building $App $v in the background for this run, about $($script:BuildMinutes) min if not cached by another agent (status $(Get-BuildStatusPath $v), log $(Get-BuildLogPath $v))"
        }
        $builds[$v] = $record
    }
    Write-Host (Format-BuildsStartLine $builds $Plans)
    return $builds
}

function Read-BuildStatusFile([string]$V) {
    $path = Get-BuildStatusPath $V
    if (-not (Test-Path $path)) { return $null }
    try { return (Get-Content $path -Raw | ConvertFrom-Json) } catch { return $null }
}

function Get-BuildLogTail([string]$V) {
    $log = Get-BuildLogPath $V
    $lines = @()
    foreach ($path in @($log, "$log.err", "$log$($script:BuildProcessLogSuffix).err")) { if (Test-Path $path) { $lines += @(Get-Content $path -Tail $script:BuildLogTailLines -ErrorAction SilentlyContinue) } }
    return @($lines | Where-Object { "$_".Trim() -ne '' } | Select-Object -Last $script:BuildLogTailLines)
}

function Get-LogWriteTime([string]$V) {
    $log = Get-BuildLogPath $V
    $times = @(@($log, "$log.err") | Where-Object { Test-Path $_ } | ForEach-Object { (Get-Item $_).LastWriteTime })
    if ($times.Count -eq 0) { return $null }
    return ($times | Sort-Object -Descending | Select-Object -First 1)
}

# A build that is ok or failed stays so; a stalled or failed one is stopped (the runner started it).
function Get-BuildView($Record) {
    if ($Record.Final) { return $Record.Final }
    $v = $Record.Variant
    $status = Read-BuildStatusFile $v   # before the cache: a build publishes its APK, then writes ok, so an ok status always finds the APK
    $cached = [bool](Find-VariantApk $v $script:RunKey)
    $exited = [bool]($Record.Process -and $Record.Process.HasExited)
    $resolved = Resolve-BuildState $status $exited (Get-LogWriteTime $v) (Get-Date) $cached
    $view = [pscustomobject]@{ State = $resolved.State; Failure = $null }
    if ($resolved.State -ceq $script:StateFailed) {
        $view.Failure = Format-BuildFailure $v $resolved.Reason (Get-BuildLogTail $v)
        if ($Record.Process -and -not $Record.Process.HasExited) { & taskkill.exe /T /F /PID $Record.Process.Id | Out-Null }
    }
    if ($resolved.State -ceq $script:StateOk -or $resolved.State -ceq $script:StateFailed) { $Record.Final = $view }
    return $view
}

function Get-BuildViews($Builds) {
    $views = @{}
    foreach ($v in $Builds.Keys) { $views[$v] = Get-BuildView $Builds[$v] }
    return $views
}

# ---- phone: install, preflight, setup
# The phone's view of the dev app: Update (its lastUpdateTime, $null when not installed) and Marker (what run-checks wrote after its own install, if still true).
function Get-InstalledState([string]$Phone) {
    $update = Get-PackageLastUpdate ((Invoke-Adb $Phone @('shell', 'dumpsys', 'package', $package)) -join "`n")
    $markerText = (Invoke-Adb $Phone @('shell', "cat $($script:InstallMarkerPath) 2>/dev/null")) -join ' '
    return [pscustomobject]@{ Update = $update; Marker = (ConvertFrom-InstallMarker $markerText) }
}

# adb install -r (data and packs stay), then the marker that says which variant and build this is.
function Install-CachedApk([string]$Phone, [int]$TimeoutSeconds) {
    $before = (Get-InstalledState $Phone).Update
    $result = "$(Invoke-Adb $Phone @('install', '-r', $script:Apk.FullName) $TimeoutSeconds)"
    $exitCode = $global:LASTEXITCODE
    $update = (Get-InstalledState $Phone).Update
    $problem = Resolve-InstallProof $exitCode $result $before $update
    if ($problem) { throw "install of $package FAILED: $problem" }
    Write-Host "   installed $package from the cache: adb exit $exitCode, $($result.Trim()), lastUpdateTime $before -> $update"
    $marker = ConvertTo-InstallMarker $script:Platform $script:CachedKey $update
    Invoke-Adb $Phone @('shell', "echo '$marker' > $($script:InstallMarkerPath)") | Out-Null
}

function Invoke-Preflight([string]$Phone) {
    $engine = (Get-Process -Id $PID).Path
    $ErrorActionPreference = 'Continue'   # preflight's own exit code and lines are the answer; a stderr line must not end this script
    # -Fix: stay-awake is the one phone setting the runner may change (preflight.ps1 sets nothing else).
    $lines = @(& $engine -NoProfile -File $script:PreflightScript -Serial $Phone -App $App -Fix 2>&1 | ForEach-Object { "$_" })
    $exitCode = $LASTEXITCODE
    $lines | Where-Object { "$_".Trim() -notlike 'PREFLIGHT *' } | ForEach-Object { Write-Host $_ }
    $verdict = Resolve-PreflightVerdict $lines $exitCode
    Write-Host (Format-PreflightLine $verdict)
    return $verdict
}

function Get-StageSecondsLeft { return $Budget * 60 - (Get-BudgetElapsedSeconds) }

# 'Present', 'Restored', 'Missing' or 'NoBudget' (not enough budget left to put them back). Present = Test-PackInstalled says a needed pack is installed (active marker plus pack.json
# in the version folder it names). Otherwise the newest aside copy (reset-dev-app.ps1 or -Prepare wrote it) is put back and verified the same way. The timed run NEVER downloads.
function Restore-PacksIfMoved([string]$Phone) {
    $needed = $script:RequiredPacks[$App]
    if (@(Get-InstalledPackIds $Phone $package $needed).Count -gt 0) { return 'Present' }
    $archive = Find-NewestAside $Phone
    if (-not $archive) { return 'Missing' }
    $timeout = Get-StepTimeout $script:TransferSeconds (Get-StageSecondsLeft) $script:MinStepSeconds
    if (-not $timeout) { return 'NoBudget' }
    Write-Host "   no neural pack is installed: restoring them from $archive (at most $timeout s)"
    try { Restore-PacksAside $Phone $package $archive $timeout | Out-Null } catch { Write-Host "   WARNING: $($_.Exception.Message)"; return 'Missing' }
    if (@(Get-InstalledPackIds $Phone $package $needed).Count -gt 0) { return 'Restored' }
    Write-Host "   the aside copy $archive does not hold a pack this app needs ($($needed -join ', '))"
    return 'Missing'
}

# The newest packs-*.tar of the dev package in the aside folder, or $null.
function Find-NewestAside([string]$Phone) {
    $listing = @(Invoke-Adb $Phone @('shell', "ls $($script:AsideRoot)/$package/packs-*.tar"))
    return ($listing | ForEach-Object { "$_".Trim() } | Where-Object { $_ -match '\.tar$' } | Sort-Object -Descending | Select-Object -First 1)
}

# pm grant's answer, said as it is: 'granted' or why not.
function Grant-DevNotifications([string]$Phone) {
    $answer = (Invoke-Adb $Phone @('shell', 'pm', 'grant', $package, $script:Notifications)) -join ' '
    if ($global:LASTEXITCODE -eq 0) { return 'granted' }
    return "NOT granted (pm grant said: $answer)"
}

function Invoke-DevSetup([string]$Phone) {
    $grant = Grant-DevNotifications $Phone
    Set-DevNoUsageMarker $Phone $package
    $packs = Restore-PacksIfMoved $Phone
    Write-Host "   setup: notifications $grant, no-usage marker written, voice packs: $packs"
    return $packs
}

# ---- -Prepare: the voice pack onto the dev app, outside any budget
# The copy for the next run; a copy that cannot be made is a warning, the pack itself is in place.
function Save-PrepareAside([string]$Phone) {
    try {
        $aside = Save-PacksAside $Phone $package $script:AsideRoot $script:TransferSeconds
        Write-Host "   aside copy saved and verified: $($aside.Path) ($($aside.PackIds -join ', '), $($aside.Files) files)"
    } catch { Write-Host "   WARNING: the pack is installed but no aside copy was saved ($($_.Exception.Message)); a reset will need -Prepare again" }
}

# 'Present' (already installed), 'Restored' (from the aside copy) or 'Missing'.
function Resolve-PreparedPack([string]$Phone) {
    $needed = $script:RequiredPacks[$App]
    if (@(Get-InstalledPackIds $Phone $package $needed).Count -gt 0) { return 'Present' }
    $archive = Find-NewestAside $Phone
    if (-not $archive) { return 'Missing' }
    Write-Host "   restoring the packs from $archive"
    try { Restore-PacksAside $Phone $package $archive $script:TransferSeconds | Out-Null } catch { Write-Host "   WARNING: $($_.Exception.Message)"; return 'Missing' }
    if (@(Get-InstalledPackIds $Phone $package $needed).Count -gt 0) { return 'Restored' }
    return 'Missing'
}

function Invoke-PrepareSteps([string]$Phone) {
    $needed = $script:RequiredPacks[$App]
    Write-Host "   notifications $(Grant-DevNotifications $Phone); the no-usage marker is written"
    Set-DevNoUsageMarker $Phone $package
    $state = Resolve-PreparedPack $Phone
    if ($state -eq 'Missing') {
        Write-Host "   no pack installed and no usable aside copy: downloading $($needed -join ' or ') with the app in the foreground (no budget, a stall of $($script:PackStallSeconds) s is a FAIL)"
        $download = Wait-PackInstalled $Phone $package $needed
        if ($download.State -ne 'Present') { Write-Host "PREPARE FAIL: $($download.Detail)"; return $script:ExitCheckFailed }
        $state = 'Downloaded'
    }
    if ($state -ne 'Restored' -and ($state -eq 'Downloaded' -or -not (Find-NewestAside $Phone))) { Save-PrepareAside $Phone }
    Write-Host "PREPARE OK: voice pack $state ($($needed -join ' or '))"
    return $script:ExitAllPassed
}

function Invoke-PrepareGuarded([string]$Phone, $State) {
    Initialize-StageLease $State $Phone
    $heldBy = Take-PhoneLease $State
    if ($heldBy) { Write-Host "PREPARE STOPPED: waited too long for $heldBy (the phone lease)"; return $script:ExitLeaseHeldByOther }
    $env:RTST_PHONE_OWNER = $State.Owner
    $State.Awake = Start-KeepAwake $Phone
    $notInstalled = Resolve-DeviceInstall $Phone $State
    if ($notInstalled) { Write-Host "PREPARE STOPPED: $notInstalled"; return (Get-InstallStopCode $notInstalled) }
    $verdict = Invoke-Preflight $Phone
    if (-not $verdict.Go) { Write-Host "PREPARE STOPPED: preflight NO-GO: $($verdict.Fatal -join '; ')"; return $script:ExitPreflightNoGo }
    return (Invoke-PrepareSteps $Phone)
}

# Restarts the budget clock: run start and waited seconds together.
function Reset-BudgetClock {
    $clock = New-BudgetClock (Get-Date)
    $script:RunStart = $clock.RunStart
    $script:WaitedSeconds = $clock.WaitedSeconds
}

function Invoke-PrepareMode {
    $script:Budget = $script:PrepareLeaseMinutes   # only the lease and the install step read it; there is no check budget in this mode
    $script:CachedKey = Get-BuildKey
    $script:Apk = Find-VariantApk $Variant $script:CachedKey
    if (-not $script:Apk) {
        Write-Host "building $App $Variant first, about $($script:BuildMinutes) min (outside any budget)"
        $built = Invoke-BuildMode
        if ($built -ne $script:ExitAllPassed) { Write-Host 'PREPARE STOPPED: the build failed'; return $built }
        $script:Apk = Find-VariantApk $Variant $script:CachedKey
    }
    Reset-BudgetClock   # the build above is outside any budget: the lease wait and the install start their clock now (run start and waited seconds together)
    $phone = Find-WirelessPhone $Serial
    if (-not $phone) { Write-PairingAsk; return $script:ExitNoPhone }
    $state = New-StageState
    try { return (Invoke-PrepareGuarded $phone $state) }
    catch { Write-Host "PREPARE FAIL: runner error: $($_.Exception.Message)"; return $script:ExitRunnerError }
    finally { Close-DeviceStage $phone $state }
}

# ---- the run
function New-Row([string]$Name, [string]$Status, [string]$Detail, $Seconds = $null, $RowVariant = $null) {
    $row = [pscustomobject]@{ Name = $Name; Status = $Status; Detail = $Detail; Seconds = $Seconds; Variant = $RowVariant }
    return $row
}

# The budget covers the checks, not the time spent only waiting for a build: that time is added to the end of the budget.
function Get-BudgetElapsedSeconds { return (Get-BudgetElapsedSecondsAt $script:RunStart $script:WaitedSeconds (Get-Date)) }
function Get-BudgetEnd { return (Get-BudgetStart $script:RunStart $script:WaitedSeconds).AddMinutes($Budget) }

# Runs one check script in this process; its deadline is the budget's end. A throw is a FAIL (or TIMEOUT when the budget ended it).
function Invoke-OneCheck($Row, [string]$Phone, [datetime]$BudgetEnd, [string]$RunVariant) {
    $splat = @{ Phone = $Phone; App = $App }
    if ($Row.Name -eq 'cold-start') { $splat.Runs = $Runs }
    Set-CheckDeadline $BudgetEnd   # the stage keeps it set (children read it from the environment) until the cleanup is done
    $started = Get-Date; $output = $null; $failure = $null
    try { $output = @(& (Join-Path $PSScriptRoot "checks\$($Row.Name).ps1") @splat) } catch { $failure = $_.Exception.Message }
    $outcome = Resolve-CheckOutcome $output $failure (Test-PastDeadline $BudgetEnd (Get-Date))
    return (New-Row $Row.Name $outcome.Status $outcome.Detail ([int]((Get-Date) - $started).TotalSeconds) $RunVariant)
}

function Get-RowForCheck($Plan, [string]$Phone, [string]$PacksState, [string]$RunVariant) {
    if ($Plan.NeedsPack -and $PacksState -eq 'NoBudget') { return (New-Row $Plan.Name $script:StatusSkipped $script:NoBudgetReason) }
    if ($Plan.NeedsPack -and $PacksState -eq 'Missing') { return (New-Row $Plan.Name $script:StatusSkipped $script:NoPackReason) }
    $decision = Get-BudgetDecision $Plan.EstimateSeconds (Get-BudgetElapsedSeconds) $Budget
    if (-not $decision.Run) { return (New-Row $Plan.Name $script:StatusSkipped $decision.Reason) }
    Write-Host "== $($Plan.Name) on the $RunVariant build (~$($Plan.EstimateSeconds) s, $($decision.LeftSeconds) s of the budget left)"
    return (Invoke-OneCheck $Plan $Phone (Get-BudgetEnd) $RunVariant)
}

# Minutes to write when the lease is renewed: the budget and its margin, never less than the caller's own lease still has.
function Get-OwnLeaseMinutes($State) {
    $held = Read-Lease $State.Key
    $expires = 0
    if ($held -and $held.Owner -ceq $State.Owner) { $expires = $held.Expires }
    return (Get-LeaseMinutes $expires (Get-UnixNow) ($Budget + $script:LeaseExtraMinutes))
}

# Minutes the caller's own lease still has (0 when none or not theirs); read before the lease is written, so the note can say a longer own lease was kept.
function Get-OwnRemainingMinutes($State) {
    $held = Read-Lease $State.Key
    if (-not $held -or $held.Owner -cne $State.Owner) { return 0 }
    return [int][math]::Ceiling(($held.Expires - (Get-UnixNow)) / 60.0)
}

function Confirm-OwnLease($State) {
    $minutes = Get-OwnLeaseMinutes $State
    return (Confirm-DeviceLease $State.Key $State.Owner $minutes 'phone')
}

# ---- run-status.json: what the coordinator reads from outside (each build, each check, the last update). Rewritten at every step, every 30 s while waiting, and by the checks' wait loops.
function Publish-RunStatus($Plans, $State, [string]$Running, [string]$Waiting) {
    $views = Get-BuildViews $script:BuildJobs
    $status = New-RunStatus $App (Get-Date) $views $Plans @($State.Rows) $Running $Waiting $script:InstalledVariant $script:RunFinished
    [IO.File]::WriteAllText((Get-RunStatusPath), ($status | ConvertTo-Json -Depth 6))
    [Environment]::SetEnvironmentVariable($script:RunStatusEnv, (Get-RunStatusPath))
}

# The dev app on the phone as the next check needs it. $null when ready (the first install also runs preflight; every install runs setup and the pack check), else a stop: Reason and Code.
function Switch-ToVariant([string]$V, [string]$Phone, $State) {
    if ($script:InstalledVariant -ceq $V) { return $null }
    Use-Variant $V
    $notInstalled = Resolve-DeviceInstall $Phone $State
    if ($notInstalled) { return [pscustomobject]@{ Reason = $notInstalled; Code = (Get-InstallStopCode $notInstalled) } }
    $script:InstalledVariant = $V
    if (-not $script:PreflightDone) {
        $verdict = Invoke-Preflight $Phone
        if (-not $verdict.Go) { return [pscustomobject]@{ Reason = "preflight NO-GO: $($verdict.Fatal -join '; ')"; Code = $script:ExitPreflightNoGo } }
        $script:PreflightDone = $true
    }
    $script:PacksState = Invoke-DevSetup $Phone
    return $null
}

# Timing checks (the AOT ones) run on a quiet machine; the first one takes it and the cleanup gives it back. $null when quiet, else why not.
function Enter-QuietForTiming($Plan, $State) {
    if ($Plan.Variant -cne $script:VariantAot -or $State.Quiet) { return $null }
    Write-Host 'Waiting for a quiet machine (no builds, emulator flows or VM runs) before the timing checks...'
    return (Enter-QuietWithinBudget $State)
}

# One planned check on the chosen build. Returns $null, or a stop (Reason, Code) that ends the stage.
function Invoke-PlannedCheck($Step, [string]$Phone, $State) {
    $plan = $Step.Plan
    if (-not (Confirm-OwnLease $State)) { [void]$State.Rows.Add((New-Row $plan.Name $script:StatusSkipped $script:LeaseLostReason)); return $null }
    $stop = Switch-ToVariant $Step.Variant $Phone $State
    if ($stop) { return $stop }
    $busy = Enter-QuietForTiming $plan $State
    if ($busy) { [void]$State.Rows.Add((New-Row $plan.Name $script:StatusSkipped "waited too long for $busy")); return $null }
    Set-CheckDeadline (Get-BudgetEnd)
    [void]$State.Rows.Add((Get-RowForCheck $plan $Phone $script:PacksState $script:InstalledVariant))
    return $null
}

# One poll while nothing can run yet: says what it waits for every 30 s, keeps the lease, and the time counts as build wait, not budget.
function Wait-ForBuildsOnce($Step, $State) {
    $now = Get-Date
    if (($now - $script:LastWaitPrint).TotalSeconds -ge $script:HeartbeatSeconds) {
        Write-Host "   $($Step.Detail)"
        $script:LastWaitPrint = $now
        if (-not (Confirm-OwnLease $State)) { throw $script:LeaseLostReason }
    }
    Start-Sleep -Seconds $script:WaitPollSeconds
    $script:WaitedSeconds += ((Get-Date) - $now).TotalSeconds
    Set-CheckDeadline (Get-BudgetEnd)   # the waited time is not budget: install, preflight and the quiet wait must not run on the old deadline
}

# Each row is kept in the state as soon as it exists, so a later failure still prints it. Returns $null, or a stop (Reason, Code).
function Invoke-Checks($Plans, [string]$Phone, $State) {
    foreach ($plan in $Plans) { if ($plan.Status -eq $script:StatusSkipped) { [void]$State.Rows.Add((New-Row $plan.Name $script:StatusSkipped $plan.Reason)) } }
    $pending = New-Object System.Collections.ArrayList
    foreach ($plan in $Plans) { if ($plan.Status -ne $script:StatusSkipped) { [void]$pending.Add($plan) } }
    while ($pending.Count -gt 0) {
        $step = Get-NextStep @($pending) (Get-BuildViews $script:BuildJobs) $script:InstalledVariant
        Publish-RunStatus $Plans $State $(if ($step.Action -ceq $script:ActionRun) { $step.Plan.Name }) $(if ($step.Action -ceq $script:ActionWait) { $step.Detail })
        if ($step.Action -ceq $script:ActionWait) { Wait-ForBuildsOnce $step $State; continue }
        [void]$pending.Remove($step.Plan)
        if ($step.Action -ceq $script:ActionFail) { Write-Host "   $($step.Plan.Name): $($step.Detail)"; [void]$State.Rows.Add((New-Row $step.Plan.Name $script:StatusFail $step.Detail $null $step.Variant)); continue }
        $stop = Invoke-PlannedCheck $step $Phone $State
        if ($stop) { return $stop }
    }
    return $null
}

function Write-Summary($Rows) {
    Write-Host ''
    Format-CheckSummary $Rows | ForEach-Object { Write-Host $_ }
    $skipped = @($Rows | Where-Object { $_.Status -eq $script:StatusSkipped })
    if ($skipped.Count -gt 0) { Write-Host "Skipped (not verified): $(($skipped | ForEach-Object { $_.Name }) -join ', ')" }
}

# Every path that ends the stage early prints the table too: each check that did not run is SKIP with the reason.
function Stop-Stage($Plans, $State, [string]$Reason, [int]$ExitCode) {
    Write-Host "STOPPED: $Reason"
    Write-Summary @(Get-FinalRows $Plans $State.Rows $Reason)
    return $ExitCode
}

function Invoke-Cleanup([string]$What, [scriptblock]$Step) {
    try { & $Step } catch { Write-Host "   cleanup: could not $What : $($_.Exception.Message)" }
}

# Each step on its own, the lease last: one failure (a dropped phone) must not leave the quiet lock or the lease held.
function Close-DeviceStage([string]$Phone, $State) {
    Invoke-Cleanup 'stop the keep-awake job' { Stop-KeepAwake $State.Awake }
    Invoke-Cleanup 'force-stop the dev app' { if ($State.HoldsLease -and (Test-OwnsLease (Read-Lease $State.Key) $State.Owner)) { Invoke-Adb $Phone @('shell', 'am', 'force-stop', $package) | Out-Null } }
    Invoke-Cleanup 'release the quiet machine' { if ($State.Quiet) { Exit-QuietMachine $State.Quiet } }
    $env:RTST_PHONE_OWNER = $State.PreviousOwner
    Invoke-Cleanup 'release the phone lease' { if ($State.HoldsLease -and -not $State.Mine) { Remove-Lease $State.Key $State.Owner | Out-Null } }
}

# What the stage holds, so the cleanup can release it whatever happened; no adb in here (the lease key is read in Initialize-StageLease, inside the guarded part).
function New-StageState {
    return [pscustomobject]@{
        Key = $null; Owner = (Get-DeviceLeaseOwner 'run-checks'); Mine = $false; HoldsLease = $false; Quiet = $null; Awake = $null
        PreviousOwner = $env:RTST_PHONE_OWNER; Rows = (New-Object System.Collections.ArrayList)
    }
}

function Initialize-StageLease($State, [string]$Phone) {
    $State.Key = Get-PhoneLeaseKey $Phone
    $held = Read-Lease $State.Key
    $State.Mine = [bool]($held -and $held.Owner -ceq $State.Owner)
}

# $null once the lease is taken, else the holder; the wait is capped and counts against the budget.
function Take-PhoneLease($State) {
    $wait = Get-BudgetPreWaitMinutes $script:RunStart $script:WaitedSeconds (Get-Date) $Budget
    $minutes = Get-OwnLeaseMinutes $State
    $ownRemaining = Get-OwnRemainingMinutes $State   # before the write: afterwards it would be the new length
    $heldBy = Wait-ForLease $State.Key $State.Owner $minutes $wait 'phone'
    if (-not $heldBy) { $State.HoldsLease = $true; Write-Host (Format-LeaseLengthNote $(if ($Prepare) { '-Prepare' } else { 'a timed run' }) $minutes ($Budget + $script:LeaseExtraMinutes) $ownRemaining) }
    return $heldBy
}

# $null once the machine is quiet, else why not (the wait is capped and counts against the budget).
function Enter-QuietWithinBudget($State) {
    $wait = [int][math]::Ceiling((Get-BudgetPreWaitMinutes $script:RunStart $script:WaitedSeconds (Get-Date) $Budget))
    try { $State.Quiet = Enter-QuietMachine $wait }
    catch { return "other jobs on this machine (builds, emulator or VM runs): $($_.Exception.Message)" }
    return $null
}

# Sets the variant a step works on: the cache name, the key, the APK.
function Use-Variant([string]$V) {
    $script:Variant = $V
    $script:Platform = Get-CachePlatform $V
    $script:CachedKey = $script:RunKey
    $script:Apk = Find-VariantApk $V $script:CachedKey
    if (-not $script:Apk) { throw "the $V build is not in the cache any more (the files changed during the run?)" }
}

# The dev app on the phone: $null when it is right (kept or installed now), else the reason the checks must be skipped.
function Resolve-DeviceInstall([string]$Phone, $State) {
    $installed = Get-InstalledState $Phone
    $plan = Resolve-InstallPlan $installed.Marker $installed.Update $script:Platform $script:CachedKey $Reinstall.IsPresent
    Write-Host "   $package ($($script:Variant)): $($plan.Reason)"
    if ($plan.Action -eq $script:ActionSkip) { return $plan.Reason }
    if ($plan.Action -eq $script:ActionKeep) { return $null }
    if (-not (Confirm-OwnLease $State)) { return $script:LeaseLostReason }
    $timeout = Get-StepTimeout $script:AdbTransferTimeoutSeconds (Get-StageSecondsLeft) $script:MinStepSeconds
    if (-not $timeout) { return $script:NoBudgetReason }
    Install-CachedApk $Phone $timeout
    return $null
}

function Get-InstallStopCode([string]$Reason) {
    if ($Reason -eq $script:LeaseLostReason) { return $script:ExitLeaseHeldByOther }
    return $script:ExitSomeSkipped
}

function Invoke-GuardedStage($Plans, [string]$Phone, $State) {
    Initialize-StageLease $State $Phone
    $heldBy = Take-PhoneLease $State
    if ($heldBy) { return (Stop-Stage $Plans $State "waited too long for $heldBy (the phone lease)" $script:ExitLeaseHeldByOther) }
    $env:RTST_PHONE_OWNER = $State.Owner
    if (-not (Confirm-OwnLease $State)) { return (Stop-Stage $Plans $State $script:LeaseLostReason $script:ExitLeaseHeldByOther) }
    $State.Awake = Start-KeepAwake $Phone
    $stop = Invoke-Checks $Plans $Phone $State
    if ($stop) { return (Stop-Stage $Plans $State $stop.Reason $stop.Code) }
    Write-Summary @(Get-FinalRows $Plans $State.Rows 'not run')
    return (Get-RunExitCode @($State.Rows))
}

# The builds still running when the checks are done keep going (they fill the cache for the next run); say so.
function Write-BuildsLeftRunning {
    foreach ($record in $script:BuildJobs.Values) {
        if ($record.Process -and -not $record.Process.HasExited) { Write-Host "left running: the $($record.Variant) build (status $(Get-BuildStatusPath $record.Variant), log $(Get-BuildLogPath $record.Variant))" }
    }
}

# The whole device stage. An error anywhere (install, setup, a dropped adb link) still ends in the table, with exit 7, and the cleanup always runs.
function Invoke-DeviceStage($Plans, [string]$Phone) {
    $state = New-StageState
    Set-CheckDeadline (Get-BudgetEnd)   # install, restore, preflight and tap/preflight children all stop at the end of the budget
    try { return (Invoke-GuardedStage $Plans $Phone $state) }
    catch { return (Stop-Stage $Plans $state "runner error: $($_.Exception.Message)" (Get-StageErrorExitCode $_.Exception.Message $script:LeaseLostReason)) }
    finally {
        $script:RunFinished = $true
        Invoke-Cleanup 'write the final run status' { Publish-RunStatus $Plans $state $null $null }
        Close-DeviceStage $Phone $state; Set-CheckDeadline $null; Write-BuildsLeftRunning
    }
}

if ($Build) { exit (Invoke-BuildMode) }
if ($Prepare) { exit (Invoke-PrepareMode) }
if (-not $Checks) { throw 'Name the checks to run, e.g. -Checks cold-start,highlight. Known: ' + ($script:Catalogue.Keys -join ', ') }
$plans = @(Resolve-CheckSelection ($Checks | ForEach-Object { $_ -split ',' } | Where-Object { $_ }) $App $script:Catalogue $Runs)
if (@($plans | Where-Object { $_.Status -eq $script:StatusPending }).Count -eq 0) {
    Write-Summary @(Get-FinalRows $plans @() 'not run')
    exit (Get-RunExitCode @(Get-FinalRows $plans @() 'not run'))
}
$phone = Find-WirelessPhone $Serial
if (-not $phone) { Write-PairingAsk; exit $script:ExitNoPhone }
$script:BuildJobs = Start-VariantBuilds $plans
exit (Invoke-DeviceStage $plans $phone)
