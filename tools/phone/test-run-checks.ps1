<#
.SYNOPSIS
  Self-test for the pure logic of run-checks.ps1 (checks/_lib.ps1, Slice 213): check selection and skip reasons, the time budget, outcomes and exit codes, the summary,
  the preflight verdict, and the parsers of the four checks (cold-start numbers, highlight markers, notification text, wake locks).
  Run it in both `powershell` (5.1) and `pwsh`. Needs no device and touches no adb.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'checks\_lib.ps1')

$failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

# ---- selection and skip reasons
$catalogue = $script:Catalogue
$plan = @(Resolve-CheckSelection @('cold-start', 'book-end', 'paused-notification', 'cold-start', 'highlight') 'rtst' $catalogue)
Assert-Equal 'duplicates dropped' 4 $plan.Count
Assert-Equal 'order kept' 'cold-start,book-end,paused-notification,highlight' (($plan | ForEach-Object { $_.Name }) -join ',')
Assert-Equal 'cold-start runs on rtst' $script:StatusPending $plan[0].Status
Assert-Equal 'book-end is Dino only' $script:StatusSkipped $plan[1].Status
Assert-Equal 'book-end reason names the app' $true ($plan[1].Reason -like 'not for rtst*')
Assert-Equal 'paused-notification is built (Slice 208) and runs on rtst' $script:StatusPending $plan[2].Status
$threw = $false
try { Resolve-CheckSelection @('nope') 'rtst' $catalogue | Out-Null } catch { $threw = $_.Exception.Message -like "*Unknown check 'nope'*cold-start*" }
Assert-Equal 'unknown check throws and lists the known ones' $true $threw
Assert-Equal 'dino runs book-end' $script:StatusPending (@(Resolve-CheckSelection @('book-end') 'dino' $catalogue))[0].Status
# Slice 209: the hook checks are in the catalogue, each with its script file, and run on the apps they are for.
foreach ($hookCheck in 'language', 'voice', 'translate', 'book-update') { Assert-Equal "check script exists: $hookCheck" $true (Test-Path (Join-Path $PSScriptRoot "checks\$hookCheck.ps1")) }
Assert-Equal 'language runs on rtst' $script:StatusPending (@(Resolve-CheckSelection @('language') 'rtst' $catalogue))[0].Status
Assert-Equal 'voice is RTST only' $script:StatusSkipped (@(Resolve-CheckSelection @('voice') 'dino' $catalogue))[0].Status
Assert-Equal 'book-update is Dino only' $script:StatusSkipped (@(Resolve-CheckSelection @('book-update') 'rtst' $catalogue))[0].Status

Assert-Equal 'cold-start estimate scales with -Runs (3)' 240 (@(Resolve-CheckSelection @('cold-start') 'rtst' $catalogue 3))[0].EstimateSeconds
Assert-Equal 'cold-start estimate scales with -Runs (6)' 480 (@(Resolve-CheckSelection @('cold-start') 'rtst' $catalogue 6))[0].EstimateSeconds
Assert-Equal 'a fixed check keeps its estimate' 120 (@(Resolve-CheckSelection @('highlight') 'rtst' $catalogue 6))[0].EstimateSeconds

# ---- build cache name and the skip text
Assert-Equal 'aot platform' 'android-dbg-arm64-aot' (Get-CachePlatform 'aot')
Assert-Equal 'debug platform' 'android-dbg-arm64' (Get-CachePlatform 'debug')
Assert-Equal 'every check declares its variant (only cold-start is aot)' 'cold-start' ((@($catalogue.Keys | Where-Object { $catalogue[$_].Variant -eq 'aot' })) -join ',')
Assert-Equal 'no check has a variant other than aot or debug' 0 @($catalogue.Keys | Where-Object { $catalogue[$_].Variant -notin 'aot', 'debug' }).Count
Assert-Equal 'cold-start needs aot' 'aot' $plan[0].Variant
Assert-Equal 'highlight takes any build' 'debug' (@(Resolve-CheckSelection @('highlight') 'rtst' $catalogue))[0].Variant

# ---- the budget: a check starts only when its estimate fits what is left
$fits = Get-BudgetDecision 240 0 10
Assert-Equal 'fits at the start' $true $fits.Run
Assert-Equal 'left at the start' 600 $fits.LeftSeconds
$late = Get-BudgetDecision 240 400 10
Assert-Equal 'does not fit after 400 s' $false $late.Run
Assert-Equal 'budget reason' 'budget: needs ~240 s, 200 s left of 10 min' $late.Reason
Assert-Equal 'fits exactly' $true (Get-BudgetDecision 200 400 10).Run
Assert-Equal 'nothing left' $false (Get-BudgetDecision 1 600 10).Run
Assert-Equal 'over budget reports 0 left' 0 (Get-BudgetDecision 60 700 10).LeftSeconds

# ---- the deadline a check's waits honour
Set-CheckDeadline (Get-Date).AddSeconds(60)
Assert-Equal 'before the deadline nothing throws' $true (-not (& { try { Assert-WithinBudget 'x'; $false } catch { $true } }))
Set-CheckDeadline (Get-Date).AddSeconds(-1)
$message = & { try { Assert-WithinBudget 'waiting for audio'; '' } catch { $_.Exception.Message } }
Assert-Equal 'past the deadline throws a BUDGET error' $true ($message -like 'BUDGET: *waiting for audio*')
Set-CheckDeadline $null
Assert-Equal 'no deadline, no throw' '' (& { try { Assert-WithinBudget 'x'; '' } catch { 'threw' } })

# ---- the deadline reaches child scripts, a call past it is a TIMEOUT, steps take what is left
$when = (Get-Date).AddSeconds(90)
Set-CheckDeadline $when
Assert-Equal 'the deadline is passed down in the environment' $true ([bool][Environment]::GetEnvironmentVariable($script:DeadlineEnvVariable))
Remove-Variable -Name $script:DeadlineVariable -Scope Global
Assert-Equal 'a child reads it back from the environment' 0 ([math]::Round(([datetime](Get-ActiveDeadline) - $when).TotalSeconds))
Set-CheckDeadline $null
Assert-Equal 'cleared in both places' $true ($null -eq (Get-ActiveDeadline))
Assert-Equal 'garbage text is no deadline' $true ($null -eq (ConvertFrom-DeadlineText 'nonsense'))
$nowStamp = Get-Date
Assert-Equal 'past the deadline' $true (Test-PastDeadline $nowStamp.AddSeconds(-1) $nowStamp)
Assert-Equal 'before the deadline' $false (Test-PastDeadline $nowStamp.AddSeconds(5) $nowStamp)
Assert-Equal 'no deadline is never past' $false (Test-PastDeadline $null $nowStamp)
Assert-Equal 'a late failure becomes TIMEOUT' 'TIMEOUT' (Resolve-CheckOutcome @() 'Phone not responding over adb' $true).Status
Assert-Equal 'an early failure stays FAIL' 'FAIL' (Resolve-CheckOutcome @() 'Phone not responding over adb' $false).Status
Assert-Equal 'step keeps its wish when time is plenty' 300 (Get-StepTimeout 300 500 30)
Assert-Equal 'step is capped to what is left' 120 (Get-StepTimeout 900 120.7 30)
Assert-Equal 'step is skipped when too little is left' $true ($null -eq (Get-StepTimeout 300 20 30))
Assert-Equal 'the lease is ours' $true (Test-OwnsLease ([pscustomobject]@{ Owner = 'a' }) 'a')
Assert-Equal 'another owner' $false (Test-OwnsLease ([pscustomobject]@{ Owner = 'b' }) 'a')
Assert-Equal 'no lease record' $false (Test-OwnsLease $null 'a')

