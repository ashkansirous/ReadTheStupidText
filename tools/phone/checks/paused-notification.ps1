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
  Wipes: only the dev app's pack folder for the pack under test (moved aside and restored, never cleared); no app data.
#>
param(
    [Parameter(Mandatory)][string]$Phone,
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [int]$PausedTimeoutSeconds = 150
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
$script:PausedMarkerPattern = 'PACKDL service paused-notified pack=(\S+)'
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

function Set-ShortTimeLimit {
    $script:AppUid = Get-PackageUid $Phone $package
    Start-DeviceRestore $Phone $package $script:AppUid $script:DeviceRestoreSeconds   # device-side heal first, before anything is changed
    Invoke-Adb $Phone @('shell', 'device_config', 'put', 'activity_manager', 'data_sync_fgs_timeout_duration', "$($script:DataSyncTimeoutMs)") | Out-Null
    Invoke-Adb $Phone @('shell', 'am', 'compat', 'enable', 'FGS_INTRODUCE_TIME_LIMITS', $package) | Out-Null
}

# Never throws: runs in the finally block.
function Reset-TimeLimit {
    try {
        Invoke-Adb $Phone @('shell', 'device_config', 'delete', 'activity_manager', 'data_sync_fgs_timeout_duration') | Out-Null
        Invoke-Adb $Phone @('shell', 'am', 'compat', 'reset', 'FGS_INTRODUCE_TIME_LIMITS', $package) | Out-Null
    } catch { Write-Host "   WARNING: could not reset the dataSync timeout: $($_.Exception.Message)" }
}

# Only the dev app's network is cut (never wifi/data: the adb link is wireless, Slice 208 rule). The cut is made after the time limit fired so the run ends unfinished; always lifted in the finally block.
function Set-Network([bool]$On) {
    try { Set-AppNetworkBlocked $Phone $script:AppUid (-not $On) }
    catch { Write-Host "   WARNING: could not $(if ($On) { 'lift' } else { 'set' }) the app network block: $($_.Exception.Message)" }
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

function Invoke-PausedCheck {
    Assert-TimeLimitPhone
    Set-ShortTimeLimit
    Set-DevMarker $Phone $package $script:ThrottleMarkerName
    $since = Get-PhoneEpochMs
    $appPid = Start-DevAppWithHook $Phone $package 'skip-first-run'
    $start = Wait-LogMatch $Phone $package $appPid $script:PackStartPattern $script:PackStartSeconds 0 "waiting for PACKDL start of $packId"
    if (-not $start) { return (New-CheckResult $false (Get-NoPackStartReason (Get-PidLog $Phone $appPid) @($packId))) }
    Write-Host "   download started: $($start.Line.Trim()); sending the app to the background"
    Invoke-Adb $Phone @('shell', 'input', 'keyevent', 'KEYCODE_HOME') | Out-Null
    $timeout = Wait-LogMatch $Phone $package $appPid $script:TimeoutMarkerPattern $script:TimeoutSeconds 0 "waiting for PACKDL service timeout ($($script:DataSyncTimeoutMs / 1000) s time limit)"
    if (-not $timeout) { return (New-CheckResult $false "no 'PACKDL service timeout' within $($script:TimeoutSeconds) s of backgrounding the app (the time limit did not fire). The app's last lines: $(Get-AppLogTail (Get-PidLog $Phone $appPid) 6)") }
    if (-not (Test-NoticeGone $since)) { return (New-CheckResult $false "a paused notice was posted at the time limit itself, before the run ended") }
    Write-Host "   time limit fired; blocking the dev app's network (not the phone's) so the run ends unfinished"
    Set-Network $false
    $marker = Wait-LogMatch $Phone $package $appPid $script:PausedMarkerPattern $PausedTimeoutSeconds $timeout.Time "waiting for PACKDL paused-notified after the run failed"
    if (-not $marker) { return (New-CheckResult $false "no 'PACKDL service paused-notified' within $PausedTimeoutSeconds s of cutting the network (the run did not end unfinished, or the app was on screen). The app's last lines: $(Get-AppLogTail (Get-PidLog $Phone $appPid) 6)") }
    Write-Host "   $($marker.Line.Trim())"
    $note = Wait-PausedNotice $since
    if (-not $note) { return (New-CheckResult $false "the log said paused-notified but no notification titled '$($script:PausedTitle)' is in dumpsys notification (notifications off for the dev app?)") }
    if (-not (Test-PausedText $note)) { return (New-CheckResult $false "the paused notification does not name the language and the size: title '$($note.Title)', text '$($note.Text)'") }
    Set-Network $true
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
    Move-PackAside
    $moved = $true
    $result = Invoke-PausedCheck
} catch {
    $budgetError = $_.Exception.Message.StartsWith($script:BudgetExceededPrefix)
    $result = New-CheckResult $false "check could not run: $($_.Exception.Message)"
    if (-not $moved -and (Test-PhonePathExists $movedPath)) { $moved = $true }   # the move happened but something after it threw
} finally {
    Set-Network $true
    Reset-TimeLimit
    if ($moved) { $restoreProblem = Restore-MovedPack }
}
if ($budgetError -and -not $restoreProblem) { throw $result.Detail.Substring('check could not run: '.Length) }   # a timeout stays a TIMEOUT (the pack is restored first)
$asidePath = if ($script:AsideCopy) { $script:AsideCopy.Path } else { '(none made)' }
if ($restoreProblem) { return (New-CheckResult $false "RESTORE FAILED: $restoreProblem. The verified aside copy is $asidePath; run reset-dev-app.ps1 or -Prepare. (check itself: $($result.Detail))") }
if ($moved) { Write-Host "   restore verified: $packId passes Test-PackInstalled" }
return $result
