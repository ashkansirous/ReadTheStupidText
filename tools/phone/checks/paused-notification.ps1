# check: Apps=rtst EstimateSeconds=240 NeedsPack=true Variant=debug
<#
.SYNOPSIS
  paused-notification: when Android stops the download service on its time limit, the app posts "Download paused. Tap to finish." (Slice 208).
.DESCRIPTION
  Needs an Android 15 phone (the dataSync foreground service time limit). Same set-up as notification-text: a verified aside copy of the installed packs is made, the pack folder is MOVED (never
  deleted) so the app has to download it, the Debug throttle marker makes that download slow. Then: 1. the dataSync timeout is lowered to 30 s (`device_config put activity_manager
  data_sync_fgs_timeout_duration 30000`) and the time limit switched on for the dev package (`am compat enable FGS_INTRODUCE_TIME_LIMITS`); 2. the dev app is launched and `PACKDL start` must be
  logged; 3. the app is sent to the background (HOME); 4. once `PACKDL service timeout` is logged (the time limit stopped the service) the dev app's network is blocked (`cmd netpolicy add restrict-background-blacklist <uid>`, never wifi/data: the adb link is wireless) so the
  run ends unfinished; 5. `PACKDL service paused-notified` must then be logged (the notice is posted where the run ends, never at the timeout itself); 6. the
  notice must be in `dumpsys notification --noredact`: title "Download paused. Tap to finish.", text naming the language and the size "(<n> MB)" (verbatim in the result);
  7. the network is switched back on, and tap-to-finish: the notice's content intent is the app's launcher intent, so it is fired with `am start` of the launcher activity (not a physical tap on the shade: SystemUI is not driven here);
  the app must then be the resumed activity, the download must log `PACKDL start` again and the notice must be gone. In a finally block, ALWAYS: the app's network block is lifted, the timeout is reset (`device_config delete`, `am compat reset`), the download cancelled (cancel-pack-downloads hook), the
  .partial and the half-made pack folder deleted, the original pack moved back and verified with Test-PackInstalled; if that fails the check FAILS (the aside copy is named in the detail).
  -NetworkOnly (Slice 233, any Android version): steps 1 and 4 change: no time limit is set or waited for; the app goes to the background and after $script:BackgroundSettleSeconds s the Debug hook `drop-pack-network` makes the running download's next read throw a dropped-connection HttpRequestException (the per-app netpolicy block cannot cut a running dataSync service: device finding, S21, 2026-11-01), so the real resumable-failure path runs; the marker must carry `reason=<offline|failed|transport>` and the notice must still appear.
  Both variants first run reset-dev-app -Level clear to drop a `VOICEPACK held-user-cancelled` hold an earlier cleanup left (packs kept aside and put back; app data otherwise cleared).
  Wipes: the dev app's app data (reset-dev-app -Level clear, installed packs kept aside and restored), then only the pack folder under test (moved aside and restored).