# ---- outcomes, summary, exit codes
$pass = Resolve-CheckOutcome @('progress text', (New-CheckResult $true 'ok 3 runs')) ''
Assert-Equal 'pass' 'PASS' $pass.Status
Assert-Equal 'pass detail' 'ok 3 runs' $pass.Detail
Assert-Equal 'a failed result' 'FAIL' (Resolve-CheckOutcome @((New-CheckResult $false 'no marker')) '').Status
Assert-Equal 'no result is a FAIL' 'FAIL' (Resolve-CheckOutcome @('only text') '').Status
Assert-Equal 'a throw is a FAIL' 'FAIL' (Resolve-CheckOutcome @() 'adb timed out').Status
$timeout = Resolve-CheckOutcome @() 'BUDGET: the time budget ran out while waiting.'
Assert-Equal 'a budget throw is a TIMEOUT' 'TIMEOUT' $timeout.Status
Assert-Equal 'timeout detail drops the prefix' 'the time budget ran out while waiting.' $timeout.Detail
$measuredOutcome = Resolve-CheckOutcome @((New-CheckResult $true 'median 1200 ms' $true)) ''
Assert-Equal 'a measured result is MEASURED, never PASS' 'MEASURED' $measuredOutcome.Status
Assert-Equal 'measured alone is exit 0' 0 (Get-RunExitCode @([pscustomobject]@{ Status = 'MEASURED' }))
Assert-Equal 'measured plus a skip is exit 3' 3 (Get-RunExitCode @([pscustomobject]@{ Status = 'MEASURED' }, [pscustomobject]@{ Status = 'SKIP' }))
$rows = @(
    [pscustomobject]@{ Name = 'cold-start'; Status = 'PASS'; Detail = 'median 1200 ms'; Seconds = 200; Variant = 'aot' },
    [pscustomobject]@{ Name = 'book-end'; Status = 'SKIP'; Detail = 'budget: needs ~420 s, 100 s left of 10 min'; Seconds = $null })
$summary = @(Format-CheckSummary $rows)
Assert-Equal 'summary has a header and a row per check' 3 $summary.Count
Assert-Equal 'a timing row carries its variant' $true ($summary[1] -like 'cold-start [[]aot[]]*')
Assert-Equal 'summary names the skip reason' $true ($summary[2] -like '*book-end*SKIP*budget: needs*')
Assert-Equal 'passes and skips: exit 3' 3 (Get-RunExitCode $rows)
Assert-Equal 'all passed: exit 0' 0 (Get-RunExitCode @($rows[0]))
Assert-Equal 'a failure wins over a skip' 1 (Get-RunExitCode @($rows[1], [pscustomobject]@{ Status = 'FAIL' }))
Assert-Equal 'a timeout is exit 1' 1 (Get-RunExitCode @([pscustomobject]@{ Status = 'TIMEOUT' }))

# ---- the live wake-lock block, not the history
$s21 = @('Wake Locks: size=0', '', '  Wakelock history:', "  [0] 2026-10-30 10:00:00 SCREEN_BRIGHT_WAKE_LOCK 'WindowManager/displayId:0' (uid=1000 pid=900 ws=WorkSource{10460}) DISABLED", "  [1] 2026-10-30 09:00:00 PARTIAL_WAKE_LOCK 'MediaPlayer' (uid=10460 pid=4242) DISABLED") -join "`n"
Assert-Equal 'size=0 with history entries: nothing held' $false (Test-AppHoldsWake $s21 '' '10460')
Assert-Equal 'no live locks parsed' 0 @(Get-LiveWakeLocks $s21).Count
$live = @('Wake Locks: size=2', "  PARTIAL_WAKE_LOCK 'AudioMix' ACQ=-5s (uid=1013 pid=900)", "  SCREEN_BRIGHT_WAKE_LOCK 'WindowManager/displayId:0' ACQ=-1s (uid=1000 pid=900 ws=WorkSource{10460})", '', '  Wakelock history:', '  [0] 1 PARTIAL_WAKE_LOCK x (uid=10460) DISABLED') -join "`n"
Assert-Equal 'two live locks parsed, the block ends at the blank line' 2 @(Get-LiveWakeLocks $live).Count
Assert-Equal 'screen-bright lock of uid 1000 with the app in its worksource holds' $true (Test-AppHoldsWake $live '' '10460')
Assert-Equal 'another app is not held by it' $false (Test-AppHoldsWake $live '' '10999')
Assert-Equal 'a lock that only the history lists is not held' $false (Test-AppHoldsWake ($live -replace 'ws=WorkSource\{10460\}', 'ws=WorkSource{10001}') '' '10460')
Assert-Equal 'a foreground service still holds' $true (Test-AppHoldsWake $s21 "  ServiceRecord{1 u0 x/.S}`n    isForeground=true" '10460')

# ---- preflight: GO only on an explicit `PREFLIGHT GO` last line; a NO-GO whose FAILs are all non-fatal is a warning; anything else stops
$goLines = @('Preflight for x', '  [OK] connection: wireless adb', 'PREFLIGHT GO')
Assert-Equal 'an explicit GO line (exit 0) is go' $true (Resolve-PreflightVerdict $goLines 0).Go
Assert-Equal 'GO with a non-zero exit is not go' $false (Resolve-PreflightVerdict $goLines 1).Go
Assert-Equal 'no verdict line at all is NO-GO' $false (Resolve-PreflightVerdict @('Preflight for x', '  [OK] connection: wireless adb') 0).Go
Assert-Equal 'nothing printed is NO-GO' $false (Resolve-PreflightVerdict @() 1).Go
Assert-Equal 'an adb error NO-GO with no FAIL line is NO-GO' $false (Resolve-PreflightVerdict @('adb failed: x', 'PREFLIGHT NO-GO: adb: x') 1).Go
Assert-Equal 'the pairing ask (exit 2) is NO-GO' $false (Resolve-PreflightVerdict @('Phone not paired: run connect.ps1') 2).Go
Assert-Equal 'a NO-GO line followed by more output is not a verdict' $false (Resolve-PreflightVerdict @('  [FAIL] stay awake: x', 'PREFLIGHT NO-GO: stay awake: x', 'trailing text') 1).Go
$preflight = @('Preflight for x', '  [OK] connection: wireless adb', '  [FAIL] stay awake: stay_on_while_plugged_in = 0 does not cover this charger', '  [FAIL] rtst dev app: uk.sirous.readthestupidtext.dbg is not installed',
    '  [FAIL] phone lease: held by someone', 'PREFLIGHT NO-GO: several')
