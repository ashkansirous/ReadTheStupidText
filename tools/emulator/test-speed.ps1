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

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-speed: all passed.'
