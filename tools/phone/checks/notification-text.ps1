<#
.SYNOPSIS
  notification-text: a throttled voice-pack download shows a progress notification (Slice 183 throttle, Slice 213).
.DESCRIPTION
  The pack the current language uses (RTST English: supertonic-3) is the one downloaded, because the app only downloads the pack it wants (no hook changes the language yet, Slice 209).
  1. A verified aside copy of the installed packs is made (Save-PacksAside). 2. The pack folder is MOVED (run-as mv, never deleted) to a sibling temp name. 3. The Debug throttle marker is written
  and the dev app launched in the foreground; `PACKDL start pack=<id>` must be logged within 60 s, else FAIL quoting the app's last RTST-TEST lines. 4. The newest notification of the dev
  package posted after that start with a percentage in its title or text is read (verbatim title and text in the result); PASS = such a notification was shown (matched by package and post
  time, not by pack name: ExpectPackNameInNotification in _lib.ps1 is the Slice 208 hook). 5. In a finally block: cancel the download (cancel-pack-downloads hook), delete the .partial and the
  half-made pack folder, move the original pack back, verify it with Test-PackInstalled; if that fails the check FAILS (the aside copy is named in the detail).
#>
param(
    [Parameter(Mandatory)][string]$Phone,
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [int]$NotificationTimeoutSeconds = 60
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\common.ps1')
. (Join-Path $PSScriptRoot '_lib.ps1')
. (Join-Path $PSScriptRoot '_device.ps1')

$package = Get-DevPackage $App
$script:AsideRoot = '/sdcard/Download/rtst-packs-aside'
$script:AsideTransferSeconds = 300
$script:MovedSuffix = '.slice213-moved'
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

# Step 1 and 2: aside copy, then the pack folder moved aside. A leftover moved folder of an earlier crashed run is put back first.
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

# Step 5: cancel, delete the partial and the half-made pack folder, move the original back, prove it. Returns $null when restored, else the reason.
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

function Wait-PostedNotification([int64]$SinceMs) {
    $started = Get-Date
    do {
        Assert-WithinBudget 'waiting for the download notification'
        $records = Get-NotificationRecords @(Invoke-Adb $Phone @('shell', 'dumpsys notification --noredact')) $package
        $note = Select-PostedProgressNotification $records $SinceMs @($packId)
        if ($note) { return $note }
        Start-Sleep -Seconds 2
    } while (((Get-Date) - $started).TotalSeconds -lt $NotificationTimeoutSeconds)
    return $null
}

# Step 3 and 4.
function Invoke-DownloadNotificationCheck {
    Set-DevMarker $Phone $package $script:ThrottleMarkerName
    $since = Get-PhoneEpochMs
    $appPid = Start-DevAppWithHook $Phone $package 'skip-first-run'
    $start = Wait-LogMatch $Phone $package $appPid $script:PackStartPattern $script:PackStartSeconds 0 "waiting for PACKDL start of $packId"
    if (-not $start) { return (New-CheckResult $false (Get-NoPackStartReason (Get-PidLog $Phone $appPid) @($packId))) }
    Write-Host "   download started: $($start.Line.Trim())"
    $verdict = Resolve-NotificationVerdict (Wait-PostedNotification $since)
    return (New-CheckResult $verdict.Passed $verdict.Detail)
}

$result = $null
$restoreProblem = $null
$moved = $false
try {
    Move-PackAside
    $moved = $true
    $result = Invoke-DownloadNotificationCheck
} catch {
    $result = New-CheckResult $false "check could not run: $($_.Exception.Message)"
    if (-not $moved -and (Test-PhonePathExists $movedPath)) { $moved = $true }   # the move happened but something after it threw
} finally {
    if ($moved) { $restoreProblem = Restore-MovedPack }
}
$asidePath = if ($script:AsideCopy) { $script:AsideCopy.Path } else { '(none made)' }
if ($restoreProblem) { return (New-CheckResult $false "RESTORE FAILED: $restoreProblem. The verified aside copy is $asidePath; run reset-dev-app.ps1 or -Prepare. (check itself: $($result.Detail))") }
if ($moved) { Write-Host "   restore verified: $packId passes Test-PackInstalled" }
return $result