$verdict = Resolve-PreflightVerdict $preflight 1
Assert-Equal 'non-fatal FAILs behind an explicit NO-GO do not stop the run' $true $verdict.Go
Assert-Equal 'they are listed as warnings' 3 $verdict.Other.Count
$noGo = Resolve-PreflightVerdict @('  [FAIL] charging: ON BATTERY (Awake): plug the phone in', '  [FAIL] screen lock: a PIN is set', '  [FAIL] stay awake: x', 'PREFLIGHT NO-GO: x') 1
Assert-Equal 'charging and lock stop it' $false $noGo.Go
Assert-Equal 'both are named' 2 $noGo.Fatal.Count
Assert-Equal 'a read failure on the lock stops it' $false (Resolve-PreflightVerdict @('  [FAIL] screen lock: could not read the lock state positively', 'PREFLIGHT NO-GO: x') 1).Go
Assert-Equal 'a NO-GO with no usable reason names the missing verdict' $true ((Resolve-PreflightVerdict @() 1).Fatal[0] -like 'no usable verdict*')

# ---- waits before the checks count against the budget and are capped at 3 minutes
$t0 = Get-Date
Assert-Equal 'a fresh run may wait the cap' 3 (Get-PreWaitMinutes $t0 $t0 10)
Assert-Equal 'later, what is left of the budget' 2 (Get-PreWaitMinutes $t0 $t0.AddMinutes(8) 10)
Assert-Equal 'nothing left: no wait' 0 (Get-PreWaitMinutes $t0 $t0.AddMinutes(12) 10)

$tWait = $t0.AddSeconds(660)
Assert-Equal 'review: a build wait moves the budget start (finding 2)' 3 (Get-PreWaitMinutes (Get-BudgetStart $t0 660) $tWait 10)
Assert-Equal 'review: without the shift the wait would be 0 (finding 2)' 0 (Get-PreWaitMinutes $t0 $tWait 10)
Assert-Equal 'review: the budget end moves by the waited seconds (finding 1)' $t0.AddMinutes(10).AddSeconds(660) (Get-BudgetStart $t0 660).AddMinutes(10)
# one budget model (final medium review 1 and 6): a -Prepare build, a lease wait and a quiet wait all measure from the same start
$prepClock = New-BudgetClock $t0.AddMinutes(20)   # the build took 20 minutes; -Prepare restarts the pair together
$tLease = $t0.AddMinutes(21)
Assert-Equal 'budget: a fresh clock has no waited seconds' 0 $prepClock.WaitedSeconds
Assert-Equal 'budget: after a -Prepare build the lease wait gets the cap' 3 (Get-BudgetPreWaitMinutes $prepClock.RunStart $prepClock.WaitedSeconds $tLease 10)
Assert-Equal 'budget: a quiet wait 9 minutes into the same clock has 1 minute left' 1 (Get-BudgetPreWaitMinutes $prepClock.RunStart $prepClock.WaitedSeconds $prepClock.RunStart.AddMinutes(9) 10)
Assert-Equal 'budget: elapsed after the reset is only the time since it' 60 (Get-BudgetElapsedSecondsAt $prepClock.RunStart $prepClock.WaitedSeconds $tLease)
# a build wait of 660 s (not -Prepare): the lease wait is not capped to 0 by the raw run start
Assert-Equal 'budget: a lease wait after a long build wait is not 0' 3 (Get-BudgetPreWaitMinutes $t0 660 $tWait 10)
Assert-Equal 'budget: elapsed excludes the waited seconds' 0 (Get-BudgetElapsedSecondsAt $t0 660 $tWait)
Assert-Equal 'review: a lost lease is exit 4, not 7 (finding 5)' 4 (Get-StageErrorExitCode 'lost' 'lost')
Assert-Equal 'review: another error is a runner error (finding 5)' 7 (Get-StageErrorExitCode 'adb dropped' 'lost')

# ---- every early stop still names every check
$twoPlans = @([pscustomobject]@{ Name = 'cold-start'; Status = 'Pending'; Reason = $null }, [pscustomobject]@{ Name = 'book-end'; Status = 'SKIP'; Reason = 'not for rtst (runs on: dino)' }, [pscustomobject]@{ Name = 'highlight'; Status = 'Pending'; Reason = $null })
$done = @([pscustomobject]@{ Name = 'cold-start'; Status = 'PASS'; Detail = 'ok'; Seconds = 90 })
$final = @(Get-FinalRows $twoPlans $done 'runner error: adb dropped')
Assert-Equal 'one row per plan' 3 $final.Count
Assert-Equal 'a row that ran is kept' 'PASS' $final[0].Status
Assert-Equal 'a planned skip keeps its own reason' 'not for rtst (runs on: dino)' $final[1].Detail
Assert-Equal 'a check that did not run is SKIP with the reason' 'SKIP' $final[2].Status
Assert-Equal 'with the stop reason' 'runner error: adb dropped' $final[2].Detail
Assert-Equal 'no rows done: all listed' 3 @(Get-FinalRows $twoPlans @() 'waited too long for x').Count

# ---- the lease the caller holds is never shortened
Assert-Equal 'no lease: the wanted minutes' 25 (Get-LeaseMinutes 0 1000 25)
Assert-Equal 'a long own lease is kept' 120 (Get-LeaseMinutes (1000 + 120 * 60) 1000 25)
Assert-Equal 'a short own lease is extended to the wanted minutes' 25 (Get-LeaseMinutes (1000 + 5 * 60) 1000 25)
Assert-Equal 'an expired own lease: the wanted minutes' 25 (Get-LeaseMinutes 500 1000 25)
Assert-Equal 'never above the maximum lease' 1440 (Get-LeaseMinutes (1000 + 5000 * 60) 1000 25)

# ---- the installed dev app: another variant is never replaced silently
$aot = 'android-dbg-arm64-aot'; $plain = 'android-dbg-arm64'
$marker = ConvertFrom-InstallMarker (ConvertTo-InstallMarker $aot 'abc123' '2026-10-30 10:11:12')
Assert-Equal 'marker platform' $aot $marker.Platform
Assert-Equal 'marker key' 'abc123' $marker.Key
Assert-Equal 'marker update time' '2026-10-30_10:11:12' $marker.Updated
Assert-Equal 'garbage is no marker' $null (ConvertFrom-InstallMarker 'cat: no such file')
Assert-Equal 'package update time from dumpsys' '2026-10-30_10:11:12' (Get-PackageLastUpdate "    firstInstallTime=2026-10-01 09:00:00`n    lastUpdateTime=2026-10-30 10:11:12`n")
Assert-Equal 'not installed has no update time' $null (Get-PackageLastUpdate 'Unable to find package: x')
Assert-Equal 'not installed: install' 'Install' (Resolve-InstallPlan $null $null $aot 'abc123' $false).Action
Assert-Equal 'same build: keep' 'Keep' (Resolve-InstallPlan $marker '2026-10-30_10:11:12' $aot 'abc123' $false).Action
Assert-Equal 'same variant, new build: install over (data stays)' 'Install' (Resolve-InstallPlan $marker '2026-10-30_10:11:12' $aot 'def456' $false).Action
$swap = Resolve-InstallPlan $marker '2026-10-30_10:11:12' $plain 'abc123' $false
Assert-Equal 'another variant over one run-checks installed: install (the one swap; adb install -r keeps data)' 'Install' $swap.Action
Assert-Equal 'the swap says which variant it goes over' $true ($swap.Reason -like '*debug build over aot*')
Assert-Equal 'the phone variant is read from the marker' 'debug' (Get-VariantOfPlatform $plain)
Assert-Equal 'installed by someone else since (update time moved): skip' 'Skip' (Resolve-InstallPlan $marker '2026-10-30_11:00:00' $aot 'abc123' $false).Action
Assert-Equal 'no marker on an installed app: skip' 'Skip' (Resolve-InstallPlan $null '2026-10-30_11:00:00' $aot 'abc123' $false).Action
Assert-Equal 'no marker, -Reinstall: install' 'Install' (Resolve-InstallPlan $null '2026-10-30_11:00:00' $aot 'abc123' $true).Action

