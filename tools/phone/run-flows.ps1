<#
.SYNOPSIS
  Runs Maestro flows from .maestro/<app> on the real phone, inside a fixed time budget (Slice 448).
.DESCRIPTION
  run-flows.ps1 -App dino -Flow store-restore,paid-books,subscription,store-payment-required [-Budget 10] [-Install] [-Serial s] [-Force] [-DryRun]
  A run writes the same run marker as run-checks.ps1 (same phone key and owner): a second run with that owner while the first is alive exits 4; -Force takes the marker over.
  The phone twin of tools/emulator/run-flows.ps1 for the flows the emulator cannot judge (a real phone, USB first, else wireless adb; never ask for it). A flow is named, never a whole folder.
  Order: lint the flows (tools/emulator/lint-flows.ps1) -> find Maestro (MAESTRO_HOME, else <worktrees>\.tools\maestro) -> pick a JDK for this process (Set-ProcessJavaHome, Slice 389)
  -> find the phone -> [-Install: run-checks.ps1 -Prepare -App <app> puts the cached Debug build on the phone, then lets go of the lease] -> take the phone lease (owner as run-checks:
  RTST_DEVICE_OWNER, else RTST_PHONE_OWNER, else run-checks-<phone key>; waits at most 3 minutes) -> preflight.ps1 -Fix (charger, screen lock, stay-awake) -> the dev app must be installed and
  debuggable (the Debug build carries the test hooks) -> each flow: logcat cleared, Maestro started, stopped when the budget is gone, the flow's `# expect-log:` texts read from logcat (tag RTST-TEST)
  -> the dev app is force-stopped and the lease released (the stay-on setting put back), whatever happened.
  First run: the flows get past FirstRunLanguage the way the emulator's do, with the Debug hook `skip-first-run` in their own launchApp arguments (no tapping); some flows wait for that page on purpose.
  A flow with `# needs: cellular-only` is SKIPPED (it would cut the phone's network); a flow whose appId is not the app's dev package is SKIPPED (the store package is never a target).
  The budget (-Budget minutes, default 10) runs from the first flow, after install, lease and preflight; a flow is stopped (TIMEOUT, its Maestro tree killed) when the time is gone and the flows
  after it are SKIPPED. -Install (and a build it needs), the lease wait (3 minutes) and preflight run outside the budget, each with its own cap.
  Output: one PASS / FAIL / SKIP / TIMEOUT line per flow and a summary line; a FAIL line shows Maestro's own failing-step lines (JVM WARNING lines dropped) and the paths of the flow's full log (phone-<flow>.log) and its --debug-output folder (debug-<flow>), both in <worktrees>\.tools\maestro-out\phone\.
  A flow whose include or runScript file cannot be resolved (a ${VARIABLE} path, a missing file) is SKIPPED as an unresolved include, since it cannot be checked for steps that cut the network.
  -DryRun prints the plan (flows, refusals, Maestro command lines) and exits 0 before any phone, lease, build or Maestro is touched (the self-test seam).
  Exit: 0 all passed, 1 a flow failed or timed out, 2 no phone, 3 some flows did not run, 4 the phone is leased by someone else, 5 preflight NO-GO, the dev app is missing / not debuggable, or the stay-on setting could not be recorded (then it is left unchanged),
  7 the runner itself broke (no JDK, no Maestro, the lint, an install).
  Self-test with no device: test-run-flows.ps1 (pwsh and powershell 5.1).
#>
param(
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [Parameter(Mandatory)][string[]]$Flow,
    [ValidateRange(1, 120)][int]$Budget = 10,
    [switch]$Install,
    [switch]$DryRun,
    [string]$Serial,
    [switch]$Force   # take the run marker over from a live run with the same lease owner (as run-checks.ps1 -Force)
)
$ErrorActionPreference = 'Stop'
$script:RunStart = Get-Date
$script:RequestedSerial = $Serial   # review 1: the emulator common.ps1 below sets $script:Serial = 'emulator-5554' in this very scope, which overwrote -Serial
. (Join-Path $PSScriptRoot '..\emulator\common.ps1')   # first: Set-ProcessJavaHome, Find-FlowFiles, Test-LogMarker; the phone helpers below win any shared name
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'flows-lib.ps1')

