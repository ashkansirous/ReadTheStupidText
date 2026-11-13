# check: Apps=dino EstimateSeconds=240 NeedsPack=false Variant=debug
<#
.SYNOPSIS
  book-end: Dino plays a test book (one of the four skip-first-run imports, Slice 261) to its end, then the phone can doze (Slice 213).
.DESCRIPTION
  Sends `open-book id=<book> page=1 play=1 speed=2` (play=1 reads on to the end of the book, DinoTestCommands) and waits for `[Usage] book_finished`. Reports how long the book
  took, then polls until the app holds no wake lock (`dumpsys power`, only the LIVE `Wake Locks: size=N` block counts, never the wakelock history below it, which keeps old locks forever; a lock is the app's when its uid or its ws=WorkSource{<uid>} is the app's, so Dino's keep-screen-on SCREEN_BRIGHT_WAKE_LOCK of uid 1000 counts) and no foreground service (`dumpsys activity services`), which is
  the time to doze. Passes when both are released within -DozeLimitSeconds of the end. Fails (never passes) when the uid or the dumps cannot be read, and needs proof the book played (HOOK open-book opened, at least 2 CHUNK or `[AudioPack] premade clip plays` lines, `book_finished`). The dump formats are read by pattern and are not yet confirmed on the S21.
  Slice 372 item 3: on the S21 (2026-11-13) the book was still playing 521 s in. Cause found in the code: `open-book` never read `speed=`, so `speed=2` was ignored and the book played at the saved speed (1x); DinoTestCommands now holds a given speed for the app run only (playback reads it, nothing is saved) and logs `HOOK open-book speed 2`.
  Slice 372 item 1 (S21 run 2026-11-13, speed 2 applied: `HOOK open-book speed 2` 17:08:22, `read_stopped finished seconds=99` and `book_finished` on page 15 of 16 at 17:10:04): the book took about 102 s at 2x, SILENT, and the check waited for `READ_END`, which Dino never writes (TestMarkers follows the shared speech reader, which Dino's clip player bypasses). It now waits for the `[Usage] book_finished` line. The run was silent (`[AudioPack] silent piece shown ... no_pack`) because the app started in `en` while the fixture pack is `fa`; Start-ReadyDinoApp now starts the app in the pack's language, and a run with silent pieces and no clips fails with "no voice pack for the language ..." instead of passing or timing out. A voiced run is not measured yet (a clip's length is not in the log), so the first voiced run prints the real time.
  Estimate 240 s = about 40 s start-up (app start, skip-first-run, fixture pack, open-book answer) + about 110 s of play + the 60 s doze poll, with margin. The end timeout is 240 s (2.3x the measured silent 102 s, room for clips longer than the silent beats); worst case 40 + 240 + 60 = 340 s, inside the default 10-minute budget. It runs ALONE (`run-checks.ps1 -App dino -Checks book-end`); behind other checks the runner skips it ("not enough budget").
  Slice 320 item 4: Dino has no voice pack since Slice 278 (the check used to wait for PACK_READY of a kokoro-en pack and could never start). It now installs the Debug fixture audio pack of the book (`install-audio-pack`, Start-ReadyDinoApp) so the read plays clips; no pack download, NeedsPack=false.
#>
param(
    [Parameter(Mandatory)][string]$Phone,
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [string]$Book = 'three-little-pigs',
    [int]$EndTimeoutSeconds = 240,
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
    $uid = Get-PackageUidFromDump ((Invoke-Adb $Phone @('shell', 'dumpsys', 'package', $package)) -join "`n")
    if (-not $uid) { throw "Could not read the uid of $package from dumpsys package (no userId=): the wake-lock check cannot run." }
    return $uid
}

$appPid = Start-ReadyDinoApp $Phone $package $Book
$uid = Get-AppUid
$begun = Get-Date
$sent = Send-DevHook $Phone $App 'open-book' @{ id = $Book; page = '1'; play = '1'; speed = '2' }
$opened = Wait-LogMatch $Phone $package $appPid 'HOOK open-book (opened|failed|fault)' $script:HookAnswerSeconds $sent 'waiting for the app to open the book'
if (-not $opened -or $opened.Line -notmatch $script:BookOpenedPattern) { return (New-CheckResult $false "the book '$Book' did not open: $(if ($opened) { $opened.Line } else { 'no HOOK open-book line in 30 s' })") }
$end = Wait-LogMatch $Phone $package $appPid $script:BookFinishedPattern $EndTimeoutSeconds $sent 'playing the book to its end'
if (-not $end) { return (New-CheckResult $false "no '[Usage] book_finished' line within $EndTimeoutSeconds s of starting '$Book' (the log's 'HOOK open-book speed' line shows the speed that was applied; none means an old build that ignores speed=)") }
$bookSeconds = [int]((Get-Date) - $begun).TotalSeconds
$proof = Resolve-BookPlayProof (Get-PidLog $Phone $appPid)
if (-not $proof.Played) { return (New-CheckResult $false "'$Book' did not play: $($proof.Problem)") }
Write-Host "   '$Book' ended after about $bookSeconds s ($($proof.Chunks) chunks)"
$dozeSeconds = Wait-WakeReleased $uid (Get-Date)
if ($null -eq $dozeSeconds) { return (New-CheckResult $false "'$Book' ended after ~$bookSeconds s, but the app still held a wake lock or foreground service $DozeLimitSeconds s later") }
return (New-CheckResult $true "'$Book' played $($proof.Chunks) chunks and ended after ~$bookSeconds s; wake lock and foreground service released $dozeSeconds s after the end")
