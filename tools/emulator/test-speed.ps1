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

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-speed: all passed.'