$script:ExitNoPhone = 2
$script:ExitLeaseBusy = 4
$script:ExitNotReady = 5
$script:ExitBroke = 7
$script:LeaseWaitMinutes = 3
$script:LeaseExtraMinutes = 15
$script:LogTag = 'RTST-TEST'
$script:LogGraceSeconds = 15
$script:Package = Get-DevPackage $App
$script:FlowDir = Join-Path $script:RepoRoot ".maestro\$App"
$script:ToolsDir = Split-Path $script:StateDir -Parent

function Find-MaestroLauncher {
    $candidates = @()
    if ($env:MAESTRO_HOME) { $candidates += Join-Path $env:MAESTRO_HOME 'bin\maestro.bat' }
    $candidates += Join-Path $script:ToolsDir '.tools\maestro\bin\maestro.bat'
    $found = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $found) { throw "Maestro not found. Unzip maestro.zip (github.com/mobile-dev-inc/maestro/releases) to $($candidates[-1] -replace '\\bin\\maestro.bat$') or set MAESTRO_HOME." }
    return $found
}

# One entry per requested flow: its file, or why it does not run (missing, wrong app, would cut the network). Reads the flow files only.
function New-FlowPlan {
    $names = ConvertTo-FlowNameList $Flow
    $request = Resolve-FlowRequest $script:FlowDir $names
    $plan = @()
    foreach ($name in $request.Missing) { $plan += [pscustomobject]@{ Name = $name; File = $null; Refusal = "no flow $name.yaml under .maestro\$App" } }
    foreach ($file in $request.Files) {
        $refusal = Get-FlowRefusal (Get-Content -Raw $file.FullName) $App $file.FullName
        $plan += [pscustomobject]@{ Name = $file.BaseName; File = $file; Refusal = $refusal }
    }
    return $plan
}

function Write-DryRun($Plan) {
    Write-Host "DRY RUN: nothing touched (no phone, lease, build or Maestro). App $App, package $($script:Package), budget $Budget min."
    foreach ($item in $Plan) {
        if ($item.Refusal) { Write-Host "  SKIP $($item.Name): $($item.Refusal)"; continue }
        Write-Host "  RUN  $($item.Name): maestro $(Get-MaestroFlowArguments '<serial>' '<out>' $item.File.FullName)"
    }
}

# Review 7: lints only the flow files this run will start (an error in an unrelated flow must not block the phone stage). lint-flows.ps1 -Path takes the files joined with ';' (one call, with -CheckSource, Slice 406 item 3c).
function Invoke-FlowLint($Plan) {
    $engine = (Get-Process -Id $PID).Path
    $ErrorActionPreference = 'Continue'   # the child's exit code is the answer; a stderr line must not end this script
    $flows = @($Plan | Where-Object { -not $_.Refusal } | ForEach-Object { $_.File.FullName })
    if ($flows.Count -eq 0) { return $true }
    # Slice 406 item 3c: one call for every flow of the run, with -CheckSource, so the `# expect-log:` source check runs on the phone path and the source is read once (not once per flow).
    $lines = @(& $engine -NoProfile -File (Join-Path $PSScriptRoot '..\emulator\lint-flows.ps1') -Path ($flows -join ';') -CheckSource 2>&1 | ForEach-Object { "$_" })
    if ($LASTEXITCODE -eq 0) { Write-Host 'lint-flows: clean (the flows of this run, markers checked against the source)'; return $true }
    $lines | Select-Object -First 12 | ForEach-Object { Write-Host "  $_" }
    return $false
}

function Invoke-InstallStep([string]$Phone) {
    Write-Host "install: run-checks.ps1 -Prepare -App $App (the cached Debug build, built first when missing; outside the budget)"
    $engine = (Get-Process -Id $PID).Path
    $ErrorActionPreference = 'Continue'
    & $engine -NoProfile -File (Join-Path $PSScriptRoot 'run-checks.ps1') -Prepare -App $App -Serial $Phone 2>&1 | ForEach-Object { Write-Host "   $_" }
    return ($LASTEXITCODE -eq 0)
}

function Invoke-PreflightStep([string]$Phone) {
    $engine = (Get-Process -Id $PID).Path
    $ErrorActionPreference = 'Continue'
    $lines = @(& $engine -NoProfile -File (Join-Path $PSScriptRoot 'preflight.ps1') -Serial $Phone -App $App -Fix 2>&1 | ForEach-Object { "$_" })
    $verdict = $lines | Where-Object { $_.Trim() -like 'PREFLIGHT *' } | Select-Object -Last 1
    Write-Host "preflight: $verdict"
    if ($verdict -and $verdict.Trim() -eq 'PREFLIGHT GO') { return $true }
    $lines | Select-Object -Last 8 | ForEach-Object { Write-Host "   $_" }
    return $false
}

