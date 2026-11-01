# check: Apps=dino EstimateSeconds=420 NeedsPack=true Variant=debug
<#
.SYNOPSIS
  book-end: Dino plays a starter book to its end, then the phone can doze (Slice 213).
.DESCRIPTION
  Sends `open-book id=<book> page=1 play=1 speed=2` (play=1 reads on to the end of the book, DinoTestCommands) and waits for READ_END. Reports how long the book
  took, then polls until the app holds no wake lock (`dumpsys power`, only the LIVE `Wake Locks: size=N` block counts, never the wakelock history below it, which keeps old locks forever; a lock is the app's when its uid or its ws=WorkSource{<uid>} is the app's, so Dino's keep-screen-on SCREEN_BRIGHT_WAKE_LOCK of uid 1000 counts) and no foreground service (`dumpsys activity services`), which is
  the time to doze. Passes when both are released within -DozeLimitSeconds of READ_END. Fails (never passes) when the uid or the dumps cannot be read, and needs proof the book played (HOOK open-book opened, at least 2 CHUNK lines, READ_END). The dump formats are read by pattern and are not yet confirmed on the S21.
#>
param(
    [Parameter(Mandatory)][string]$Phone,
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [string]$Book = 'three-little-pigs',
    [int]$EndTimeoutSeconds = 900,
    [int]$DozeLimitSeconds = 60
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\common.ps1')
. (Join-Path $PSScriptRoot '_lib.ps1')
. (Join-Path $PSScriptRoot '_device.ps1')

$package = Get-DevPackage $App

# The two dumps; a dump that does not look like the real one is an error, never "nothing is held".
function Test-PhoneAwakeBecauseOfApp([string]$Uid) {
    $power = (Invoke-Adb $Phone @('shell', 'dumpsys', 'power')) -join "`n"
    $services = (Invoke-Adb $Phone @('shell', 'dumpsys', 'activity', 'services', $package)) -join "`n"
    $problem = Get-WakeDumpProblem $power $services
    if ($problem) { throw "Cannot tell whether the app holds the phone awake: $problem." }
    return (Test-AppHoldsWake $power $services $Uid)
}

function Wait-WakeReleased([string]$Uid, [datetime]$Since) {
    $deadline = $Since.AddSeconds($DozeLimitSeconds)
    while ((Get-Date) -lt $deadline) {
        Assert-WithinBudget 'waiting for the app to release its wake lock'
        if (-not (Test-PhoneAwakeBecauseOfApp $Uid)) { return [int]((Get-Date) - $Since).TotalSeconds }
        Start-Sleep -Seconds 2
    }
    return $null
}

# The uid the wake locks are matched by; without it the lock test would skip and pass, so not finding it is a failure.
function Get-AppUid {
    $uid = Get-PackageUid ((Invoke-Adb $Phone @('shell', 'dumpsys', 'package', $package)) -join "`n")
    if (-not $uid) { throw "Could not read the uid of $package from dumpsys package (no userId=): the wake-lock check cannot run." }
    return $uid
}

$appPid = Start-ReadyApp $Phone $App $package
$uid = Get-AppUid
$begun = Get-Date
$sent = Send-DevHook $Phone $App 'open-book' @{ id = $Book; page = '1'; play = '1'; speed = '2' }
$opened = Wait-LogMatch $Phone $package $appPid 'HOOK open-book (opened|failed|fault)' $script:HookAnswerSeconds $sent 'waiting for the app to open the book'
if (-not $opened -or $opened.Line -notmatch $script:BookOpenedPattern) { return (New-CheckResult $false "the book '$Book' did not open: $(if ($opened) { $opened.Line } else { 'no HOOK open-book line in 30 s' })") }
$end = Wait-LogMatch $Phone $package $appPid 'READ_END\s*$' $EndTimeoutSeconds $sent 'playing the book to its end'
if (-not $end) { return (New-CheckResult $false "READ_END not seen within $EndTimeoutSeconds s of starting '$Book'") }
$bookSeconds = [int]((Get-Date) - $begun).TotalSeconds
$proof = Resolve-BookPlayProof (Get-PidLog $Phone $appPid)
if (-not $proof.Played) { return (New-CheckResult $false "'$Book' did not play: $($proof.Problem)") }
Write-Host "   '$Book' ended after about $bookSeconds s ($($proof.Chunks) chunks)"
$dozeSeconds = Wait-WakeReleased $uid (Get-Date)
if ($null -eq $dozeSeconds) { return (New-CheckResult $false "'$Book' ended after ~$bookSeconds s, but the app still held a wake lock or foreground service $DozeLimitSeconds s later") }
return (New-CheckResult $true "'$Book' played $($proof.Chunks) chunks and ended after ~$bookSeconds s; wake lock and foreground service released $dozeSeconds s after the end")