#>
param(
    [Parameter(Mandatory)][string]$Phone,
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [int]$PausedTimeoutSeconds = 150,
    [switch]$NetworkOnly,   # Slice 233: no dataSync time limit; the backgrounded app just loses its network (see paused-notification-network.ps1)
    [switch]$Stall   # Slice 241 item 7: not a paused notice at all: one body read hangs (hook stall-pack-network) and the stall guard must log it (see pack-stall.ps1)
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\common.ps1')
. (Join-Path $PSScriptRoot '_lib.ps1')
. (Join-Path $PSScriptRoot '_device.ps1')

$package = Get-DevPackage $App
$script:AsideRoot = '/sdcard/Download/rtst-packs-aside'
$script:AsideTransferSeconds = 300
$script:MovedSuffix = '.slice213-moved'
$script:DataSyncTimeoutMs = 30000
$script:PausedTitle = 'Download paused. Tap to finish.'
$script:TimeoutMarkerPattern = 'PACKDL service timeout'
$script:TimeoutSeconds = 90
$script:ResumeSeconds = 60
$script:BackgroundSettleSeconds = 5
$script:PausedMarkerPattern = 'PACKDL service paused-notified pack=(\S+)( reason=(offline|failed|transport))?'
$script:DownloadLanguageName = 'English'   # launched with lang=en (Start-DevAppWithHook)
$script:Launchable = $null
$packId = @($script:RequiredPacks[$App])[0]
$packPath = "$($script:PacksDirectory)/$packId"
$movedPath = "$packPath$($script:MovedSuffix)"

function Invoke-RunAs([string]$Command) {
    Assert-DevPackage $package
    $out = @(Invoke-Adb $Phone @('shell', "run-as $package sh -c '$Command'"))
    return [pscustomobject]@{ Exit = $global:LASTEXITCODE; Out = ($out -join ' | ') }
}

function Test-PhonePathExists([string]$Path) { return ((Invoke-RunAs "test -e $Path").Exit -eq 0) }

function Get-PhoneEpochMs {
    $seconds = "$(Invoke-Adb $Phone @('shell', 'date', '+%s'))".Trim()
    if ($seconds -notmatch '^\d+$') { throw "Could not read the phone's clock (date said '$seconds')." }
    return ([int64]$seconds * 1000)
}

# The time limit exists from Android 15 (API 35); on an older phone there is nothing to test.
function Assert-TimeLimitPhone {
    $sdk = "$(Invoke-Adb $Phone @('shell', 'getprop', 'ro.build.version.sdk'))".Trim()
    if ($sdk -notmatch '^\d+$' -or [int]$sdk -lt 35) { throw "the dataSync time limit needs Android 15 (API 35); this phone reports API '$sdk'." }
}

# The app uid and the device-side heal, before anything is changed; both variants start here.
function Start-AppHeal {
    $script:AppUid = Get-PackageUid $Phone $package
    Start-DeviceRestore $Phone $package $script:AppUid $script:DeviceRestoreSeconds
    $script:HealStarted = $true
}

# Slice 236 item 7: the package's compat state at the start, as the phone says it (best effort, never throws; the exact dumpsys wording is not confirmed on a device). A leftover override of an earlier variant is the
# suspect for the S21's "Compatibility: Killing package ... PlatformCompat overrides" 0.7 s after a launch (2026-11-01, not proven), so the log shows what was there before this check set anything.
function Write-CompatState([string]$When) {
    try {
        $dump = @(Invoke-Adb $Phone @('shell', 'dumpsys platform_compat'))
        $lines = @($dump | Where-Object { "$_" -match [regex]::Escape($package) -or "$_" -match 'FGS_INTRODUCE_TIME_LIMITS' } | Select-Object -First 6 | ForEach-Object { "$_".Trim() })
        Write-Host "   compat state $When : $(if ($lines.Count -gt 0) { $lines -join ' | ' } else { 'no line names this package or FGS_INTRODUCE_TIME_LIMITS' })"
    } catch { Write-Host "   compat state $When : could not be read ($($_.Exception.Message))" }
}

# What this run raised (set BEFORE the command is sent, so a half-sent command still counts). The reset also acts on what it finds on the phone, so an override an earlier killed run left is cleared too.
$script:TimeoutSet = $false
$script:CompatSet = $false

function Set-ShortTimeLimit {
    Start-AppHeal
    Write-CompatState 'before this check changes anything'
    $script:TimeoutSet = $true
    Invoke-Adb $Phone @('shell', 'device_config', 'put', 'activity_manager', 'data_sync_fgs_timeout_duration', "$($script:DataSyncTimeoutMs)") | Out-Null
    $script:CompatSet = $true
    Invoke-Adb $Phone @('shell', 'am', 'compat', 'enable', 'FGS_INTRODUCE_TIME_LIMITS', $package) | Out-Null
}

# Never throws: runs in the finally block. $true when everything this check set was reset (both resets reached the phone). Resets only what is present: the timeout when device_config holds a value (or this run set it), the compat flag when the dump shows an override for the package (or this run enabled it). Nothing present = no `am compat reset`, which would kill the app (Slice 236 item 7). An override left by a killed earlier run is present, so it is cleared. Only the time-limit variant calls it.
function Reset-TimeLimit {
    try {
        # Slice 236 review: a failed diagnostic read never skips the delete/reset. An unreadable state counts as present (both calls are safe to repeat).
        $timeoutValue = ''; $overrides = @(); $timeoutUnknown = $false; $compatUnknown = $false
        try { $timeoutValue = "$(Invoke-Adb $Phone @('shell', 'device_config', 'get', 'activity_manager', 'data_sync_fgs_timeout_duration'))".Trim() }
        catch { $timeoutUnknown = $true; Write-Host "   WARNING: could not read the dataSync timeout ($($_.Exception.Message)); deleting it anyway" }
        try { $overrides = @(Get-CompatOverrideLines @(Invoke-Adb $Phone @('shell', 'dumpsys platform_compat')) $package) }
        catch { $compatUnknown = $true; Write-Host "   WARNING: could not read the compat state ($($_.Exception.Message)); resetting it anyway" }
        $timeoutPresent = $script:TimeoutSet -or $timeoutUnknown -or (Test-DeviceConfigValueSet $timeoutValue)
        $compatPresent = $script:CompatSet -or $compatUnknown -or $overrides.Count -gt 0
        Write-Host "   found: timeout '$timeoutValue' (set by this run: $($script:TimeoutSet)), compat override lines $($overrides.Count) (enabled by this run: $($script:CompatSet))"
        if ($timeoutPresent) { Invoke-Adb $Phone @('shell', 'device_config', 'delete', 'activity_manager', 'data_sync_fgs_timeout_duration') | Out-Null }
        if ($compatPresent) { Invoke-Adb $Phone @('shell', 'am', 'compat', 'reset', 'FGS_INTRODUCE_TIME_LIMITS', $package) | Out-Null }
        Write-Host "   reset: timeout $(if ($timeoutPresent) { 'deleted' } else { 'none present, no call' }), compat flag $(if ($compatPresent) { 'reset' } else { 'none present, no call (a reset would kill the app)' })"
        Write-CompatState 'after the reset'
        return $true
    } catch { Write-Host "   WARNING: could not reset the dataSync timeout: $($_.Exception.Message)"; return $false }
}

# Only the dev app's network is cut (never wifi/data: the adb link is wireless, Slice 208 rule). The cut is made after the time limit fired so the run ends unfinished; always lifted in the finally block.
# Slice 225 item 15: with no uid (Get-PackageUid threw before it was read) `netpolicy remove ... 0` must never run. Round 3: that is NOT a restore: it returns $false ("not restored (no uid)"),
# so the device-side heal is not cancelled and a leftover block for the real uid from a dead run is still cleared by it. $true when the block is off.
function Set-Network([bool]$On) {
    if (-not $script:AppUid) { Write-Host '   network block not restored (no uid): none was read, so the device-side heal is left to run'; return $false }
    try { Set-AppNetworkBlocked $Phone $script:AppUid (-not $On); return $true }
    catch { Write-Host "   WARNING: could not $(if ($On) { 'lift' } else { 'set' }) the app network block: $($_.Exception.Message)"; return $false }
}

# The fast path restored everything over adb: the device-side heal is no longer needed and must not fire into a later run. If the fast path failed it is left to heal.
function Stop-HealWhenRestored([bool]$NetworkRestored, [bool]$TimeLimitRestored) {
    if (-not ($NetworkRestored -and $TimeLimitRestored)) { Write-Host '   the fast restore did not finish: the device-side restore is left to heal the phone'; return }
    $cancelled = Stop-DeviceRestore $Phone $package
    Write-Host "   device-side restore $(if ($cancelled) { 'cancelled (the fast restore finished)' } else { 'was not pending' })"
}

# Step 7 end: after the content intent the run restarts (PACKDL start again) and the notice is removed.
function Test-NoticeGone([int64]$SinceMs) {
    $records = Get-NotificationRecords @(Invoke-Adb $Phone @('shell', 'dumpsys notification --noredact')) $package
    return (@($records | Where-Object { $_.Title -eq $script:PausedTitle -and $_.PostTime -ge $SinceMs }).Count -eq 0)
}

function Move-PackAside {
    if ((Test-PhonePathExists $movedPath) -and -not (Test-PhonePathExists $packPath)) { Restore-MovedPack | Out-Null }
    if (Test-PhonePathExists $movedPath) { throw "$movedPath already exists next to a live ${packPath}: a former run left it; look at both before running again." }
    if (@(Get-InstalledPackIds $Phone $package @($packId)).Count -eq 0) { throw "$packId is not installed, so there is nothing to move aside (run -Prepare first)." }
    $aside = Save-PacksAside $Phone $package $script:AsideRoot $script:AsideTransferSeconds
    Write-Host "   aside copy verified: $($aside.Path) ($($aside.PackIds -join ', '))"
    $script:AsideCopy = $aside
    $moved = Invoke-RunAs "mv $packPath $movedPath"
    if ($moved.Exit -ne 0) { throw "could not move $packPath aside: $($moved.Out)" }
}

# Cancel, delete the partial and the half-made pack folder, move the original back, prove it. Returns $null when restored, else the reason.
function Restore-MovedPack {
    try { Send-DevHook $Phone $App 'cancel-pack-downloads' | Out-Null } catch { Write-Host "   WARNING: could not cancel the pack download: $($_.Exception.Message)" }
    try { Invoke-Adb $Phone @('shell', 'am', 'force-stop', $package) | Out-Null } catch { Write-Host "   WARNING: could not stop the app: $($_.Exception.Message)" }
    try {
        Invoke-Adb $Phone @('shell', "rm -f /sdcard/Android/data/$package/files/$($script:ThrottleMarkerName)") | Out-Null
        Invoke-RunAs "rm -f $($script:PacksDirectory)/$packId-*.partial" | Out-Null
        if (Test-PhonePathExists $movedPath) {
            Invoke-RunAs "rm -rf $packPath" | Out-Null
            $back = Invoke-RunAs "mv $movedPath $packPath"
            if ($back.Exit -ne 0) { return "mv back failed: $($back.Out)" }
        }
        if (@(Get-InstalledPackIds $Phone $package @($packId)).Count -eq 0) { return "$packId does not pass the install check after moving it back" }
        return $null
    } catch { return "restore threw: $($_.Exception.Message)" }
}

# The paused notice posted after $SinceMs: a record of the dev package whose title is the paused title.
function Wait-PausedNotice([int64]$SinceMs) {
    $started = Get-Date
    do {
        Assert-WithinBudget 'waiting for the paused notification'
        $records = Get-NotificationRecords @(Invoke-Adb $Phone @('shell', 'dumpsys notification --noredact')) $package
        $note = @($records | Where-Object { $_.Title -eq $script:PausedTitle -and $_.PostTime -ge $SinceMs } | Select-Object -Last 1)
        if ($note.Count -gt 0) { return $note[0] }
        Start-Sleep -Seconds 2
    } while (((Get-Date) - $started).TotalSeconds -lt $script:NoticeSeconds)
    return $null
}
$script:NoticeSeconds = 20

function Test-PausedText($Note) {
    return (($Note.Text -match $script:SizePattern) -and (Test-RecordNamesPack "$($Note.Title) $($Note.Text)" @($script:DownloadLanguageName)))
}

# Step 6: the notice's content intent opens the app; the launcher activity is what that intent starts.
function Test-AppResumedAfterTap {
    Invoke-Adb $Phone @('shell', 'am', 'start', '-n', (Get-LauncherActivity $Phone $package)) | Out-Null
    $deadline = (Get-Date).AddSeconds(15)
    do {
        Assert-WithinBudget 'waiting for the app to come back to the foreground'
        $resumed = "$(Invoke-Adb $Phone @('shell', 'dumpsys activity activities | grep -E "topResumedActivity|mResumedActivity"'))"
        if ($resumed -match [regex]::Escape($package)) { return $true }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)
    return $false
}

# The network-only variant still needs the app uid and the device-side heal that Set-ShortTimeLimit makes, but no time limit.
function Set-NetworkOnlySetup {
    Start-AppHeal
    Write-CompatState 'at the start of the network-only variant (it sets none)'
}

# True once no activity of the dev app is the resumed one (the HOME key has finished moving it out of the foreground).
function Wait-AppBackgrounded([int]$Seconds = 30) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        $resumed = @(Invoke-Adb $Phone @('shell', 'dumpsys activity activities') | Where-Object { "$_" -match 'topResumedActivity|mResumedActivity' })
        if (-not ($resumed -match [regex]::Escape($package))) { return $true }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)
    return $false
}