# ---- logcat is read incrementally
Assert-Equal 'cursor is the newest time' 1003.5 (Get-LogCursor @('1001.000 1 1 I a: x', '1003.500 1 1 I a: y', '1002.000 1 1 I a: z') 0)
Assert-Equal 'cursor never moves back' 1010 (Get-LogCursor @('1001.000 1 1 I a: x') 1010)
Assert-Equal 'no lines keep the cursor' 5 (Get-LogCursor @() 5)

# ---- book-end: proof the book played, and readable dumps
$played = @('1000.000  1  1 I RTST-TEST: HOOK open-book opened id=three-little-pigs', '1001.000  1  1 I RTST-TEST: RTST-TEST CHUNK 0', '1002.000  1  1 I RTST-TEST: RTST-TEST CHUNK 1', '1009.000  1  1 I RTST-TEST: RTST-TEST READ_END')
Assert-Equal 'a played book is proven' $true (Resolve-BookPlayProof $played).Played
Assert-Equal 'chunks counted' 2 (Resolve-BookPlayProof $played).Chunks
Assert-Equal 'READ_END alone is not play' $false (Resolve-BookPlayProof @('1000.000  1  1 I RTST-TEST: HOOK open-book opened id=x', '1001.000  1  1 I RTST-TEST: RTST-TEST READ_END')).Played
Assert-Equal 'one chunk is not enough' $false (Resolve-BookPlayProof @($played[0], $played[1], $played[3])).Played
Assert-Equal 'no open-book line is not play' $false (Resolve-BookPlayProof @($played[1], $played[2], $played[3])).Played
Assert-Equal 'no READ_END is not play' $false (Resolve-BookPlayProof @($played[0], $played[1], $played[2])).Played
Assert-Equal 'real-looking dumps are readable' $null (Get-WakeDumpProblem "Wake Locks: size=0`n mWakefulness=Awake" 'ACTIVITY MANAGER SERVICES (dumpsys activity services)')
Assert-Equal 'an empty power dump is a problem' $true ((Get-WakeDumpProblem '' 'ACTIVITY MANAGER SERVICES') -like '*power*')
Assert-Equal 'an error services dump is a problem' $true ((Get-WakeDumpProblem 'Wake Locks: size=0' 'Error: service not found') -like '*services*')

# ---- highlight: the editor holds exactly the typed text
$listed = @('On screen (text | id | clickable):', '  Read | ReadButton | True', '  The first sentence. The second. | ReviewEditor | True')
Assert-Equal 'editor holds the typed text' $true (Test-EditorHoldsText $listed 'ReviewEditor' 'The first sentence. The second.')
Assert-Equal 'an old draft in front is caught' $false (Test-EditorHoldsText $listed 'ReviewEditor' 'The first sentence.')
Assert-Equal 'no editor on screen' $false (Test-EditorHoldsText @('  Read | ReadButton | True') 'ReviewEditor' 'x')

# ---- short-lines (Slice 220): the probe line and the guard lines of one read
$probeLog = @(
    '10-30 12:00:00.100 I/x: retry take 2 (padded, voice kokoro:am_liam, 4 chars)',
    '10-30 12:00:00.200 W/x: silent voice output rescued by padded take 2 (voice kokoro:am_liam, 4 chars, voiced ms 0/480, needed 90, pace 1.00)',
    '10-30 12:00:00.300 D/x: short line probe (voice kokoro:am_liam, 4 chars): plain [samples 0, peak 0.000, voiced ms 0]; dots [samples 9600, peak 0.310, voiced ms 380]; commas [samples 24000, peak 0.001, voiced ms 0]')
$facts = ConvertFrom-ShortLineLog $probeLog
Assert-Equal 'plain take with zero samples is silent' $true ($facts.Probe.Plain.Silent -and $facts.Probe.Plain.Samples -eq 0)
Assert-Equal 'padded take voiced ms read' 380 $facts.Probe.Dots.VoicedMs
Assert-Equal 'all-zero commas take is silent but has samples' $true ($facts.Probe.Commas.Silent -and $facts.Probe.Commas.Samples -eq 24000)
Assert-Equal 'density is read when present' 0.35 (ConvertFrom-TakeFacts 'samples 10, peak 0.3, voiced ms 5, density 0.35').Density
Assert-Equal 'rescue by a padded take is named' 'padded take 2' $facts.Rescue
Assert-Equal 'retry kind is kept' 'True' "$($facts.Retries -contains '2:padded')"
Assert-Equal 'no give-up' $false $facts.GaveUp
Assert-Equal 'a gave up line is seen' $true (ConvertFrom-ShortLineLog @('x: silent voice output after 3 takes; gave up (voice kokoro:am_liam, 4 chars, voiced ms 0/0/0)')).GaveUp
Assert-Equal 'summary counts silent plain takes' $true ((Get-ShortLineSummary @([pscustomobject]@{ Probe = $facts.Probe; GaveUp = $false })) -like 'Plain silent 1/1 (zero samples 1); Dots silent 0/1*')

# ---- a deadline caps an adb call; none leaves it alone
$now = Get-Date
Assert-Equal 'no deadline: the call keeps its limit' 30 (Limit-SecondsToDeadline 30 $null $now)
Assert-Equal 'a near deadline caps the call' 12 (Limit-SecondsToDeadline 30 $now.AddSeconds(12) $now)
Assert-Equal 'a far deadline leaves the limit' 30 (Limit-SecondsToDeadline 30 $now.AddSeconds(300) $now)
Assert-Equal 'a passed deadline still gives a short call' 5 (Limit-SecondsToDeadline 30 $now.AddSeconds(-10) $now)