# $null when the dev app is installed and debuggable, else why not.
function Get-DevAppProblem([string]$Phone) {
    $listing = Join-PmLines (Invoke-Adb $Phone @('shell', 'pm', 'list', 'packages', $script:Package))   # review 5: one line per package, so the per-line match sees each
    if ($listing -notmatch "(?m)^package:$([regex]::Escape($script:Package))\s*$") { return "$($script:Package) is not installed on the phone: run with -Install" }
    $dump = Join-PmLines (Invoke-Adb $Phone @('shell', 'dumpsys', 'package', $script:Package))
    if (-not (Test-DebuggableInstall $dump)) { return "$($script:Package) is installed but not the Debug build (no DEBUGGABLE flag, so no test hooks): run with -Install" }
    return $null
}

function Stop-MaestroTree([Diagnostics.Process]$Process) {
    & taskkill /PID $Process.Id /T /F 2>&1 | Out-Null
    $null = $Process.WaitForExit(15000)
}

# Review 3: after a TIMEOUT only the host Maestro tree was killed; the driver app on the phone and the adb forwards this run made stay and break the next run. Stops the driver and removes those forwards only.
# Check4 1 + 2: a forward is removed only when it is absent from a before-list that was read; when either list could not be read, no forward is removed (WARN).
function Stop-MaestroLeftovers([string]$Phone, $ForwardsBefore) {
    Invoke-AdbCleanup $Phone @('shell', 'am', 'force-stop', $script:MaestroDriverPackage) 'stop the Maestro driver'
    $after = $null
    if ($null -ne $ForwardsBefore) { $after = Get-AdbForwardLines $Phone }
    if ($null -eq $ForwardsBefore -or $null -eq $after) { Write-Host '   cleanup: WARN no adb forward removed: the forward list could not be read before or after the flow'; return }
    foreach ($spec in (Get-NewForwardSpecs $ForwardsBefore $after $Phone)) {
        Invoke-AdbCleanup $Phone @('forward', '--remove', $spec) "remove the adb forward $spec"
    }
}