# Slice 233: an earlier cleanup (or the cancel hook) can leave the dev app in `VOICEPACK held-user-cancelled`, and then the download this check waits for never starts.
# Every variant therefore clears that hold first: reset-dev-app -Level clear (the installed packs are kept aside and put back; -Level data does not exist).
function Clear-DevDownloadHold {
    Write-Host "   clearing any download hold left by an earlier check (reset-dev-app -Level clear)"
    & (Join-Path (Join-Path $PSScriptRoot '..') 'reset-dev-app.ps1') -App $App -Level clear -Serial $Phone | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "reset-dev-app -Level clear did not bring $package back alive (exit $LASTEXITCODE), so a download hold could not be cleared" }
    # pm clear also resets the runtime notification permission (S21 2026-11-01: the channel read importance=NONE after it), so grant it again.
    $answer = (Invoke-Adb $Phone @('shell', 'pm', 'grant', $package, 'android.permission.POST_NOTIFICATIONS')) -join ' '
    if ($global:LASTEXITCODE -ne 0) { throw "pm grant POST_NOTIFICATIONS failed after the clear: $answer" }
    Write-Host "   notifications granted again after the clear"
}

# Slice 241 item 7: a download whose socket goes silent (the 2026-11-01 phone run: a .partial stuck at 9,011,200 bytes for 7 minutes with no PACKDL line). Same set-up as the paused checks (pack moved aside, throttled
# download); once the body is moving (a PACKDL progress line) the Debug hook stall-pack-network hangs the next read like a dead socket. The 60 s stall guard must then write `PACKDL stall pack=<id> phase=body` within 90 s of the hook,
# and the retry must follow (`PACKDL retry` or a new `PACKDL start`). No network setting is touched, so nothing needs the device-side restore.
$script:StallWithinSeconds = 90
$script:RetryWithinSeconds = 60
$script:ProgressWithinSeconds = 30
function Invoke-StallCheck {
    Set-DevMarker $Phone $package $script:ThrottleMarkerName
    $appPid = Start-DevAppWithHook $Phone $package 'skip-first-run'
    $start = Wait-LogMatch $Phone $package $appPid $script:PackStartPattern $script:PackStartSeconds 0 "waiting for PACKDL start of $packId"
    if (-not $start) { return (New-CheckResult $false (Get-NoPackStartReason (Get-PidLog $Phone $appPid) @($packId))) }
    $progress = Wait-LogMatch $Phone $package $appPid 'PACKDL progress pack=\S+ bytes=\d+' $script:ProgressWithinSeconds 0 'waiting for a PACKDL progress line (the body is moving)'
    if (-not $progress) { return (New-CheckResult $false "no 'PACKDL progress' line within $($script:ProgressWithinSeconds) s of the download start: the body is not moving or the progress log is missing. The app's last lines: $(Get-AppLogTail (Get-PidLog $Phone $appPid) 6)") }
    Write-Host "   body moving: $($progress.Line.Trim()); hanging the next read with the stall-pack-network hook"
    $armedAt = (Get-PhoneEpochMs) / 1000.0
    $hook = Send-DevHook $Phone $App 'stall-pack-network'
    Write-Host "   stall-pack-network: $hook"
    $stall = Wait-LogMatch $Phone $package $appPid 'PACKDL stall pack=(\S+) phase=body bytes=(\d+)' $script:StallWithinSeconds $armedAt "waiting for PACKDL stall within $($script:StallWithinSeconds) s of the hook"
    if (-not $stall) { return (New-CheckResult $false "no 'PACKDL stall ... phase=body' within $($script:StallWithinSeconds) s of the hung read: the stall guard did not fire or did not log. The app's last lines: $(Get-AppLogTail (Get-PidLog $Phone $appPid) 8)") }
    Write-Host "   $($stall.Line.Trim())"
    $retry = Wait-LogMatch $Phone $package $appPid 'PACKDL (retry|start) ' $script:RetryWithinSeconds ($stall.Time + 0.001) 'waiting for the retry after the stall'
    if (-not $retry) { return (New-CheckResult $false "the stall was logged ($($stall.Line.Trim())) but no 'PACKDL retry' or new 'PACKDL start' followed within $($script:RetryWithinSeconds) s: the download did not go on") }
    return (New-CheckResult $true "hung read ended as '$($stall.Line.Trim())' within $($script:StallWithinSeconds) s; then '$($retry.Line.Trim())'")
}