# ---- cold-start numbers from a saved log (the read hook at phone time 1000.0)
$events = @(ConvertFrom-TimingLog (Get-Content (Join-Path $PSScriptRoot 'fixtures/timing-log.txt')))
$numbers = Get-ColdStartNumbers $events 1000.0
Assert-Equal 'first audio = first CHUNK minus the hook' 2100 $numbers.FirstAudioMs
Assert-Equal 'engine ms' 480 $numbers.EngineMs
Assert-Equal 'first synthesis ms' 400 $numbers.FirstSynthMs
Assert-Equal 'no chunk yet: no first-audio number' $null (Get-ColdStartNumbers @() 1000.0).FirstAudioMs
# ---- Slice 214: first-read presets
Assert-Equal 'preset 1w is one word' 'Hello.' (Resolve-ColdStartText '1w' '').Text
Assert-Equal 'preset 2s has two sentences' 2 @((Resolve-ColdStartText '2s' '').Text -split '(?<=\.)\s+').Count
Assert-Equal 'preset 1s has one sentence' 1 @((Resolve-ColdStartText '1s' '').Text -split '(?<=\.)\s+').Count
Assert-Equal 'no preset = the fixed passage (null text)' $null (Resolve-ColdStartText '' '').Text
Assert-Equal 'free text wins over a preset' 'Hi there.' (Resolve-ColdStartText '2s' 'Hi there.').Text
$threw = $false; try { Resolve-ColdStartText '9x' '' | Out-Null } catch { $threw = $true }
Assert-Equal 'unknown preset throws' $true $threw
Assert-Equal 'no audio event: no chunk 0 audio length' $null (Get-ColdStartNumbers @() 1000.0).FirstAudioLengthMs
$audioEvents = @([pscustomobject]@{ Kind = 'audio'; Index = 1; Ms = 999; Time = 1000.5 }, [pscustomobject]@{ Kind = 'audio'; Index = 0; Ms = 2400; Time = 1000.6 })
Assert-Equal 'chunk 0 audio length is reported' 2400 (Get-ColdStartNumbers $audioEvents 1000.0).FirstAudioLengthMs
Assert-Equal 'median of three' 2 (Get-Median @(5, 1, 2))
Assert-Equal 'median of four' 2.5 (Get-Median @(1, 2, 3, 4))

# ---- highlight: SENTENCE <chunk> <n> with n >= 1 before READ_END
$moved = @(ConvertFrom-SentenceLog @(
    '1000.000  4242  4242 I RTST-TEST: RTST-TEST READ_START',
    '1001.500  4242  4242 I RTST-TEST: RTST-TEST SENTENCE 0 1',
    '1004.000  4242  4242 I RTST-TEST: RTST-TEST READ_END'))
Assert-Equal 'sentence marker parsed' 1 @($moved | Where-Object Kind -eq 'sentence').Count
Assert-Equal 'moved before the end' $true (Test-HighlightMoved $moved)
$late = @(ConvertFrom-SentenceLog @('1004.000  1  1 I RTST-TEST: RTST-TEST READ_END', '1005.000  1  1 I RTST-TEST: RTST-TEST SENTENCE 0 1'))
Assert-Equal 'a marker after READ_END is not a move' $false (Test-HighlightMoved $late)
Assert-Equal 'sentence 0 alone is not a move' $false (Test-HighlightMoved @(ConvertFrom-SentenceLog @('1001.000  1  1 I RTST-TEST: RTST-TEST SENTENCE 0 0')))
Assert-Equal 'no markers, no move' $false (Test-HighlightMoved @())

# ---- notification-text: parse the dumpsys record of the dev package only
$dump = @(
    '  NotificationRecord(0x1 : pkg=com.android.systemui user=UserHandle{0} id=1 tag=null)',
    '        android.title=String (Charging)',
    '  NotificationRecord(0x2 : pkg=uk.sirous.readthestupidtext.dbg user=UserHandle{0} id=7 tag=null)',
    '      extras={',
    '        android.title=String (Downloading natural voices 12%)',
    '        android.text=String (Cancel anytime)')
$note = Get-NotificationText $dump 'uk.sirous.readthestupidtext.dbg'
Assert-Equal 'title of the dev package' 'Downloading natural voices 12%' $note.Title
Assert-Equal 'text of the dev package' 'Cancel anytime' $note.Text
Assert-Equal 'another package is not read' $null (Get-NotificationText $dump 'uk.sirous.dinoreader.dbg')
Assert-Equal 'a good title has no problem' $null (Get-NotificationTitleProblem 'Downloading natural voices 12%')
Assert-Equal 'empty title' $true ((Get-NotificationTitleProblem '') -like '*empty*')
Assert-Equal 'raw placeholder' $true ((Get-NotificationTitleProblem 'Getting {0}') -like '*placeholder*')
Assert-Equal 'no percentage' $true ((Get-NotificationTitleProblem 'Downloading voice') -like '*percentage*')

# ---- several records of the dev package: the newest by post time, the one that names the pack when any does
$many = @(
    '  NotificationRecord(0x1 : pkg=uk.sirous.readthestupidtext.dbg user=UserHandle{0} id=3 tag=null)', '    mUpdateTimeMs=2000', '        android.title=String (Updated later, no pack)',
    '  NotificationRecord(0x2 : pkg=uk.sirous.readthestupidtext.dbg user=UserHandle{0} id=7 tag=piper-fa)', '    mUpdateTimeMs=1000', '        android.title=String (Downloading Persian voice 4%)',
    '  NotificationRecord(0x3 : pkg=uk.sirous.readthestupidtext.dbg user=UserHandle{0} id=8 tag=null)', '    mUpdateTimeMs=1500', '        android.title=String (Another)')
$records = Get-NotificationRecords $many 'uk.sirous.readthestupidtext.dbg'
Assert-Equal 'three records of the package' 3 $records.Count
Assert-Equal 'newest by post time without a pack name' 'Updated later, no pack' (Select-DownloadNotification $records '').Title
Assert-Equal 'the record that names the pack wins' 'Downloading Persian voice 4%' (Select-DownloadNotification $records 'piper-fa').Title
Assert-Equal 'a pack name nothing mentions is no notification' $null (Select-DownloadNotification $records 'piper-nb')
Assert-Equal 'the language code also names the pack' 'Downloading Persian voice 4%' (Select-DownloadNotification $records @('piper-nb', 'piper-fa')).Title
Assert-Equal 'a code inside another word does not match' $null (Select-DownloadNotification @([pscustomobject]@{ Raw = 'tag=false'; PostTime = 1; Order = 1; Title = 'x' }) @('fa'))
Assert-Equal 'a percent title is recognised' $true ('Downloading 12%' -match $script:PercentPattern)

# ---- notification-text (Slice 213 redesign): the notification posted after the download started, matched by package and time, not by pack name
$pk = 'uk.sirous.readthestupidtext.dbg'
$posted = @(
    '  NotificationRecord(0x1 : pkg=uk.sirous.readthestupidtext.dbg user=UserHandle{0} id=1 tag=null)', '    mUpdateTimeMs=500', '        android.title=String (Old download 90%)',
    '  NotificationRecord(0x2 : pkg=uk.sirous.readthestupidtext.dbg user=UserHandle{0} id=2 tag=null)', '    mUpdateTimeMs=3000', '        android.title=String (Getting voices)', '        android.text=String (Cancel anytime)',
    '  NotificationRecord(0x3 : pkg=uk.sirous.readthestupidtext.dbg user=UserHandle{0} id=3 tag=null)', '    mUpdateTimeMs=2000', '        android.title=String (Downloading voices)', '        android.text=String (12% of 300 MB)',
    '  NotificationRecord(0x4 : pkg=com.other user=UserHandle{0} id=4 tag=null)', '    mUpdateTimeMs=9000', '        android.title=String (Other 50%)')
