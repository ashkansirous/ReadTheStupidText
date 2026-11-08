<#
.SYNOPSIS
  Self-test for the pure parts of Slice 241 (speed.ps1): snapshot boot choice and command line, the crash lines, the flow split. Needs no device. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
if (-not $env:ANDROID_HOME -and -not $env:ANDROID_SDK_ROOT) { $env:ANDROID_HOME = [IO.Path]::GetTempPath() }   # common.ps1 only needs it to build paths
. (Join-Path $PSScriptRoot 'common.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

# item 2: boot mode and command line
Assert-Equal 'snapshot exists' 'snapshot' (Resolve-BootMode $false $true)
Assert-Equal 'no snapshot yet' 'cold' (Resolve-BootMode $false $false)
Assert-Equal '-ColdBoot wins' 'cold' (Resolve-BootMode $true $true)
$snap = (Get-EmulatorStartArguments 'RTST_Test' 5554 'swiftshader_indirect' 4096 'snapshot') -join ' '
Assert-Equal 'snapshot args load the clean snapshot' $true ($snap -like '*-snapshot rtst-clean*')
Assert-Equal 'snapshot args never save' $true ($snap -like '*-no-snapshot-save*')
Assert-Equal 'snapshot args do not disable the load' $false ($snap -like '*-no-snapshot-load*')
$cold = (Get-EmulatorStartArguments 'RTST_Test' 5554 'swiftshader_indirect' 4096 'cold') -join ' '
Assert-Equal 'cold args skip the load' $true ($cold -like '*-no-snapshot-load*')
Assert-Equal 'cold args never save' $true ($cold -like '*-no-snapshot-save*')
Assert-Equal 'cold args name no snapshot' $false ($cold -like '*-snapshot rtst-clean*')

$home_ = Join-Path ([IO.Path]::GetTempPath()) ("rtst-avd-" + [guid]::NewGuid().ToString('N'))
try {
    Assert-Equal 'no AVD folder' $false (Test-CleanSnapshotExists 'RTST_Test' $home_)
    $dir = Join-Path $home_ 'RTST_Test.avd\snapshots\rtst-clean'
    $null = New-Item -ItemType Directory -Force $dir
    Assert-Equal 'empty snapshot folder is an interrupted save' $false (Test-CleanSnapshotExists 'RTST_Test' $home_)
    Set-Content (Join-Path $dir 'snapshot.pb') 'x'
    Assert-Equal 'snapshot folder with files' $true (Test-CleanSnapshotExists 'RTST_Test' $home_)
    Assert-Equal 'other AVD has none' $false (Test-CleanSnapshotExists 'RTST_Test2' $home_)
} finally { Remove-Item $home_ -Recurse -Force -ErrorAction SilentlyContinue }
$cmds = @(Get-CleanStateCommands @('a.b', 'c.d'))
Assert-Equal 'clean state uninstalls each package' 'pm uninstall a.b|pm uninstall c.d' ($cmds -join '|')

# item 1: crash lines
$pkg = 'uk.sirous.readthestupidtext'
$crashLog = @'
11-01 10:00:00.000  1234  1234 E AndroidRuntime: FATAL EXCEPTION: main
11-01 10:00:00.000  1234  1234 E AndroidRuntime: Process: uk.sirous.readthestupidtext, PID: 1234
11-01 10:00:00.000  1234  1234 E AndroidRuntime: java.lang.IllegalStateException: boom
11-01 10:00:00.000  1234  1234 E AndroidRuntime: 	at x.y.Z
'@
$found = @(Find-AppCrashLines $crashLog '' $pkg)
Assert-Equal 'crash: lines found' 4 $found.Count
Assert-Equal 'crash: starts at FATAL EXCEPTION' $true ($found[0] -like '*FATAL EXCEPTION*')
Assert-Equal 'crash: other package ignored' 0 @(Find-AppCrashLines $crashLog '' 'uk.sirous.dinoreader.dbg').Count
Assert-Equal 'crash: empty log' 0 @(Find-AppCrashLines '' '' $pkg).Count
$native = @'
11-01 21:46:30.000  900  912 F libc    : Fatal signal 11 (SIGSEGV), code 1 (SEGV_MAPERR), fault addr 0x8 in tid 912 (.NET TP Worker), pid 900 (uk.sirous.readthestupidtext)
11-01 21:46:30.100  950  950 F DEBUG   : pid: 900, tid: 912, name: .NET TP Worker  >>> uk.sirous.readthestupidtext <<<
11-01 21:46:30.200  951  951 F libc    : Fatal signal 11 (SIGSEGV), code 1 in tid 5 (x), pid 700 (uk.sirous.other)
'@
Assert-Equal 'crash: native signal lines found (no Process: line)' 2 @(Find-AppCrashLines $native '' $pkg).Count
Assert-Equal 'crash: native signal of another package ignored' 0 @(Find-AppCrashLines $native '' 'uk.sirous.readthestupidtext.extra').Count
$anr = '11-01 10:00:00.000  500  520 E ActivityManager: ANR in uk.sirous.readthestupidtext (uk.sirous.readthestupidtext/.MainActivity)'
Assert-Equal 'anr: found' 1 @(Find-AppCrashLines '' $anr $pkg).Count
Assert-Equal 'anr: prefix package is not a match' 0 @(Find-AppCrashLines '' $anr 'uk.sirous.readthestupidtext.extra').Count

# item 4: split
$four = Split-FlowNames @('a', 'b', 'c', 'd') $true
Assert-Equal 'split 4: first' 'a,b' ($four.First -join ',')
Assert-Equal 'split 4: second' 'c,d' ($four.Second -join ',')
$five = Split-FlowNames @('a', 'b', 'c', 'd', 'e') $true
Assert-Equal 'split 5: first' 'a,b,c' ($five.First -join ',')
Assert-Equal 'split 5: second' 'd,e' ($five.Second -join ',')
$three = Split-FlowNames @('a', 'b', 'c') $true
Assert-Equal 'three flows stay on one emulator' 0 @($three.Second).Count
Assert-Equal 'three flows: first' 'a,b,c' ($three.First -join ',')
Assert-Equal 'split is opt-in: not asked, one emulator' 0 @((Split-FlowNames @('a', 'b', 'c', 'd') $false).Second).Count
Assert-Equal 'not asked: all flows stay first' 'a,b,c,d' ((Split-FlowNames @('a', 'b', 'c', 'd') $false).First -join ',')

# the snapshot is deleted only after a failure with no load, or the second failure in a row
Assert-Equal 'snapshot: failed idle -> delete' $true (Resolve-SnapshotRemoval $false 1)
Assert-Equal 'snapshot: first failure under load -> keep' $false (Resolve-SnapshotRemoval $true 1)
Assert-Equal 'snapshot: second failure under load -> delete' $true (Resolve-SnapshotRemoval $true 2)

# a bounded boot wait gives up on a boot that is still running, and reports an ended one
$hung = [pscustomobject]@{ HasExited = $false; Log = ''; ExitCode = $null; Handle = $null } | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value { param($ms) return $false } -PassThru
Assert-Equal 'boot wait: hung boot times out' 'False' ([string](Wait-EmulatorBoot $hung 1))
$done = [pscustomobject]@{ HasExited = $true; Log = (Join-Path ([IO.Path]::GetTempPath()) 'rtst-no-such-boot.log'); ExitCode = 0; Handle = $null } | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value { param($ms) return $true } -PassThru
Assert-Equal 'boot wait: finished boot' 'True' ([string](Wait-EmulatorBoot $done 1))
Assert-Equal 'flow list: commas, paths, array' 'one,two,three' ((Split-FlowList @('one,.maestro\rtst\two.yaml', 'three')) -join ',')
Assert-Equal 'flow list: a space stays in the name' 'my flow' ((Split-FlowList 'my flow') -join ',')
Assert-Equal 'flow list: empty' 0 @(Split-FlowList '').Count

# item 5 helper: the cmd line keeps inner quotes
Assert-Equal 'cmd line' '/d /s /c ""C:\a b\pwsh.exe" -File "x y.ps1" >"o.log" 2>"e.log""' (ConvertTo-CmdLine 'C:\a b\pwsh.exe' '-File "x y.ps1"' 'o.log' 'e.log')

# Slice 247 items e + c: the crash watch decision and the Mono SIGSEGV retry rule (fixture: the 2026-11-01 crash, docs/plan/notes/2026-11-01-emulator-mono-sigsegv.md)
$dinoPkg = 'uk.sirous.dinoreader.dbg'
$mono = Get-Content (Join-Path $PSScriptRoot 'fixtures\mono-sigsegv-crash.txt') -Raw
Assert-Equal 'watch: the fixture is a crash of the Dino app' $true (Test-AppCrashed $mono '' $dinoPkg)
Assert-Equal 'watch: another package is not a crash' $false (Test-AppCrashed $mono '' $pkg)
Assert-Equal 'watch: empty buffer is not a crash' $false (Test-AppCrashed '' '' $dinoPkg)
Assert-Equal 'watch: a Java crash counts' $true (Test-AppCrashed $crashLog '' $pkg)
Assert-Equal 'retry: uptime read from the fixture' 6 (Get-CrashProcessUptime $mono $dinoPkg)
Assert-Equal 'retry: Mono SIGSEGV at 6 s is retryable' $true (Test-RetryableMonoCrash $mono $dinoPkg 20)
Assert-Equal 'retry: same crash at 6 s with a 5 s limit is not' $false (Test-RetryableMonoCrash $mono $dinoPkg 5)
Assert-Equal 'retry: late Mono SIGSEGV (uptime 45 s) is not' $false (Test-RetryableMonoCrash ($mono -replace 'Process uptime: 6s', 'Process uptime: 45s') $dinoPkg 20)
Assert-Equal 'retry: no uptime line is not' $false (Test-RetryableMonoCrash ($mono -replace 'Process uptime: 6s', '') $dinoPkg 20)
Assert-Equal 'retry: a Java crash is not' $false (Test-RetryableMonoCrash $crashLog $pkg 20)
Assert-Equal 'retry: a native crash with no Mono frame is not' $false (Test-RetryableMonoCrash ($mono -replace 'libmonosgen|\.NET TP Worker', 'libother') $dinoPkg 20)
Assert-Equal 'retry: another package is not' $false (Test-RetryableMonoCrash $mono $pkg 20)
$lateThenEarly = ($mono -replace 'Process uptime: 6s', 'Process uptime: 45s') + "`n" + $mono
Assert-Equal 'retry: two blocks, the last (6 s) decides' $true (Test-RetryableMonoCrash $lateThenEarly $dinoPkg 20)
Assert-Equal 'retry: two blocks, the last one late is not retryable' $false (Test-RetryableMonoCrash ($mono + "`n" + ($mono -replace 'Process uptime: 6s', 'Process uptime: 45s')) $dinoPkg 20)
# the real probe wiring (a closure): the serial reaches the reader, and the crash text is kept for the verdict
$seenProbe = @{ CrashLog = '' }
$serialsRead = New-Object System.Collections.ArrayList
$probe = New-AppCrashProbe 'emulator-5554' $dinoPkg $seenProbe ({ param($s) [void]$serialsRead.Add($s); $mono }.GetNewClosure())
Assert-Equal 'probe: sees the crash of the fixture' $true ([bool](& $probe))
Assert-Equal 'probe: reads the device it was given' 'emulator-5554' $serialsRead[0]
Assert-Equal 'probe: keeps the crash text' $true ($seenProbe.CrashLog -like '*libmonosgen*')
$quietProbe = New-AppCrashProbe 'emulator-5554' $dinoPkg @{ CrashLog = '' } ({ param($s) '' })
Assert-Equal 'probe: an empty buffer is no crash' $false ([bool](& $quietProbe))
Assert-Equal 'phases: a truncated commands.json gives empty phases, no error' 'none' (Get-FlowPhaseTimes '[ {"command":{"launchAppC').Largest
$failureText = Format-MaestroFailure "Exception: device offline`nat java.base" "step 1`nstep 2 FAILED"
Assert-Equal 'failure: quotes stderr, labelled' $true ($failureText -like '*--- stderr*Exception: device offline*')
Assert-Equal 'failure: quotes stdout after it, labelled' $true ($failureText -like '*--- stdout*step 2 FAILED')
Assert-Equal 'probe: an error counts as no crash' $false (Invoke-CrashProbeSafely { throw 'adb failed' })
Assert-Equal 'retry summary: names the retried flow' 1 @(Format-RetrySummary @([pscustomobject]@{ Name = 'a'; Retried = 'CRASH-RETRIED a: x'; FirstRunSeconds = 4.0 }, [pscustomobject]@{ Name = 'b'; Retried = $null; FirstRunSeconds = 0.0 })).Count
Assert-Equal 'retry: line names the flow' $true ((Format-CrashRetryLine 'my-flow' 'Fatal signal 11') -like 'CRASH-RETRIED my-flow:*')

# the watch kills a process that outlives a crash within the poll time (a real child process: a 30 s sleep is killed after about 2 s)
$sleeper = Start-Process -FilePath ((Get-Process -Id $PID).Path) -ArgumentList '-NoProfile -Command Start-Sleep -Seconds 30' -PassThru -WindowStyle Hidden
$watchTimer = [Diagnostics.Stopwatch]::StartNew()
$killedByWatch = Wait-ProcessWithCrashWatch $sleeper { $true } 1
Assert-Equal 'watch: a crash kills the process' $true $killedByWatch
Assert-Equal 'watch: the kill is quick (under 10 s)' $true ($watchTimer.Elapsed.TotalSeconds -lt 10)
Assert-Equal 'watch: the process is gone' $true ($sleeper.HasExited)
$quick = Start-Process -FilePath ((Get-Process -Id $PID).Path) -ArgumentList '-NoProfile -Command exit 0' -PassThru -WindowStyle Hidden
Assert-Equal 'watch: a process that ends by itself is not reported as killed' $false (Wait-ProcessWithCrashWatch $quick { $false } 1)

# item a: phases from a Maestro commands.json (shape read from a real run: command/metadata with duration in ms and depth)
$commandsJson = @'
[ {"command":{"defineVariablesCommand":{"env":{}}},"metadata":{"status":"COMPLETED","timestamp":1,"duration":8,"depth":0}},
  {"command":{"launchAppCommand":{"appId":"x","clearState":true}},"metadata":{"status":"COMPLETED","timestamp":2,"duration":2611,"depth":0}},
  {"command":{"assertConditionCommand":{}},"metadata":{"status":"COMPLETED","timestamp":3,"duration":5218,"depth":0}},
  {"command":{"tapOnElementCommand":{}},"metadata":{"status":"COMPLETED","timestamp":4,"duration":400,"depth":0}},
  {"command":{"tapOnElementCommand":{}},"metadata":{"status":"COMPLETED","timestamp":5,"duration":9000,"depth":1}},
  {"command":{"assertConditionCommand":{}},"metadata":{"status":"FAILED","timestamp":6,"duration":25182,"depth":0}} ]
'@
$ph = Get-FlowPhaseTimes $commandsJson
Assert-Equal 'phases: launch' '2.611' ([string]$ph.Launch)
Assert-Equal 'phases: wait' '30.4' ([string]$ph.Wait)
Assert-Equal 'phases: action (depth 1 ignored)' '0.4' ([string]$ph.Action)
Assert-Equal 'phases: largest' 'Wait' $ph.Largest
Assert-Equal 'phases: slowest' 'assertCondition 25.2 s FAILED' $ph.Slowest
Assert-Equal 'phases: empty json' 'none' (Get-FlowPhaseTimes '').Largest
Assert-Equal 'phases: line' $true ((Format-FlowPhases 'f' $ph 40.0) -like 'PHASES f: launch 2.6 s, wait 30.4 s, action 0.4 s, other 6.6 s*largest: Wait*')

# Slice 301 item 1: a kill that never ends (a fake process whose WaitForExit always times out) is reported after the limit, not waited out
$stuck = [pscustomobject]@{ Id = 4242 }
$stuck | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value { param([int]$Milliseconds) Start-Sleep -Milliseconds $Milliseconds; return $false }
$killTimer = [Diagnostics.Stopwatch]::StartNew()
Assert-Equal 'kill-wait: a process that never ends gives false' $false (Wait-AfterCrashKill $stuck 1)
Assert-Equal 'kill-wait: it returns after the limit (under 10 s)' $true ($killTimer.Elapsed.TotalSeconds -lt 10)
$gone = [pscustomobject]@{ Id = 1 }
$gone | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value { param([int]$Milliseconds) return $true }
Assert-Equal 'kill-wait: a process that ended gives true' $true (Wait-AfterCrashKill $gone 1)
Assert-Equal 'kill-wait: the default limit is 60 s' 60 $script:CrashKillWaitSeconds
Assert-Equal 'stuck: line names the tree and is not a crash' $true ((Format-RunnerStuckLine 4242 60) -like 'runner stuck: Maestro tree 4242 would not exit*60 s*' -and (Format-RunnerStuckLine 4242 60) -notlike '*crash*')
Assert-Equal 'stuck: summary names only the flow that is stuck' 1 @(Format-RunnerStuckSummary @([pscustomobject]@{ Name = 'a'; RunnerStuck = $true; StuckPid = 9 }, [pscustomobject]@{ Name = 'b'; RunnerStuck = $false }, [pscustomobject]@{ Name = 'c' })).Count

# Slice 301 (one design): Resolve-MaestroEnd is the one exit path. A tree still alive after a NATURAL end (not killed) is stuck too; one that ends is not.
$killsSeen = New-Object System.Collections.ArrayList
$aliveTree = [pscustomobject]@{ Id = 555; ExitCode = 0 }
$aliveTree | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value { param([int]$Milliseconds) Start-Sleep -Milliseconds 20; return $false }
$endNatural = Resolve-MaestroEnd $aliveTree $false 2 1({ param($ProcessId) [void]$killsSeen.Add($ProcessId) }.GetNewClosure())
Assert-Equal 'end: a tree alive after a natural exit is RunnerStuck' $true $endNatural.RunnerStuck
Assert-Equal 'end: ... with its pid, exit code -1, and a re-kill tried' $true ($endNatural.StuckPid -eq 555 -and $endNatural.ExitCode -eq -1 -and $killsSeen.Count -ge 1 -and -not $endNatural.Killed)
$endAfterKill = Resolve-MaestroEnd $aliveTree $true 1 1 ({ param($ProcessId) }.GetNewClosure())
Assert-Equal 'end: a tree alive after the crash-kill is RunnerStuck, not a crash state' $true ($endAfterKill.RunnerStuck -and $endAfterKill.Killed -and $endAfterKill.ExitCode -eq -1)
$goneTree = [pscustomobject]@{ Id = 556; ExitCode = 3 }
$goneTree | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value { param([int]$Milliseconds) return $true }
$endGone = Resolve-MaestroEnd $goneTree $false 1 1
Assert-Equal 'end: a tree that ended gives its exit code and is not stuck' $true (-not $endGone.RunnerStuck -and $endGone.ExitCode -eq 3 -and $endGone.StuckPid -eq 0)
Assert-Equal 'end: the predicate sees the stuck result' $true (Test-RunnerStuck $endNatural)
Assert-Equal 'end: ... and not the clean one, nor $null, nor a result without the property' $false ((Test-RunnerStuck $endGone) -or (Test-RunnerStuck $null) -or (Test-RunnerStuck ([pscustomobject]@{ Name = 'x' })))

# Slice 301 item 2: a flow that crashes once leaves two logs (attempt 1 with the crash, attempt 2), and the summary names both paths
$logDir = Join-Path ([IO.Path]::GetTempPath()) ("rtst-attempt-" + [guid]::NewGuid().ToString('N'))
try {
    $null = New-Item -ItemType Directory -Force $logDir
    Set-Content (Join-Path $logDir 'dino-f.log') 'RTST-TEST first run line'
    $log1 = Save-AttemptLog $logDir 'dino' 'f' 1 'F libc: Fatal signal 11 (SIGSEGV)'
    Set-Content (Join-Path $logDir 'dino-f.log') 'RTST-TEST second run line'   # the retry overwrites the flow's own log
    $log2 = Save-AttemptLog $logDir 'dino' 'f' 2 ''
    Assert-Equal 'attempt logs: two files left' 2 @(Get-ChildItem $logDir -Filter 'dino-f.attempt*.log').Count
    Assert-Equal 'attempt logs: attempt 1 keeps the crash' $true ("$(Get-Content $log1 -Raw)" -like '*Fatal signal 11*first run line*' -or "$(Get-Content $log1 -Raw)" -like '*first run line*Fatal signal 11*')
    Assert-Equal 'attempt logs: attempt 2 has the second run, not the crash' $true ("$(Get-Content $log2 -Raw)" -like '*second run line*' -and "$(Get-Content $log2 -Raw)" -notlike '*Fatal signal*')
    $lines = @(Format-RetryLogLines @([pscustomobject]@{ Name = 'f'; RetryLogs = @($log1, $log2) }, [pscustomobject]@{ Name = 'g'; RetryLogs = @() }))
    Assert-Equal 'attempt logs: one summary line for the retried flow' 1 $lines.Count
    Assert-Equal 'attempt logs: the line prints both paths' $true ($lines[0].Contains($log1) -and $lines[0].Contains($log2))
} finally { Remove-Item $logDir -Recurse -Force -ErrorAction SilentlyContinue }

# Slice 301 review: the kill is issued again partway, stale attempt logs are cleared, and the retry glue (Invoke-CrashRetryFlow) is driven with stubbed runs
$script:killCalls = 0
$slow = [pscustomobject]@{ Id = 7 }
$slow | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value { param([int]$Milliseconds) Start-Sleep -Milliseconds 50; return $false }
Assert-Equal 'kill-wait: stuck process gives false after retries' $false (Wait-AfterCrashKill $slow 3 1 { param($ProcessId) $script:killCalls++ })
Assert-Equal 'kill-wait: the kill is re-issued between slices (3 s, every 1 s = 2 re-kills)' 2 $script:killCalls
$retryDir = Join-Path ([IO.Path]::GetTempPath()) ("rtst-retry-" + [guid]::NewGuid().ToString('N'))
try {
    $null = New-Item -ItemType Directory -Force $retryDir
    Set-Content (Join-Path $retryDir 'dino-f.attempt1.log') 'stale'
    Set-Content (Join-Path $retryDir 'dino-f.attempt2.log') 'stale'
    $null = Save-AttemptLog $retryDir 'dino' 'f' 1 'fresh crash'
    Assert-Equal 'attempt logs: a stale attempt 2 is cleared when attempt 1 is saved' 1 @(Get-ChildItem $retryDir -Filter 'dino-f.attempt*.log').Count
    $crashed = { [pscustomobject]@{ Name = 'f'; Seconds = 2.0; Problem = 'boom'; Crash = @('F libc: Fatal signal 11'); CrashLog = 'F libc: Fatal signal 11'; Phases = $null; Retried = $null; FirstRunSeconds = 0.0; RunnerStuck = $false; StuckPid = 0; RetryLogs = @() } }
    $script:runs = 0
    $r = Invoke-CrashRetryFlow 'f' $retryDir 'dino' { $script:runs++; & $crashed } { param($l) $true }
    Assert-Equal 'retry glue: a retryable crash runs twice' 2 $script:runs
    Assert-Equal 'retry glue: the row is marked retried with both logs and both times' $true ($r.Retried -and @($r.RetryLogs).Count -eq 2 -and $r.Seconds -eq 4.0 -and $r.FirstRunSeconds -eq 2.0)
    $script:runs = 0
    $r = Invoke-CrashRetryFlow 'f' $retryDir 'dino' { $script:runs++; $x = & $crashed; $x.RunnerStuck = $true; $x.StuckPid = 77; $x } { param($l) $true }
    Assert-Equal 'retry glue: a crash whose Maestro tree is stuck is not retried' 1 $script:runs
    Assert-Equal 'retry glue: ... and keeps RunnerStuck for the report' $true (Test-RunnerStuck $r)
    Assert-Equal 'retry glue: ... and the run stop text is the stuck text, not an app crash' 'runner stuck: Maestro tree 77 would not exit' (Format-RunStopLine 'f' (Get-RunStopReason $r))
    $script:runs = 0
    $r = Invoke-CrashRetryFlow 'f' $retryDir 'dino' { $script:runs++; $x = & $crashed; if ($script:runs -ge 2) { $x.RunnerStuck = $true; $x.StuckPid = 88 }; $x } { param($l) $true }
    Assert-Equal 'retry glue: a stuck attempt 2 ends it after two runs (no third)' 2 $script:runs
    Assert-Equal 'retry glue: ... the result carries RunnerStuck and the stuck stop reason' $true ((Test-RunnerStuck $r) -and (Get-RunStopReason $r) -eq 'runner stuck: Maestro tree 88 would not exit')
    $script:runs = 0
    $r = Invoke-CrashRetryFlow 'f' $retryDir 'dino' { $script:runs++; & $crashed } { param($l) $false }
    Assert-Equal 'retry glue: a non-Mono crash is not retried' 1 $script:runs
    $script:runs = 0
    $r = Invoke-CrashRetryFlow 'f' $retryDir 'dino' { $script:runs++; $x = & $crashed; $x.Problem = $null; $x } { param($l) $true }
    Assert-Equal 'retry glue: a passing flow runs once' 1 $script:runs
    $script:runs = 0
    $r = Invoke-CrashRetryFlow 'f' $retryDir 'dino' { $script:runs++; & $crashed } { param($l) $true }
    Assert-Equal 'retry glue: a second failure names the first attempt crash' $true ($r.Problem -like 'First attempt:*Fatal signal 11*boom*')
} finally { Remove-Item $retryDir -Recurse -Force -ErrorAction SilentlyContinue }

# Slice 301 review: run-stop reason (a crash, or a Maestro that outlived the kill, ends the run)
Assert-Equal 'stop: a clean result goes on' '' (Get-RunStopReason ([pscustomobject]@{ Crash = @(); RunnerStuck = $false }))
Assert-Equal 'stop: a crash stops with its line' 'F libc: Fatal signal 11' (Get-RunStopReason ([pscustomobject]@{ Crash = @('F libc: Fatal signal 11'); RunnerStuck = $false }))
Assert-Equal 'stop: a stuck tree stops even with no crash line' 'runner stuck: Maestro tree 5 would not exit' (Get-RunStopReason ([pscustomobject]@{ Crash = @(); RunnerStuck = $true; StuckPid = 5 }))
Assert-Equal 'stop: a stuck tree wins over a crash line, and is never called an app crash' 'runner stuck: Maestro tree 5 would not exit' (Format-RunStopLine 'f' (Get-RunStopReason ([pscustomobject]@{ Crash = @('F libc: x'); RunnerStuck = $true; StuckPid = 5 })))
Assert-Equal 'stop: a crash is named as the app crash' 'app crashed in f: F libc: x' (Format-RunStopLine 'f' 'F libc: x')
Assert-Equal 'stop: the summary says runner stuck when a tree is stuck' 'dino: 0 passed, 1 failed, 1 not run (runner stuck) in 1.0 s.' (Format-FlowsSummary 'dino' @([pscustomobject]@{ Problem = 'p'; NotRun = $null; RunnerStuck = $true; StuckPid = 5 }, [pscustomobject]@{ Problem = $null; NotRun = 'runner stuck: x' }) 1.0)

# Slice 301 review: the REAL Invoke-FlowCrashRetry from run-flows.ps1 (which cannot be dot-sourced) is parsed out and run with a stub Invoke-GuardedFlow defined in this script's scope,
# so a closure that cannot see a script-scope function fails here ("term not recognized") instead of on the first real flow.
$flowsAst = [System.Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'run-flows.ps1'), [ref]$null, [ref]$null)
$retryFn = $flowsAst.Find({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Invoke-FlowCrashRetry' }, $true)
Assert-Equal 'wiring: Invoke-FlowCrashRetry found in run-flows.ps1' $true ($null -ne $retryFn)
Invoke-Expression $retryFn.Extent.Text
$script:guardedCalls = @()
function Invoke-GuardedFlow([string]$Maestro, $File) {
    $script:guardedCalls += "$Maestro|$($File.BaseName)"
    return [pscustomobject]@{ Name = $File.BaseName; Seconds = 1.0; Problem = $null; Crash = @(); CrashLog = ''; Phases = $null; Retried = $null; FirstRunSeconds = 0.0; RunnerStuck = $false; StuckPid = 0; RetryLogs = @() }
}
$packages = @{ dino = 'uk.test' }
$App = 'dino'
$Instance = 1
$script:StateDir = [IO.Path]::GetTempPath()
$wired = Invoke-FlowCrashRetry 'maestro-stub' ([System.IO.FileInfo]::new((Join-Path $PSScriptRoot 'common.ps1')))
Assert-Equal 'wiring: the closure reaches the script-scope guarded flow' 'maestro-stub|common' ($script:guardedCalls -join ',')
Assert-Equal 'wiring: the flow result comes back' 'common' $wired.Name

# The scoping failure shows only when the code runs under -Command (tools/CLAUDE.md, Slice 253 item 5), so the same real function is run once more in a child process started that way.
$childShell = (Get-Process -Id $PID).Path
$childCode = ". '$((Join-Path $PSScriptRoot 'common.ps1'))'; function Invoke-GuardedFlow(`$Maestro, `$File) { [pscustomobject]@{ Name = 'ok' } }; $($retryFn.Extent.Text); `$packages = @{ dino = 'x' }; `$App = 'dino'; `$Instance = 1; `$script:StateDir = [IO.Path]::GetTempPath(); (Invoke-FlowCrashRetry 'm' ([System.IO.FileInfo]::new('x.txt'))).Name"
$childOut = (& $childShell -NoProfile -Command $childCode 2>&1 | Out-String).Trim()
Assert-Equal 'wiring: the real Invoke-FlowCrashRetry works under -Command too' 'ok' ($childOut -split "`n" | Select-Object -Last 1).Trim()

# Slice 301: attempt 2's log is written only from a flow log that run 2 saved. Run 2 saves none here (the glue's stubbed runs touch no file), so no attempt-2 log exists.
$missDir = Join-Path ([IO.Path]::GetTempPath()) ("rtst-miss-" + [guid]::NewGuid().ToString('N'))
try {
    $null = New-Item -ItemType Directory -Force $missDir
    $script:missRuns = 0
    $runWritesLogOnce = { $script:missRuns++; if ($script:missRuns -eq 1) { Start-Sleep -Milliseconds 60; Set-Content (Join-Path $missDir 'dino-f.log') 'run one saved' }; [pscustomobject]@{ Name = 'f'; Seconds = 1.0; Problem = 'boom'; Crash = @('F libc: Fatal signal 11'); CrashLog = 'F libc: Fatal signal 11'; Phases = $null; Retried = $null; FirstRunSeconds = 0.0; RunnerStuck = $false; StuckPid = 0; RetryLogs = @() } }
    $missResult = Invoke-CrashRetryFlow 'f' $missDir 'dino' $runWritesLogOnce { param($l) $true }
    Assert-Equal 'attempt logs: run 1 saved a log, so attempt 1 is kept' $true ($null -ne $missResult.RetryLogs[0] -and (Test-Path $missResult.RetryLogs[0]))
    Assert-Equal 'attempt logs: run 2 saved none, so no attempt-2 log exists and its slot is empty' $true ($null -eq $missResult.RetryLogs[1] -and -not (Test-Path (Join-Path $missDir 'dino-f.attempt2.log')))
    Assert-Equal 'attempt logs: the summary says (not kept) for it' $true ((@(Format-RetryLogLines @($missResult))[0]) -like '*and (not kept)')
} finally { Remove-Item $missDir -Recurse -Force -ErrorAction SilentlyContinue }

# Slice 301: the real Invoke-GuardedFlow from run-flows.ps1 never launches a second Maestro (the health-guard rerun) after a stuck first run.
$guardFn = $flowsAst.Find({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Invoke-GuardedFlow' }, $true)
Assert-Equal 'wiring: Invoke-GuardedFlow found in run-flows.ps1' $true ($null -ne $guardFn)
Remove-Item Function:\Invoke-GuardedFlow
Invoke-Expression $guardFn.Extent.Text
$script:flowLaunches = 0
function Confirm-EmulatorHealthy([string]$When) { return $false }   # a dead emulator: without the predicate the guard would run the flow again
function Get-EmulatorHealthProblem { return 'dead' }
function Invoke-Flow([string]$Maestro, $File) { $script:flowLaunches++; return [pscustomobject]@{ Name = 'f'; Problem = 'p'; Crash = @(); RunnerStuck = $true; StuckPid = 31 } }
$script:UnhealthyCount = 0
$guarded = Invoke-GuardedFlow 'm' ([System.IO.FileInfo]::new('x.txt'))
Assert-Equal 'guarded: a stuck first run is returned with no rerun (one launch)' 1 $script:flowLaunches
Assert-Equal 'guarded: ... still stuck' $true (Test-RunnerStuck $guarded)

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-speed: all passed.'
exit 0