# Review check3 4: one cleanup adb call; an adb timeout (exception) or a non-zero exit prints a "cleanup:" WARN line, since Invoke-Adb never throws on a failed command.
function Invoke-AdbCleanup([string]$Phone, [string[]]$Arguments, [string]$What) {
    try { $out = Invoke-Adb $Phone $Arguments }
    catch { Write-Host "   cleanup: WARN could not ${What}: $($_.Exception.Message)"; return }
    if ($LASTEXITCODE -ne 0) { Write-Host "   cleanup: WARN could not ${What}: adb exit $LASTEXITCODE $(Get-LastLines ($out -join "`n") 2)" }
}

# Review 5: a hard kill leaves the .phone-run-<name>.yaml copy of a flow in the flow folder, where a folder-wide run or a commit would find it. Copies older than any run's budget (120 min max) are deleted at the start.
function Remove-StaleRunCopies {
    $limit = (Get-Date).AddMinutes(-150)
    Get-ChildItem -LiteralPath $script:FlowDir -Filter '.phone-run-*.yaml' -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -lt $limit } | Remove-Item -Force -ErrorAction SilentlyContinue
}

# Review 3: a flow that declares the RTST package without .dbg runs as a copy (same folder, so relative runFlow paths hold) whose appId is the phone's dev package; any other flow runs as is.
function New-RunCopyWhenNeeded([System.IO.FileInfo]$File) {
    $text = Get-Content -Raw $File.FullName
    if ((Get-FlowAppId $text) -eq $script:Package) { return $File }
    $copy = Join-Path $File.DirectoryName ".phone-run-$($File.Name)"
    [IO.File]::WriteAllText($copy, (Set-FlowAppId $text $script:Package), (New-Object Text.UTF8Encoding($false)))
    return Get-Item -LiteralPath $copy
}

# Starts Maestro for one flow and waits at most $Seconds. Returns ExitCode (-1 when killed at the budget), TimedOut, and both output streams.
function Invoke-PhoneMaestro([string]$Maestro, [string]$Phone, [System.IO.FileInfo]$File, [int]$Seconds) {
    $out = Join-Path $script:MaestroOut "phone-$($File.BaseName).out"
    $err = Join-Path $script:MaestroOut "phone-$($File.BaseName).err"
    $log = Join-Path $script:MaestroOut "phone-$($File.BaseName).log"
    $debugDir = Join-Path $script:MaestroOut "debug-$($File.BaseName)"   # check4: one --debug-output folder per flow (maestro.log, commands, screenshots), never shared
    $null = New-Item -ItemType Directory -Force $debugDir
    $runFile = New-RunCopyWhenNeeded $File
    $argLine = Get-MaestroFlowArguments $Phone $debugDir $runFile.FullName
    $oldJavaOptions = $env:JAVA_TOOL_OPTIONS   # review 4: the caller's value comes back after the flow
    $forwardsBefore = Get-AdbForwardLines $Phone
    $env:JAVA_TOOL_OPTIONS = "-Duser.home=$(Get-SpaceFreePath $script:MaestroHome)"   # review 4: as MAESTRO_OPTS, a space in the path would split the option
    try {
        $p = Start-Process -FilePath $Maestro -ArgumentList $argLine -NoNewWindow -PassThru -RedirectStandardOutput $out -RedirectStandardError $err
        $null = $p.Handle   # review 2: a .bat wrapper's handle is cached only when read, else ExitCode can be $null (tools/emulator/run-flows.ps1 does the same)
        $finished = $p.WaitForExit($Seconds * 1000)
        if (-not $finished) { Stop-MaestroTree $p }
        if (-not $finished -or $p.ExitCode -ne 0) { Stop-MaestroLeftovers $Phone $forwardsBefore }   # review check2 3: a failed flow leaves the driver and forwards too, not only a timeout
    } finally {
        if ($null -eq $oldJavaOptions) { Remove-Item Env:JAVA_TOOL_OPTIONS -ErrorAction SilentlyContinue } else { $env:JAVA_TOOL_OPTIONS = $oldJavaOptions }
        if ($runFile.FullName -ne $File.FullName) { Remove-Item -LiteralPath $runFile.FullName -ErrorAction SilentlyContinue }
    }
    $text = ((Get-Content $out -Raw -ErrorAction SilentlyContinue), (Get-Content $err -Raw -ErrorAction SilentlyContinue)) -join "`n"
    [IO.File]::WriteAllText($log, $text, (New-Object Text.UTF8Encoding($false)))   # check4: the whole Maestro stdout and stderr of this flow, kept for the failure line to point at
    return [pscustomobject]@{ ExitCode = $(if ($finished) { $p.ExitCode } else { -1 }); TimedOut = (-not $finished); Text = $text; LogPath = $log; DebugDir = $debugDir }
}

# Review 4: the RTST-TEST lines are logged asynchronously, so the markers are polled until all are found or the grace (at most $script:LogGraceSeconds, never past the budget) is over.
function Get-MissingMarkers([string]$Phone, [System.IO.FileInfo]$File) {
    $expected = Get-FlowExpectedLogs (Get-Content -Raw $File.FullName)
    if ($expected.Count -eq 0) { return @() }
    $graceEnd = (Get-Date).AddSeconds([math]::Min($script:LogGraceSeconds, (Get-FlowSecondsLeft $script:RunStart (Get-Date) $Budget)))
    while ($true) {
        $log = Join-PmLines (Invoke-Adb $Phone @('logcat', '-d', '-s', $script:LogTag))
        $missing = @($expected | Where-Object { -not (Test-LogMarker $log $_) })
        if ($missing.Count -eq 0 -or (Get-Date) -ge $graceEnd) { return $missing }
        Start-Sleep -Seconds 1
    }
}

function Get-LastLines([string]$Text, [int]$Count) {
    return (@($Text -split "`r?`n" | Where-Object { $_.Trim() }) | Select-Object -Last $Count) -join ' | '
}

# One flow: logcat cleared, Maestro, the verdict. The result is a PASS / FAIL / TIMEOUT line and its outcome word.
function Invoke-OneFlow([string]$Maestro, [string]$Phone, $Item) {
    $left = Get-FlowSecondsLeft $script:RunStart (Get-Date) $Budget
    if ($left -le 0) { return [pscustomobject]@{ Outcome = 'SKIP'; Line = (Format-FlowResultLine 'SKIP' $Item.Name 0 'the budget is used up before it started') } }
    Invoke-Adb $Phone @('logcat', '-b', 'main', '-c') | Out-Null   # review 6: only the main buffer; crash and system hold other sessions' evidence
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $run = Invoke-PhoneMaestro $Maestro $Phone $Item.File $left
    $seconds = $sw.Elapsed.TotalSeconds
    return (Get-FlowVerdict $Phone $Item $run $seconds)
}

# The verdict of a finished Maestro run: timeout, no exit code, exit code, then the expected log markers.
function Get-FlowVerdict([string]$Phone, $Item, $run, [double]$seconds) {
    if ($run.TimedOut) { return [pscustomobject]@{ Outcome = 'TIMEOUT'; Line = (Format-FlowResultLine 'TIMEOUT' $Item.Name $seconds "stopped at the $Budget-minute budget") } }
    if ($null -eq $run.ExitCode) { return [pscustomobject]@{ Outcome = 'FAIL'; Line = (Format-FlowResultLine 'FAIL' $Item.Name $seconds 'runner problem: Maestro finished but Windows gave no exit code; the verdict is unknown') } }
    if ($run.ExitCode -ne 0) { return New-FailureResult $Item $seconds $run }
    $missing = @(Get-MissingMarkers $Phone $Item.File)
    if ($missing.Count -gt 0) { return [pscustomobject]@{ Outcome = 'FAIL'; Line = (Format-FlowResultLine 'FAIL' $Item.Name $seconds "missing log marker(s) under tag $($script:LogTag): $($missing -join '; ')") } }
    return [pscustomobject]@{ Outcome = 'PASS'; Line = (Format-FlowResultLine 'PASS' $Item.Name $seconds) }
}

function New-FailureResult($Item, [double]$Seconds, $Run) {
    $detail = "Maestro exit $($Run.ExitCode): $(Get-MaestroFailureLines $Run.Text 4) (full log: $($Run.LogPath); debug output: $($Run.DebugDir))"
    return [pscustomobject]@{ Outcome = 'FAIL'; Line = (Format-FlowResultLine 'FAIL' $Item.Name $Seconds $detail) }
}

function Initialize-MaestroState {
    $env:MAESTRO_CLI_NO_ANALYTICS = '1'
    $env:MAESTRO_CLI_NO_ANALYTICS_NOTIFICATION = '1'
    $script:MaestroOut = Join-Path $script:ToolsDir '.tools\maestro-out\phone'
    $script:MaestroHome = Join-Path $script:ToolsDir '.tools\maestro-home\phone'
    $null = New-Item -ItemType Directory -Force $script:MaestroOut, $script:MaestroHome
    $env:MAESTRO_OPTS = "-Duser.home=$(Get-SpaceFreePath $script:MaestroHome)"
    Remove-StaleRunCopies
}

# Review 5: an adb failure inside one flow (logcat clear, marker read: "Phone not responding") is that flow's FAIL line; the earlier results and the later flows stay.
function Invoke-OneFlowSafely([string]$Maestro, [string]$Phone, $Item) {
    try { return (Invoke-OneFlow $Maestro $Phone $Item) }
    catch { return [pscustomobject]@{ Outcome = 'FAIL'; Line = (Format-FlowResultLine 'FAIL' $Item.Name 0 "runner problem: $($_.Exception.Message)") } }
}

# Review check2 2 + check3 1: the flows' setOrientation changes persistent system settings (auto-rotate, fixed rotation). Reads them before the flows.
# One behaviour: rotation is touched only when BOTH reads worked. Saved is key -> digits, or $null for a key the phone says is unset ("null"). When either read fails, Saved is empty
# (nothing is ever written or deleted) and Readable is $false, so a flow that changes orientation is SKIPPED.
function Save-RotationSettings([string]$Phone) {
    $saved = @{}
    $readable = $true
    foreach ($key in $script:RotationSettings) {
        try {
            $out = Invoke-Adb $Phone @('shell', 'settings', 'get', 'system', $key)
            $reading = Get-SettingReading ($out -join "`n") $LASTEXITCODE
        } catch { $reading = [pscustomobject]@{ State = 'unreadable'; Value = $null }; Write-Host "   rotation: could not read ${key}: $($_.Exception.Message)" }
        if ($reading.State -eq 'unreadable') { $readable = $false; Write-Host "   rotation: could not read $key; flows that change orientation are skipped and no rotation setting is touched" }
        else { $saved[$key] = $reading.Value }
    }
    if (-not $readable) { $saved = @{} }
    return [pscustomobject]@{ Saved = $saved; Readable = $readable }
}

# Puts the saved rotation settings back; each command on its own, so one adb failure never stops the rest or the run's cleanup. Returns $true when every command worked;
# a failed one prints `WARN rotation not restored: <key>=<value>`.
function Restore-RotationSettings([string]$Phone, $Saved) {
    $allRestored = $true
    foreach ($command in (Get-RotationRestoreCommands $Saved)) {
        $failed = $false
        try { Invoke-Adb $Phone $command | Out-Null; $failed = ($LASTEXITCODE -ne 0) } catch { $failed = $true }
        if ($failed) { $allRestored = $false; Write-Host (Format-RotationWarn $command) }
    }
    return $allRestored
}

# Runs the planned flows on the leased phone; returns every outcome word. The phone must already be leased and ready.
function Invoke-PlannedFlows([string]$Maestro, [string]$Phone, $Plan, $Rotation = [pscustomobject]@{ Saved = @{}; Readable = $true }) {
    $outcomes = New-Object System.Collections.Generic.List[string]
    foreach ($item in $Plan) {
        if (-not $item.Refusal -and -not $Rotation.Readable -and (Test-FlowChangesOrientation (Get-Content -Raw $item.File.FullName) $item.File.FullName)) {
            $item = [pscustomobject]@{ Name = $item.Name; File = $item.File; Refusal = 'the phone rotation settings could not be read, and this flow changes the orientation (nothing would be put back)' }
        }
        $result = if ($item.Refusal) { [pscustomobject]@{ Outcome = 'SKIP'; Line = (Format-FlowResultLine 'SKIP' $item.Name 0 $item.Refusal) } } else { Invoke-OneFlowSafely $Maestro $Phone $item }
        if (-not $item.Refusal -and -not (Restore-RotationSettings $Phone $Rotation.Saved)) { $script:RotationNotRestored = $true }   # after every outcome: the next flow starts from the phone's own rotation
        Write-Host $result.Line
        $outcomes.Add($result.Outcome)
    }
    return @($outcomes)
}

function Write-RunSummary([string[]]$Outcomes, [int]$Code) {
    $counts = ($Outcomes | Group-Object | ForEach-Object { "$($_.Count) $($_.Name)" }) -join ', '
    Write-Host ("SUMMARY {0} flows on the phone in {1:n0} s: {2}; exit {3}" -f $Outcomes.Count, ((Get-Date) - $script:RunStart).TotalSeconds, $counts, $Code)
}

# Takes the phone lease for the whole run. Returns the stage (Key, Owner, Mine) or $null when someone else holds the phone.
function Enter-PhoneStage([string]$Phone) {
    $key = Get-PhoneLeaseKey $Phone
    $owner = Get-RunChecksOwner $key
    $held = Read-Lease $key
    $mine = [bool]($held -and $held.Owner -ceq $owner)
    $heldBy = Wait-ForLease $key $owner ($Budget + $script:LeaseExtraMinutes) $script:LeaseWaitMinutes 'phone'
    if ($heldBy) { Write-Host "The phone is leased by $heldBy; waited $($script:LeaseWaitMinutes) min. Not touching it."; return $null }
    $saveFailed = $false   # review 2: when the setting cannot be recorded it is never changed (preflight -Fix and keep-awake would leave stay-on on the phone)
    if (-not $mine) { try { Save-StayOnForLease $Phone $key $owner | Out-Null } catch { $saveFailed = $true; Write-Host "NOT READY: could not record the screen timeout, so it is left unchanged: $($_.Exception.Message)" } }
    return [pscustomobject]@{ Key = $key; Owner = $owner; Mine = $mine; PreviousOwner = $env:RTST_PHONE_OWNER; SaveFailed = $saveFailed }
}

function Exit-PhoneStage([string]$Phone, $Stage) {
    try { Invoke-Adb $Phone @('shell', 'am', 'force-stop', $script:Package) | Out-Null } catch { Write-Host "   cleanup: could not force-stop the dev app: $($_.Exception.Message)" }
    $env:RTST_PHONE_OWNER = $Stage.PreviousOwner
    if ($Stage.Mine) { return }   # a lease the caller held is theirs to release
    try { Close-PhoneLease $Phone $Stage.Key $Stage.Owner | Out-Null } catch { Write-Host "   cleanup: could not release the lease: $($_.Exception.Message)" }
}

# The part of a run that holds the lease: preflight, the dev app check, the flows. Returns the exit code.
function Invoke-LeasedRun([string]$Maestro, [string]$Phone, $Plan, $Stage) {
    if ($Stage.SaveFailed) { return $script:ExitNotReady }
    if (-not (Invoke-PreflightStep $Phone)) { return $script:ExitNotReady }
    $problem = Get-DevAppProblem $Phone
    if ($problem) { Write-Host "NOT READY: $problem"; return $script:ExitNotReady }
    $awake = $null
    $rotation = $null
    $finalRestoreDone = $false
    try {
        $awake = Start-KeepAwake $Phone   # check5 2: inside the try, so the finally always stops it (adb dropping right after it started must not leave stay-awake on)
        $script:RunStart = Get-Date   # review 1: the budget runs from the first flow; install, the lease wait and preflight keep their own caps
        $script:RotationNotRestored = $false
        $rotation = Save-RotationSettings $Phone
        $outcomes = Invoke-PlannedFlows $Maestro $Phone $Plan $rotation
        $finalRestoreDone = $true   # check4 5: the final restore runs before the exit code is computed, so a failure of it counts
        if (-not (Restore-RotationSettings $Phone $rotation.Saved)) { $script:RotationNotRestored = $true; Write-Host 'WARN rotation restore failed at the end of the run' }
        $code = Get-RunExitCodeAfterRestore (Get-FlowRunExitCode $outcomes) $script:RotationNotRestored
        Write-RunSummary $outcomes $code
        return $code
    } finally {
        if (-not $finalRestoreDone -and $rotation -and -not (Restore-RotationSettings $Phone $rotation.Saved)) { Write-Host 'WARN rotation restore failed at the end of the run' }   # a run that threw
        Stop-KeepAwake $awake
    }
}

function Start-PhoneRun($Plan) {
    $maestro = Find-MaestroLauncher
    if (-not (Set-ProcessJavaHome)) { return $script:ExitBroke }
    if (-not (Invoke-FlowLint $Plan)) { Write-Host 'lint-flows failed: fix the flows first.'; return $script:ExitBroke }
    $phone = Find-Phone $script:RequestedSerial
    if (-not $phone) { Write-PairingAsk $script:RequestedSerial; return $script:ExitNoPhone }
    if ((Get-DeviceKind $phone) -eq $script:KindEmulator) { Write-Host 'This runner is for the real phone; the emulator has tools/emulator/run-flows.ps1.'; return $script:ExitNoPhone }
    Initialize-MaestroState
    if ($Install -and -not (Invoke-InstallStep $phone)) { Write-Host 'INSTALL FAILED: see the lines above.'; return $script:ExitBroke }
    return (Invoke-MarkedStage $maestro $phone $Plan)
}

# Review 3: the run marker run-checks.ps1 writes (Slice 221), same key and owner, so two runs on one phone never overlap (exit 4). Taken after -Install, whose run-checks -Prepare writes its own.
function Invoke-MarkedStage([string]$Maestro, [string]$Phone, $Plan) {
    $key = Get-PhoneLeaseKey $Phone
    $owner = Get-RunChecksOwner $key
    $refusal = Request-RunMarker $key $owner $Force.IsPresent
    if ($refusal) { Write-Host $refusal; return $script:ExitLeaseBusy }
    try {
        $stage = Enter-PhoneStage $Phone
        if (-not $stage) { return $script:ExitLeaseBusy }
        $env:RTST_PHONE_OWNER = $stage.Owner
        try { return (Invoke-LeasedRun $Maestro $Phone $Plan $stage) }
        finally { Exit-PhoneStage $Phone $stage }
    } finally { Remove-RunMarker $key $owner }
}

$plan = @(New-FlowPlan)
if ($plan.Count -eq 0) { Write-Host 'No flow named: nothing to run.'; exit 3 }   # review 6: never green with nothing run
if ($DryRun) { Write-DryRun $plan; exit 0 }
try { $exitCode = Start-PhoneRun $plan }
catch { Write-Host "RUNNER ERROR: $($_.Exception.Message)"; $exitCode = $script:ExitBroke }
exit $exitCode