$recs = Get-NotificationRecords $posted $pk
$sel = Select-PostedProgressNotification $recs 1000 @('supertonic-3') $false
Assert-Equal 'a percent in the text counts, no pack name needed' 'Downloading voices' $sel.Title
Assert-Equal 'text is read verbatim' '12% of 300 MB' $sel.Text
Assert-Equal 'a record posted before the start is not taken' $null (Select-PostedProgressNotification $recs 2500 @('supertonic-3') $false)
Assert-Equal 'with the pack name expected (Slice 208) an unnamed notification fails' $null (Select-PostedProgressNotification $recs 1000 @('supertonic-3') $true)
Assert-Equal 'another package never matches' 'Downloading voices' (Select-PostedProgressNotification $recs 0 @() $false).Title
Assert-Equal 'the Slice 208 rule is on: the download text names the language and the size' $true $script:ExpectPackNameInNotification
$named = Get-NotificationRecords @(
    '  NotificationRecord(0x5 : pkg=uk.sirous.readthestupidtext.dbg user=UserHandle{0} id=5 tag=null)', '    mUpdateTimeMs=4000', '        android.title=String (Downloading the English voice (63 MB)... 12%)') $pk
Assert-Equal 'a notification naming the language and the size passes the Slice 208 rule' $true ($null -ne (Select-PostedProgressNotification $named 1000 @('English') $true))
Assert-Equal 'a size without the language does not pass' $null (Select-PostedProgressNotification $named 1000 @('French') $true)
Assert-Equal 'a language without the size does not pass' $null (Select-PostedProgressNotification (Get-NotificationRecords @('  NotificationRecord(0x6 : pkg=uk.sirous.readthestupidtext.dbg user=UserHandle{0} id=6 tag=null)', '    mUpdateTimeMs=4000', '        android.title=String (Downloading English voices 12%)') $pk) 1000 @('English') $true)
$noTime = Get-NotificationRecords @('  NotificationRecord(0x9 : pkg=uk.sirous.readthestupidtext.dbg user=UserHandle{0} id=9 tag=null)', '        android.title=String (Old download 90%)') $pk
Assert-Equal 'review: a record with no post time is not proof of a fresh download (finding 6)' $null (Select-PostedProgressNotification $noTime 1000 @() $false)
Assert-Equal 'a notification passes' $true (Resolve-NotificationVerdict $sel).Passed
Assert-Equal 'verdict quotes title and text' $true ((Resolve-NotificationVerdict $sel).Detail -like "*Downloading voices*12% of 300 MB*")
Assert-Equal 'none fails' $false (Resolve-NotificationVerdict $null).Passed
Assert-Equal 'review: no post time in the dump is flagged' $true (Test-PostTimeMissing $noTime)
Assert-Equal 'review: records with a post time are not flagged' $false (Test-PostTimeMissing $recs)
Assert-Equal 'review: the unknown post time is named, not "no notification"' $true ((Resolve-NotificationVerdict $null $true).Detail -like '*no post time*')

# ---- book-end: wake locks of the app's uid, or a foreground service
Assert-Equal 'uid from dumpsys package' '10234' (Get-PackageUid '    userId=10234')
Assert-Equal 'uid from appId (S21)' '10460' (Get-PackageUid '    appId=10460')
$power = "Wake Locks: size=2`n  PARTIAL_WAKE_LOCK              'AudioMix' ACQ=-5s23ms (uid=1013 pid=900)`n  PARTIAL_WAKE_LOCK              'MediaPlayer' ACQ=-2s (uid=10234 pid=4242)"
Assert-Equal 'app wake lock held' $true (Test-AppHoldsWake $power '' '10234')
Assert-Equal 'other uid only' $false (Test-AppHoldsWake "Wake Locks: size=1`n  PARTIAL_WAKE_LOCK 'AudioMix' (uid=1013 pid=900)" '' '10234')
Assert-Equal 'worksource counts' $true (Test-AppHoldsWake "Wake Locks: size=1`n  PARTIAL_WAKE_LOCK 'x' (uid=1013 ws=WorkSource{10234})" '' '10234')
Assert-Equal 'foreground service holds it' $true (Test-AppHoldsWake '' '  ServiceRecord{1 u0 x/.S}`n    isForeground=true' '10234')
Assert-Equal 'nothing held' $false (Test-AppHoldsWake '' '  isForeground=false' '10234')

# ---- preflight: one clear line, no "NO-GO" followed by carrying on; no space is fatal
Assert-Equal 'no space stops the run' $false (Resolve-PreflightVerdict @('  [FAIL] free space: 0.2 GB free on /data (under 0.5 GB: no room for a voice pack)', 'PREFLIGHT NO-GO: free space: x') 1).Go
Assert-Equal 'a clean GO line' 'preflight: GO' (Format-PreflightLine (Resolve-PreflightVerdict $goLines 0))
$warned = Format-PreflightLine $verdict
Assert-Equal 'GO with warnings says it carries on' $true ($warned -like 'preflight: GO (warnings, carrying on: stay awake*')
Assert-Equal 'the GO line never says NO-GO' $false ($warned -like '*NO-GO*')
$stopLine = Format-PreflightLine $noGo
Assert-Equal 'STOP names the fatal reasons' $true ($stopLine -like 'preflight: STOP - charging:*; screen lock:*')
Assert-Equal 'the STOP line never says NO-GO' $false ($stopLine -like '*NO-GO*')

# ---- -Reinstall installs again over the same build, and the install is proven
Assert-Equal '-Reinstall over the same build: install again' 'Install' (Resolve-InstallPlan $marker '2026-10-30_10:11:12' $aot 'abc123' $true).Action
Assert-Equal 'an install that happened has no problem' $null (Resolve-InstallProof 0 'Performing Streamed Install Success' '2026-10-30_10:11:12' '2026-10-30_10:20:00')
Assert-Equal 'a first install has no before time' $null (Resolve-InstallProof 0 'Success' $null '2026-10-30_10:20:00')
Assert-Equal 'a non-zero exit fails with the adb output' $true ((Resolve-InstallProof 1 'Failure [INSTALL_FAILED_X]' 'a' 'b').Contains('exited 1: Failure [INSTALL_FAILED_X]'))
Assert-Equal 'no Success in the output fails' $true ((Resolve-InstallProof 0 'nothing' 'a' 'b') -like '*did not say Success*')
Assert-Equal 'an unchanged lastUpdateTime fails' $true ((Resolve-InstallProof 0 'Success' 'same' 'same') -like '*lastUpdateTime did not change*')
Assert-Equal 'an invisible package fails' $true ((Resolve-InstallProof 0 'Success' 'a' $null) -like '*not visible*')