function Invoke-PausedCheck {
    if ($NetworkOnly) { Set-NetworkOnlySetup } else { Assert-TimeLimitPhone; Set-ShortTimeLimit }
    Set-DevMarker $Phone $package $script:ThrottleMarkerName
    $since = Get-PhoneEpochMs
    $appPid = Start-DevAppWithHook $Phone $package 'skip-first-run'
    $start = Wait-LogMatch $Phone $package $appPid $script:PackStartPattern $script:PackStartSeconds 0 "waiting for PACKDL start of $packId"
    if (-not $start) { return (New-CheckResult $false (Get-NoPackStartReason (Get-PidLog $Phone $appPid) @($packId))) }
    Write-Host "   download started: $($start.Line.Trim()); sending the app to the background"
    Invoke-Adb $Phone @('shell', 'input', 'keyevent', 'KEYCODE_HOME') | Out-Null
    if ($NetworkOnly) {
        if (-not (Wait-AppBackgrounded)) { return (New-CheckResult $false "$package was still the resumed app 30 s after the HOME key, so the network cut would not be a background end") }
        Start-Sleep -Seconds $script:BackgroundSettleSeconds
        $timeout = [pscustomobject]@{ Time = $start.Time }
        Write-Host "   no time limit in this variant; dropping the running download's connection with the drop-pack-network hook (a running dataSync service is exempt from the per-app network block)"
    } else {
        $timeout = Wait-LogMatch $Phone $package $appPid $script:TimeoutMarkerPattern $script:TimeoutSeconds 0 "waiting for PACKDL service timeout ($($script:DataSyncTimeoutMs / 1000) s time limit)"
        if (-not $timeout) { return (New-CheckResult $false "no 'PACKDL service timeout' within $($script:TimeoutSeconds) s of backgrounding the app (the time limit did not fire). The app's last lines: $(Get-AppLogTail (Get-PidLog $Phone $appPid) 6)") }
        if (-not (Test-NoticeGone $since)) { return (New-CheckResult $false "a paused notice was posted at the time limit itself, before the run ended") }
        Write-Host "   time limit fired; blocking the dev app's network (not the phone's) so the run ends unfinished"
    }
    $cutAt = (Get-PhoneEpochMs) / 1000.0   # the marker must come from this run's network cut, never an earlier one
    if ($NetworkOnly) {
        $drop = Send-DevHook $Phone $App 'drop-pack-network'
        Write-Host "   drop-pack-network: $drop"
    } else {
        $null = Set-Network $false
    }
    $marker = Wait-LogMatch $Phone $package $appPid $script:PausedMarkerPattern $PausedTimeoutSeconds $cutAt "waiting for PACKDL paused-notified after the run failed"
    if (-not $marker) { return (New-CheckResult $false "no 'PACKDL service paused-notified' within $PausedTimeoutSeconds s of cutting the network (the run did not end unfinished, or the app was on screen). The app's last lines: $(Get-AppLogTail (Get-PidLog $Phone $appPid) 6)") }
    Write-Host "   $($marker.Line.Trim())"
    if ($NetworkOnly -and $marker.Line -notmatch 'reason=(offline|failed|transport)') { return (New-CheckResult $false "the paused-notified marker carries no end reason (Slice 233): $($marker.Line.Trim())") }
    $note = Wait-PausedNotice $since
    if (-not $note) { return (New-CheckResult $false "the log said paused-notified but no notification titled '$($script:PausedTitle)' is in dumpsys notification (notifications off for the dev app?)") }
    if (-not (Test-PausedText $note)) { return (New-CheckResult $false "the paused notification does not name the language and the size: title '$($note.Title)', text '$($note.Text)'") }
    if (-not $NetworkOnly) { $null = Set-Network $true }
    if (-not (Test-AppResumedAfterTap)) { return (New-CheckResult $false "launching the notice's target did not bring $package to the foreground") }
    $restart = Wait-LogMatch $Phone $package $appPid $script:PackStartPattern $script:ResumeSeconds ($marker.Time + 0.001) "waiting for the download to start again"
    if (-not $restart) { return (New-CheckResult $false "the download did not log PACKDL start again within $($script:ResumeSeconds) s of the network coming back and the app opening") }
    if (-not (Test-NoticeGone $since)) { return (New-CheckResult $false "the paused notice is still shown after the download restarted") }
    return (New-CheckResult $true "paused notification title '$($note.Title)', text '$($note.Text)'; the app came back from its content intent, the download restarted ($($restart.Line.Trim())) and the notice is gone")
}