# ---- -Prepare: the pack download log, the 60 s start rule, the stall rule (the layout itself is tested in test-phone-lib.ps1)
$packIds = @('supertonic-3')
Assert-Equal 'a done line for the pack' 'Done' (Resolve-PackDownloadLog @('1.0 1 1 I RTST-TEST: PACKDL done pack=supertonic-3 bytes=5 seconds=9') $packIds).State
Assert-Equal 'a failed line for the pack' 'Failed' (Resolve-PackDownloadLog @('1.0 1 1 I RTST-TEST: PACKDL failed pack=supertonic-3 attempts=3 result=Offline') $packIds).State
Assert-Equal 'another pack is ignored' 'Idle' (Resolve-PackDownloadLog @('PACKDL failed pack=piper-fa attempts=3 result=Offline') $packIds).State
Assert-Equal 'a start line is Started' 'Started' (Resolve-PackDownloadLog @('PACKDL start pack=supertonic-3 offset=0') $packIds).State
Assert-Equal 'failed outranks started' 'Failed' (Resolve-PackDownloadLog @('PACKDL start pack=supertonic-3 offset=0', 'PACKDL failed pack=supertonic-3 attempts=3 result=Offline') $packIds).State
Assert-Equal 'no lines is Idle' 'Idle' (Resolve-PackDownloadLog @() $packIds).State
$logLines = 1..15 | ForEach-Object { "10.$_ 1 1 I RTST-TEST: line $_" }
$tail = @(Get-AppLogTail (@('99.0 1 1 I Other: noise') + $logLines) 10)
Assert-Equal 'tail keeps 10 lines' 10 $tail.Count
Assert-Equal 'tail is the newest' $true ($tail[9] -like '*line 15')
Assert-Equal 'tail only RTST-TEST lines' $false ($tail -join '|' -like '*Other*')
$noStart = Get-NoPackStartReason $logLines $packIds
Assert-Equal 'no-start reason names the 60 s rule' $true ($noStart -like 'no PACKDL start for supertonic-3 within 60 s*')
Assert-Equal 'no-start reason quotes the app log' $true ($noStart -like '*line 15')
Assert-Equal 'no-start reason with an empty log says so' $true ((Get-NoPackStartReason @() $packIds) -like '*no RTST-TEST lines at all*')
Assert-Equal 'the SKIP reason for a missing pack' 'no pack: run -Prepare first' $script:NoPackReason
Assert-Equal 'partial size: the biggest' 300 (Get-PartialBytes @('100', '300', 'stat: no such file'))
Assert-Equal 'no partial file: 0' 0 (Get-PartialBytes @())
$t = Get-Date
Assert-Equal 'growing 119 s ago is not a stall' $false (Test-PackStalled $t.AddSeconds(-119) $t 120)
Assert-Equal 'not growing for 120 s is a stall' $true (Test-PackStalled $t.AddSeconds(-120) $t 120)
Assert-Equal 'progress with a known total' '12.5 MB of 100 MB' (Format-PackProgress 13107200 104857600)
Assert-Equal 'progress without a total' '1 MB (total size not known)' (Format-PackProgress 1048576)

# ---- the budget counts the time since the runner started (a wait after 4 minutes has 6 of 10 left; capped at the 3-minute wait cap)
$start = Get-Date
Assert-Equal 'pre-wait is capped' $script:PreWaitCapMinutes (Get-PreWaitMinutes $start $start 10)
Assert-Equal 'pre-wait shrinks with the elapsed time' 1 (Get-PreWaitMinutes $start $start.AddMinutes(9) 10)
Assert-Equal 'pre-wait is never negative' 0 (Get-PreWaitMinutes $start $start.AddMinutes(12) 10)


# ---- builds in the background: the state of one build
$now = Get-Date
$fresh = [pscustomobject]@{ state = 'building'; startedUtc = (ConvertTo-DeadlineText $now.AddMinutes(-2)); error = $null }
Assert-Equal 'cached is ok whatever the status says' 'ok' (Resolve-BuildState $null $false $null $now $true).State
Assert-Equal 'no status yet: queued' 'queued' (Resolve-BuildState $null $false $null $now $false).State
Assert-Equal 'no status and the process is gone: failed' 'failed' (Resolve-BuildState $null $true $null $now $false).State
Assert-Equal 'building with a growing log: building' 'building' (Resolve-BuildState $fresh $false $now.AddSeconds(-20) $now $false).State
$stalled = Resolve-BuildState $fresh $false $now.AddMinutes(-5) $now $false
Assert-Equal 'a log not growing for 5 minutes: failed (stalled)' 'failed' $stalled.State
Assert-Equal 'stalled says so' $true ($stalled.Reason -like 'stalled*')
Assert-Equal 'queued is never stalled (it waits for a lock)' 'queued' (Resolve-BuildState ([pscustomobject]@{ state = 'queued'; startedUtc = (ConvertTo-DeadlineText $now.AddMinutes(-30)) }) $false $null $now $false).State
Assert-Equal 'a failed status keeps its error' 'dotnet build exited 1' (Resolve-BuildState ([pscustomobject]@{ state = 'failed'; error = 'dotnet build exited 1' }) $true $null $now $false).Reason
Assert-Equal 'the process exiting mid-build is failed' 'failed' (Resolve-BuildState $fresh $true $now $now $false).State
Assert-Equal 'ok without an APK in the cache is failed' 'failed' (Resolve-BuildState ([pscustomobject]@{ state = 'ok' }) $true $null $now $false).State
$logTail = 1..30 | ForEach-Object { "line $_" }
$failure = Format-BuildFailure 'aot' 'dotnet build exited 1' $logTail
Assert-Equal 'the failure has the last 20 log lines' $true ($failure -like '*Last 20 log lines: line 11 | *| line 30')
Assert-Equal 'the failure names the build' $true ($failure -like 'the aot build failed: dotnet build exited 1.*')