$result = $null
$restoreProblem = $null
$budgetError = $false
$moved = $false
try {
    Clear-DevDownloadHold
    Move-PackAside
    $moved = $true
    $result = if ($Stall) { Invoke-StallCheck } else { Invoke-PausedCheck }
} catch {
    $budgetError = $_.Exception.Message.StartsWith($script:BudgetExceededPrefix)
    $result = New-CheckResult $false "check could not run: $($_.Exception.Message)"
    if (-not $moved -and (Test-PhonePathExists $movedPath)) { $moved = $true }   # the move happened but something after it threw
} finally {
    $networkRestored = $true; $timeLimitRestored = $true
    if (-not $NetworkOnly) { $networkRestored = Set-Network $true }   # the network-only variant never blocks the app's network (Slice 233)
    if (-not $NetworkOnly) { $timeLimitRestored = Reset-TimeLimit }   # the network-only variant never set the timeout or the compat flag: leave any the phone already had
    if ($script:HealStarted) { Stop-HealWhenRestored $networkRestored $timeLimitRestored }
    if ($moved) { $restoreProblem = Restore-MovedPack }
}
if ($budgetError -and -not $restoreProblem) { throw $result.Detail.Substring('check could not run: '.Length) }   # a timeout stays a TIMEOUT (the pack is restored first)
$asidePath = if ($script:AsideCopy) { $script:AsideCopy.Path } else { '(none made)' }
if ($restoreProblem) { return (New-CheckResult $false "RESTORE FAILED: $restoreProblem. The verified aside copy is $asidePath; run reset-dev-app.ps1 or -Prepare. (check itself: $($result.Detail))") }
if ($moved) { Write-Host "   restore verified: $packId passes Test-PackInstalled" }
return $result