# ---- which build each check picks (Resolve-PlanStep / Get-NextStep)
function New-Builds([string]$Debug, [string]$Aot) {
    return @{ debug = [pscustomobject]@{ State = $Debug; Failure = $(if ($Debug -eq 'failed') { 'the debug build failed: x' }) }; aot = [pscustomobject]@{ State = $Aot; Failure = $(if ($Aot -eq 'failed') { 'the aot build failed: y' }) } }
}
$cold = [pscustomobject]@{ Name = 'cold-start'; Variant = 'aot'; Status = 'Pending' }
$hl = [pscustomobject]@{ Name = 'highlight'; Variant = 'debug'; Status = 'Pending' }
$nt = [pscustomobject]@{ Name = 'notification-text'; Variant = 'debug'; Status = 'Pending' }
$step = Get-NextStep @($cold, $hl, $nt) (New-Builds 'building' 'building') $null
Assert-Equal 'nothing ready: wait' 'Wait' $step.Action
Assert-Equal 'the wait names what it waits for' $true ($step.Detail -like '*waiting for the aot build*cold-start*')
$step = Get-NextStep @($cold, $hl, $nt) (New-Builds 'ok' 'building') $null
Assert-Equal 'debug ready first: the first check that does not need aot runs' 'highlight' $step.Plan.Name
Assert-Equal 'it runs on debug' 'debug' $step.Variant
$step = Get-NextStep @($cold, $hl, $nt) (New-Builds 'ok' 'ok') $null
Assert-Equal 'both ready at the start: cold-start first, on aot' 'cold-start' $step.Plan.Name
$step = Get-NextStep @($hl, $nt) (New-Builds 'ok' 'ok') $null
Assert-Equal 'both ready: a debug-class check prefers aot' 'aot' $step.Variant
$step = Get-NextStep @($nt) (New-Builds 'ok' 'ok') 'debug'
Assert-Equal 'a check that does not need aot never swaps' 'debug' $step.Variant
$step = Get-NextStep @($cold) (New-Builds 'ok' 'ok') 'debug'
Assert-Equal 'cold-start swaps debug -> aot' 'aot' $step.Variant
$step = Get-NextStep @($hl) (New-Builds 'ok' 'ok') 'aot'
Assert-Equal 'installed aot: the others stay on aot (never back)' 'aot' $step.Variant
$step = Get-NextStep @($cold, $hl) (New-Builds 'ok' 'building') 'debug'
Assert-Equal 'debug installed, aot building: the debug check runs while cold-start waits' 'highlight' $step.Plan.Name
$step = Get-NextStep @($cold) (New-Builds 'ok' 'building') 'debug'
Assert-Equal 'only cold-start left and aot building: wait, never fall back to debug' 'Wait' $step.Action
$step = Get-NextStep @($cold, $hl) (New-Builds 'ok' 'failed') $null
Assert-Equal 'an aot failure fails cold-start first' 'Fail' $step.Action
Assert-Equal 'it fails the check that needs it' 'cold-start' $step.Plan.Name
Assert-Equal 'with the build failure text' $true ($step.Detail -like '*the aot build failed*')
$step = Get-NextStep @($hl) (New-Builds 'ok' 'failed') $null
Assert-Equal 'an aot failure does not stop a debug-class check' 'Run' $step.Action
$step = Get-NextStep @($hl) (New-Builds 'failed' 'building') $null
Assert-Equal 'debug failed, aot building: wait for aot' 'Wait' $step.Action
$step = Get-NextStep @($hl) (New-Builds 'failed' 'ok') $null
Assert-Equal 'debug failed, aot ok: the debug-class check runs on aot' 'aot' $step.Variant
$step = Get-NextStep @($hl) (New-Builds 'failed' 'failed') $null
Assert-Equal 'both failed: the check FAILs' 'Fail' $step.Action
Assert-Equal 'the failure reports the debug build and the aot one' $true ($step.Detail -like '*debug build failed*aot build failed too*')

# a whole run, simulated as the builds progress: at most one swap, every check ends up run
$pending = New-Object System.Collections.ArrayList; [void]$pending.AddRange(@($cold, $hl, $nt)); $installed = $null; $swaps = 0; $ran = @()
foreach ($builds in @((New-Builds 'building' 'building'), (New-Builds 'ok' 'building'), (New-Builds 'ok' 'ok'))) {
    while ($pending.Count -gt 0) {
        $next = Get-NextStep @($pending) $builds $installed
        if ($next.Action -ne 'Run') { break }
        if ($installed -and $installed -ne $next.Variant) { $swaps++ }
        $installed = $next.Variant; $ran += "$($next.Plan.Name):$($next.Variant)"; [void]$pending.Remove($next.Plan)
    }
}
Assert-Equal 'the simulated run ran everything' 0 $pending.Count
Assert-Equal 'debug checks on debug while aot builds, then cold-start on aot' 'highlight:debug,notification-text:debug,cold-start:aot' ($ran -join ',')
Assert-Equal 'one swap only' 1 $swaps

# ---- run-status.json
$bookEnd = [pscustomobject]@{ Name = 'book-end'; Variant = 'debug'; Status = 'SKIP' }
$doneRows = @([pscustomobject]@{ Name = 'highlight'; Status = 'PASS'; Detail = 'moved' })
$status = New-RunStatus 'rtst' $now (New-Builds 'ok' 'failed') @($cold, $hl, $bookEnd) $doneRows 'cold-start' $null 'debug'
Assert-Equal 'status: the build states' 'failed' $status.builds.aot.state
Assert-Equal 'status: a finished check' 'PASS' (@($status.checks | Where-Object { $_.name -eq 'highlight' }))[0].state
Assert-Equal 'status: the running check' 'running' (@($status.checks | Where-Object { $_.name -eq 'cold-start' }))[0].state
Assert-Equal 'status: a skipped check' 'SKIP' (@($status.checks | Where-Object { $_.name -eq 'book-end' }))[0].state
Assert-Equal 'status: the last update time' (ConvertTo-DeadlineText $now) $status.updatedUtc
$roundTrip = ($status | ConvertTo-Json -Depth 6) | ConvertFrom-Json
Assert-Equal 'status survives JSON' 'debug' $roundTrip.installedVariant
Assert-Equal 'status keeps the aot failure text' 'the aot build failed: y' $roundTrip.builds.aot.failure
# the variant a check ran on (the installed one) is recorded, not the declared one
$ranRows = @([pscustomobject]@{ Name = 'highlight'; Status = 'PASS'; Detail = 'ok'; Variant = 'aot' })
$ranStatus = New-RunStatus 'rtst' $now (New-Builds 'ok' 'ok') @($hl) $ranRows $null $null 'aot'
Assert-Equal 'status: ran-on variant beats declared' 'aot' $ranStatus.checks[0].variant
# the start line names each build and why
$script:App = 'rtst'
$fakeBuilds = @{ debug = [pscustomobject]@{ Process = 1 }; aot = [pscustomobject]@{ Process = $null } }
Assert-Equal 'start line: nothing waits' 'starting builds: rtst debug (for later runs; nothing in this run waits for it)' (Format-BuildsStartLine $fakeBuilds @($cold))
Assert-Equal 'start line: a check waits' 'starting builds: rtst debug (for highlight)' (Format-BuildsStartLine $fakeBuilds @($hl))
$waitStatus = New-RunStatus 'rtst' $now (New-Builds 'building' 'building') @($cold) @() $null 'waiting for the aot build (building) for cold-start' $null
Assert-Equal 'status: a waiting check' 'waiting' $waitStatus.checks[0].state
$beat = Join-Path ([IO.Path]::GetTempPath()) ('rtst-status-' + [guid]::NewGuid().ToString('N') + '.json')
try {
    ($status | ConvertTo-Json -Depth 6) | Set-Content $beat
    $env:RTST_RUN_STATUS_FILE = $beat
    Remove-Variable -Name RtstHeartbeatAt -Scope Global -ErrorAction SilentlyContinue
    Send-RunHeartbeat
    $after = Get-Content $beat -Raw | ConvertFrom-Json
    Assert-Equal 'a check wait loop refreshes updatedUtc' $true ((ConvertFrom-StatusTime $after.updatedUtc) -ge $now.AddSeconds(-1))
    Assert-Equal 'the heartbeat keeps the rest of the file' 'cold-start' $after.checks[0].name
} finally { $env:RTST_RUN_STATUS_FILE = $null; Remove-Item $beat -ErrorAction SilentlyContinue; Remove-Variable -Name RtstHeartbeatAt -Scope Global -ErrorAction SilentlyContinue }

if ($failed) { Write-Host "$failed run-checks checks failed (PowerShell $($PSVersionTable.PSVersion))."; exit 1 }
Write-Host "All run-checks checks pass (PowerShell $($PSVersionTable.PSVersion))."
