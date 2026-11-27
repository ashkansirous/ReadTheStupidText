<#
.SYNOPSIS
  Self-test for run-flows.ps1 and flows-lib.ps1 (Slice 448): flow-name lists, flow lookup, the refusals (wrong appId, network cut), expected log lines, the Maestro command line,
  the debuggable-install read, the failure lines, the budget clock, the exit code, and a -DryRun of the real Slice 396 flows that must touch no phone. Run it in both `powershell` (5.1) and `pwsh`. Needs no device.
#>
$ErrorActionPreference = 'Stop'
if (-not $env:ANDROID_HOME -and -not $env:ANDROID_SDK_ROOT) { $env:ANDROID_HOME = $env:TEMP }
. (Join-Path $PSScriptRoot '..\emulator\common.ps1')
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'flows-lib.ps1')

$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

# --- names
Assert-Equal 'name list' 'a|b|c' ((ConvertTo-FlowNameList @('a.yaml, b', 'a', '.maestro/dino/c.yaml')) -join '|')
Assert-Equal 'empty names' 0 @(ConvertTo-FlowNameList @('', ' ')).Count

# --- lookup in a throw-away flow folder
$root = Join-Path ([IO.Path]::GetTempPath()) ("rtst-run-flows-test-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
$null = New-Item -ItemType Directory -Force (Join-Path $root 'needs-file')
try {
    Set-Content -Path (Join-Path $root 'one.yaml') -Value "appId: uk.sirous.dinoreader.dbg`n---`n- launchApp"
    Set-Content -Path (Join-Path $root 'needs-file\two.yaml') -Value "appId: uk.sirous.dinoreader.dbg`n---`n- launchApp"
    $request = Resolve-FlowRequest $root @('one', 'two', 'nope')
    Assert-Equal 'found files' 'one|two' (($request.Files | ForEach-Object { $_.BaseName }) -join '|')
    Assert-Equal 'missing names' 'nope' ($request.Missing -join '|')
} finally { Remove-Item -Recurse -Force $root -ErrorAction SilentlyContinue }

# --- refusals
$ok = "# expect-log: HOOK a ok`n# expect-log: [X] y=1`nappId: uk.sirous.dinoreader.dbg`nname: n`n---`n- launchApp"
Assert-Equal 'dino flow on dino' $null (Get-FlowRefusal $ok 'dino')
Assert-Equal 'app id read' 'uk.sirous.dinoreader.dbg' (Get-FlowAppId $ok)
Assert-Equal 'rtst app refuses a dino flow' $true ([bool](Get-FlowRefusal $ok 'rtst'))
Assert-Equal 'store package refused' $true ([bool](Get-FlowRefusal "appId: uk.sirous.dinoreader`n---" 'dino'))
Assert-Equal 'no appId refused' $true ([bool](Get-FlowRefusal "name: x`n---" 'dino'))
Assert-Equal 'quoted appId read' 'uk.sirous.dinoreader.dbg' (Get-FlowAppId "appId: `"uk.sirous.dinoreader.dbg`"`n---")
Assert-Equal 'appId with comment read' 'uk.sirous.dinoreader.dbg' (Get-FlowAppId "appId: uk.sirous.dinoreader.dbg  # dev`r`n---")
Assert-Equal 'quoted appId runs' $null (Get-FlowRefusal "appId: 'uk.sirous.dinoreader.dbg' # dev`n---" 'dino')
Assert-Equal 'rtst flow with the plain package runs' $null (Get-FlowRefusal "appId: uk.sirous.readthestupidtext`nname: n`n---`n- launchApp" 'rtst')
Assert-Equal 'rtst flow with the dev package runs' $null (Get-FlowRefusal "appId: uk.sirous.readthestupidtext.dbg`n---" 'rtst')
Assert-Equal 'dino app refuses the plain rtst package' $true ([bool](Get-FlowRefusal "appId: uk.sirous.readthestupidtext`n---" 'dino'))
Assert-Equal 'appId swapped' "appId: uk.sirous.readthestupidtext.dbg`nname: n`n---" (Set-FlowAppId "appId: uk.sirous.readthestupidtext`nname: n`n---" 'uk.sirous.readthestupidtext.dbg')
Assert-Equal 'review 1: only the first appId line is swapped' "appId: new`n---`nappId: uk.other" (Set-FlowAppId "appId: old`n---`nappId: uk.other" 'new')
Assert-Equal 'review 1: an APPID line in another case is left alone' "appId: new`n# APPID: x`nAppId: y" (Set-FlowAppId "appId: old`n# APPID: x`nAppId: y" 'new')
$fwdBefore = @('R3 tcp:7001 tcp:7001', 'other tcp:9 tcp:9')
$fwdAfter = @('R3 tcp:7001 tcp:7001', 'R3 tcp:51234 tcp:7001', 'other tcp:9 tcp:9', 'other tcp:10 tcp:10')
Assert-Equal 'review 3: only the forwards this run made on this phone are removed' 'tcp:51234' ((Get-NewForwardSpecs $fwdBefore $fwdAfter 'R3') -join ',')
# --- check4 1 + 2: forwards are parsed one per line; a list that could not be read removes nothing
Assert-Equal 'check4: two other-session forwards stay when a new one appears' 'tcp:5' ((Get-NewForwardSpecs @('R3 tcp:1 tcp:2', 'R3 tcp:3 tcp:4') @('R3 tcp:1 tcp:2', 'R3 tcp:3 tcp:4', 'R3 tcp:5 tcp:6') 'R3') -join ',')
Assert-Equal 'check4: a failed before-list ($null) removes no forward' 0 @(Get-NewForwardSpecs $null @('R3 tcp:1 tcp:2') 'R3').Count
Assert-Equal 'check4: a failed after-list ($null) removes no forward' 0 @(Get-NewForwardSpecs @('R3 tcp:1 tcp:2') $null 'R3').Count
Assert-Equal 'check4: an empty but read before-list makes every forward new' 'tcp:1' ((Get-NewForwardSpecs @() @('R3 tcp:1 tcp:2') 'R3') -join ',')
Assert-Equal 'check4: parsed specs' 'R3|tcp:1|tcp:2' ((ConvertTo-ForwardSpecs @('R3 tcp:1 tcp:2', 'junk')) | ForEach-Object { "$($_.Serial)|$($_.Local)|$($_.Remote)" })
function Invoke-Adb([string]$Serial, [string[]]$Arguments, [int]$TimeoutSeconds = 0) {
    if ($script:FakeAdbThrows) { throw 'Phone not responding over adb' }
    $global:LASTEXITCODE = $script:FakeAdbExit
    return @('R3 tcp:1 tcp:2', 'R3 tcp:3 tcp:4')
}
$script:FakeAdbThrows = $false; $script:FakeAdbExit = 0
$fakeLines = Get-AdbForwardLines 'R3'
Assert-Equal 'check4: each forward is its own line' 2 @($fakeLines).Count
Assert-Equal 'check4: the first forward is kept whole' 'R3 tcp:1 tcp:2' @($fakeLines)[0]
$script:FakeAdbExit = 1
Assert-Equal 'check4: a non-zero adb exit is $null, not an empty list' $true ($null -eq (Get-AdbForwardLines 'R3'))
$script:FakeAdbExit = 0; $script:FakeAdbThrows = $true
Assert-Equal 'check4: an adb timeout is $null, not an empty list' $true ($null -eq (Get-AdbForwardLines 'R3'))
$script:FakeAdbThrows = $false
Remove-Item Function:\Invoke-Adb
. (Join-Path $PSScriptRoot 'common.ps1')
$cellular ="# needs: voice, cellular-only`nappId: uk.sirous.dinoreader.dbg`n---"
Assert-Equal 'cellular-only refused' $true ([bool](Get-FlowRefusal $cellular 'dino'))
Assert-Equal 'voice need allowed' $null (Get-FlowRefusal "# needs: voice`nappId: uk.sirous.dinoreader.dbg`n---" 'dino')

# --- review check2 1: a network step refuses a flow by its text and through runFlow, with no header
$airRoot = Join-Path ([IO.Path]::GetTempPath()) ("rtst-air-test-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
$null = New-Item -ItemType Directory -Force $airRoot
try {
    $head = "appId: uk.sirous.dinoreader.dbg`n---`n"
    Set-Content -Path (Join-Path $airRoot 'inner.yaml') -Value "$head- setAirplaneMode: enabled"
    Set-Content -Path (Join-Path $airRoot 'mid.yaml') -Value "$head- runFlow:`n    file: inner.yaml"
    Set-Content -Path (Join-Path $airRoot 'loop.yaml') -Value "$head- runFlow: loop.yaml"
    Set-Content -Path (Join-Path $airRoot 'clean.yaml') -Value "$head# toggleAirplaneMode is not run here`n- launchApp"
    $direct = "$head- toggleAirplaneMode"
    Assert-Equal 'check2: direct toggleAirplaneMode refused' $true ([bool](Get-FlowRefusal $direct 'dino'))
    Assert-Equal 'check2: refusal names the step' $true ((Get-FlowRefusal $direct 'dino') -like "*toggleAirplaneMode*")
    Assert-Equal 'check2: a commented step is not a step' $null (Get-FlowRefusal (Get-Content -Raw (Join-Path $airRoot 'clean.yaml')) 'dino' (Join-Path $airRoot 'clean.yaml'))
    $midPath = Join-Path $airRoot 'mid.yaml'
    Assert-Equal 'check2: a runFlow sub-flow with setAirplaneMode refuses' $true ([bool](Get-FlowRefusal (Get-Content -Raw $midPath) 'dino' $midPath))
    Assert-Equal 'check2: the includes are not read without a path' $null (Get-FlowRefusal (Get-Content -Raw $midPath) 'dino')
    $loopPath = Join-Path $airRoot 'loop.yaml'
    Assert-Equal 'check2: a runFlow loop ends' $null (Get-FlowRefusal (Get-Content -Raw $loopPath) 'dino' $loopPath)
    # review check3 3: svc data / svc wifi / airplane in the flow, and in runScript files
    Assert-Equal 'check3: svc data in the flow refused' $true ([bool](Get-FlowRefusal "$head- runScript: x`n- evalScript: `${exec('adb shell svc data disable')}" 'dino'))
    Assert-Equal 'check3: svc wifi in the flow refused' $true ([bool](Get-FlowRefusal "$head# adb shell svc wifi disable is a comment`n- inputText: svc wifi disable" 'dino'))
    Assert-Equal 'check3: a commented svc wifi is not a step' $null (Get-FlowRefusal "$head# adb shell svc wifi disable`n- launchApp" 'dino')
    Assert-Equal 'check3: any airplane word refused' $true ([bool](Get-FlowRefusal "$head- launchApp`n- airplane-mode-on" 'dino'))
    Set-Content -Path (Join-Path $airRoot 'net-off.js') -Value "var r = shell('svc data disable');"
    Set-Content -Path (Join-Path $airRoot 'ok.js') -Value "var r = 1;"
    Set-Content -Path (Join-Path $airRoot 'script.yaml') -Value "$head- runScript: net-off.js"
    Set-Content -Path (Join-Path $airRoot 'script-ok.yaml') -Value "$head- runScript: ok.js"
    Set-Content -Path (Join-Path $airRoot 'script-file.yaml') -Value "$head- runScript:`n    file: net-off.js"
    $scriptPath = Join-Path $airRoot 'script.yaml'
    Assert-Equal 'check3: a runScript file with svc data refuses' $true ([bool](Get-FlowRefusal (Get-Content -Raw $scriptPath) 'dino' $scriptPath))
    $scriptFilePath = Join-Path $airRoot 'script-file.yaml'
    Assert-Equal 'check3: a runScript file: form refuses' $true ([bool](Get-FlowRefusal (Get-Content -Raw $scriptFilePath) 'dino' $scriptFilePath))
    $scriptOkPath = Join-Path $airRoot 'script-ok.yaml'
    Assert-Equal 'check3: a harmless runScript file runs' $null (Get-FlowRefusal (Get-Content -Raw $scriptOkPath) 'dino' $scriptOkPath)
    # check4 3 + 4: an include that cannot be resolved fails closed; more radio tokens
    Set-Content -Path (Join-Path $airRoot 'var-include.yaml') -Value "$head- runFlow: `${SUB}.yaml"
    Set-Content -Path (Join-Path $airRoot 'missing-include.yaml') -Value "$head- runFlow: nope.yaml"
    Set-Content -Path (Join-Path $airRoot 'abs-include.yaml') -Value "$head- runScript: $(Join-Path $airRoot 'ok.js')"
    Set-Content -Path (Join-Path $airRoot 'inline-include.yaml') -Value "$head- runFlow: { file: nope.yaml, when: { visible: x } }"
    Set-Content -Path (Join-Path $airRoot 'nested-missing.yaml') -Value "$head- runFlow: mid-missing.yaml"
    Set-Content -Path (Join-Path $airRoot 'mid-missing.yaml') -Value "$head- runScript: gone.js"
    foreach ($case in 'var-include', 'missing-include', 'inline-include', 'nested-missing') {
        $casePath = Join-Path $airRoot "$case.yaml"
        Assert-Equal "check4: $case is refused as an unresolved include" $true ((Get-FlowRefusal (Get-Content -Raw $casePath) 'dino' $casePath) -like 'unresolved include*')
    }
    $absPath = Join-Path $airRoot 'abs-include.yaml'
    Assert-Equal 'check4: an absolute include that exists is read and runs' $null (Get-FlowRefusal (Get-Content -Raw $absPath) 'dino' $absPath)
    foreach ($token in 'adb shell cmd wifi set-wifi-enabled disabled', 'adb shell cmd phone data disable', 'adb shell settings put global wifi_on 0', 'adb shell settings put global mobile_data 0') {
        Assert-Equal "check4: '$token' refused" $true ([bool](Get-FlowRefusal "$head- evalScript: `${exec('$token')}" 'dino'))
    }
    Set-Content -Path (Join-Path $airRoot 'orient-inner.yaml') -Value "$head- setOrientation: PORTRAIT"
    Set-Content -Path (Join-Path $airRoot 'orient-outer.yaml') -Value "$head- runFlow: orient-inner.yaml"
    $orientPath = Join-Path $airRoot 'orient-outer.yaml'
    Assert-Equal 'check3: setOrientation inside a sub-flow is found' $true (Test-FlowChangesOrientation (Get-Content -Raw $orientPath) $orientPath)
} finally { Remove-Item -Recurse -Force $airRoot -ErrorAction SilentlyContinue }
$repoMaestro = Join-Path $PSScriptRoot '..\..\.maestro\rtst'
foreach ($name in 'translation-wait-cancel', 'translation-waits-for-internet') {
    $path = Join-Path $repoMaestro "$name.yaml"
    if (Test-Path $path) { Assert-Equal "check2: real flow $name refused" $true ([bool](Get-FlowRefusal (Get-Content -Raw $path) 'rtst' $path)) }
}

# --- review check2 2: the rotation restore helper
$savedRotation = @{ accelerometer_rotation = '1'; user_rotation = $null }
$restore = @(Get-RotationRestoreCommands $savedRotation)
Assert-Equal 'check2: a saved value is put back' 'shell settings put system accelerometer_rotation 1' ($restore[0] -join ' ')
Assert-Equal 'check2: a setting that had no value is deleted' 'shell settings delete system user_rotation' ($restore[1] -join ' ')
Assert-Equal 'check2: an unread setting is left alone' 0 @(Get-RotationRestoreCommands @{}).Count
Assert-Equal 'check2: setting value read' '2' (ConvertTo-SettingValue "2`r`n")
Assert-Equal 'check2: null output is no value' $null (ConvertTo-SettingValue "null`n")
# --- review check3 1, 2: one behaviour: only a readable setting is ever restored or deleted
Assert-Equal 'check3: a number is a value' 'value:2' ((Get-SettingReading "2`r`n" 0) | ForEach-Object { "$($_.State):$($_.Value)" })
Assert-Equal 'check3: exactly null is unset' 'unset' (Get-SettingReading "null`n" 0).State
Assert-Equal 'check3: an adb error text is unreadable, not unset' 'unreadable' (Get-SettingReading 'error: device offline' 0).State
Assert-Equal 'check3: a service error is unreadable' 'unreadable' (Get-SettingReading "cmd: Can't find service: settings" 0).State
Assert-Equal 'check3: a non-zero exit is unreadable even with a number' 'unreadable' (Get-SettingReading '1' 1).State
Assert-Equal 'check3: empty output is unreadable' 'unreadable' (Get-SettingReading '' 0).State
Assert-Equal 'check3: a failed read leaves no restore command' 0 @(Get-RotationRestoreCommands @{}).Count
Assert-Equal 'check3: warn for a put' 'WARN rotation not restored: user_rotation=1' (Format-RotationWarn @('shell', 'settings', 'put', 'system', 'user_rotation', '1'))
Assert-Equal 'check3: warn for a delete' 'WARN rotation not restored: user_rotation=null' (Format-RotationWarn @('shell', 'settings', 'delete', 'system', 'user_rotation'))
Assert-Equal 'check3: a failed restore makes a green run non-green' 1 (Get-RunExitCodeAfterRestore 0 $true)
Assert-Equal 'check3: a failed restore keeps a red code' 1 (Get-RunExitCodeAfterRestore 1 $true)
Assert-Equal 'check3: a clean restore keeps the code' 0 (Get-RunExitCodeAfterRestore 0 $false)
Assert-Equal 'check3: setOrientation is found' $true (Test-FlowChangesOrientation "appId: a`n---`n- setOrientation: LANDSCAPE_LEFT" '')
Assert-Equal 'check3: a commented setOrientation is not' $false (Test-FlowChangesOrientation "appId: a`n---`n# setOrientation: x`n- launchApp" '')

# --- expect-log, command line
Assert-Equal 'expected logs' 'HOOK a ok|[X] y=1' ((Get-FlowExpectedLogs $ok) -join '|')
Assert-Equal 'maestro args' '--device R3CR30FQT0V test --no-reinstall-driver --debug-output "C:\o" "C:\f\a.yaml"' (Get-MaestroFlowArguments 'R3CR30FQT0V' 'C:\o' 'C:\f\a.yaml')
Assert-Equal 'device before test' $true ((Get-MaestroFlowArguments 's' 'o' 'f').IndexOf('--device') -lt (Get-MaestroFlowArguments 's' 'o' 'f').IndexOf(' test '))

# --- installed build, first-run stop
Assert-Equal 'debuggable' $true (Test-DebuggableInstall "    pkgFlags=[ DEBUGGABLE HAS_CODE ALLOW_CLEAR_USER_DATA ]`n    flags=[ DEBUGGABLE HAS_CODE ALLOW_CLEAR_USER_DATA ]")
Assert-Equal 'release build' $false (Test-DebuggableInstall "    flags=[ HAS_CODE ALLOW_CLEAR_USER_DATA ]")

# --- check4: the FirstRunLanguage hint is gone (a flow may wait for that page on purpose); the failure line shows Maestro's own step lines, never JVM warnings
Assert-Equal 'check4: no first-run hint function' 0 @(Get-Command 'Test-StoppedAtFirstRun', 'Format-FirstRunHint' -ErrorAction SilentlyContinue).Count
$jvm = "WARNING: A terminally deprecated method in sun.misc.Unsafe has been called`nWARNING: sun.misc.Unsafe::objectFieldOffset will be removed`n"
$esc = [string][char]27
$failText = "$jvm$esc[31mAssertion is false: id: StoryTile is visible$esc[0m`nFlow failed"
Assert-Equal 'check4: JVM warnings are not the failure line' 'Assertion is false: id: StoryTile is visible | Flow failed' (Get-MaestroFailureLines $failText 4)
Assert-Equal 'check4: only warnings gives nothing, not the warnings' '' (Get-MaestroFailureLines $jvm 4)
Assert-Equal 'check4: no step word falls back to the last lines kept' 'two | three' (Get-MaestroFailureLines "WARNING: x`none`ntwo`nthree" 2)
Assert-Equal 'check4: a waited-for FirstRunLanguage screen adds no hint' $false ((Get-MaestroFailureLines 'Assertion is false: id: FirstRunLanguage is visible' 4) -match 'STOPPED AT')

# --- budget and exit code
$start = [datetime]'2026-01-01T10:00:00'
Assert-Equal 'seconds left' 570 (Get-FlowSecondsLeft $start $start.AddSeconds(30) 10)
Assert-Equal 'seconds left never negative' 0 (Get-FlowSecondsLeft $start $start.AddMinutes(11) 10)
Assert-Equal 'exit all pass' 0 (Get-FlowRunExitCode @('PASS', 'PASS'))
Assert-Equal 'exit a fail' 1 (Get-FlowRunExitCode @('PASS', 'FAIL', 'SKIP'))
Assert-Equal 'exit a timeout' 1 (Get-FlowRunExitCode @('TIMEOUT'))
Assert-Equal 'exit a skip' 3 (Get-FlowRunExitCode @('PASS', 'SKIP'))
Assert-Equal 'exit nothing ran' 3 (Get-FlowRunExitCode @())
Assert-Equal 'empty flow list is an empty plan' 0 @(ConvertTo-FlowNameList @(',', ' ')).Count
Assert-Equal 'result line' 'PASS    a (1.5 s)' (Format-FlowResultLine 'PASS' 'a' 1.5)
Assert-Equal 'result line detail' 'FAIL    a (2.0 s): why' (Format-FlowResultLine 'FAIL' 'a' 2 'why')

# --- the JDK pick is the emulator's own (Slice 389), not a copy
Assert-Equal 'run-flows uses Set-ProcessJavaHome' $true ((Get-Content -Raw (Join-Path $PSScriptRoot 'run-flows.ps1')) -match 'Set-ProcessJavaHome')

# --- -DryRun of the four Slice 396 flows: exit 0, four RUN lines, nothing touched
$engine = (Get-Process -Id $PID).Path
$flows = 'store-restore,paid-books,subscription,store-payment-required'
$lines = @(& $engine -NoProfile -File (Join-Path $PSScriptRoot 'run-flows.ps1') -App dino -Flow $flows -DryRun 2>&1 | ForEach-Object { "$_" })
Assert-Equal 'dry run exit' 0 $LASTEXITCODE
Assert-Equal 'dry run RUN lines' 4 @($lines | Where-Object { $_ -match '^\s+RUN\s' }).Count
Assert-Equal 'dry run says nothing touched' $true ([bool]($lines -match 'DRY RUN: nothing touched'))
$missingLines = @(& $engine -NoProfile -File (Join-Path $PSScriptRoot 'run-flows.ps1') -App dino -Flow 'no-such-flow' -DryRun 2>&1 | ForEach-Object { "$_" })
Assert-Equal 'unknown flow is a SKIP line' $true ([bool]($missingLines -match 'SKIP no-such-flow: no flow'))

# --- review 3: -App rtst takes the RTST flows (they declare the package without .dbg)
$rtstLines = @(& $engine -NoProfile -File (Join-Path $PSScriptRoot 'run-flows.ps1') -App rtst -Flow 'first-run' -DryRun 2>&1 | ForEach-Object { "$_" })
Assert-Equal 'rtst dry run exit' 0 $LASTEXITCODE
Assert-Equal 'rtst flow is planned, not skipped' 1 @($rtstLines | Where-Object { $_ -match '^\s+RUN\s+first-run' }).Count

$emptyLines =@(& $engine -NoProfile -File (Join-Path $PSScriptRoot 'run-flows.ps1') -App dino -Flow ',' -DryRun 2>&1 | ForEach-Object { "$_" })
Assert-Equal 'empty plan exits 3, not green' 3 $LASTEXITCODE

# --- review 1: the emulator common.ps1 overwrites $script:Serial in the script scope, so the requested serial is copied before it is dot-sourced.
# Behaviour probe: the same two dot-sources in a child script given -Serial must still show the requested value in the saved copy.
$probe = Join-Path ([IO.Path]::GetTempPath()) ("rtst-serial-probe-" + [guid]::NewGuid().ToString('N').Substring(0, 8) + '.ps1')
try {
    $emuCommon = Join-Path $PSScriptRoot '..\emulator\common.ps1'
    Set-Content -Path $probe -Value "param([string]`$Serial)`n`$script:RequestedSerial = `$Serial`n. '$emuCommon'`nWrite-Output `"KEPT=[`$(`$script:RequestedSerial)] OVERWRITTEN=[`$(`$script:Serial)]`""
    $probeOut = (& $engine -NoProfile -File $probe -Serial 'R3CR30FQT0V' 2>&1 | ForEach-Object { "$_" }) -join ' '
    Assert-Equal 'requested serial kept after the emulator dot-source' $true ($probeOut -match 'KEPT=\[R3CR30FQT0V\]')
} finally { Remove-Item $probe -Force -ErrorAction SilentlyContinue }
$runText = Get-Content -Raw (Join-Path $PSScriptRoot 'run-flows.ps1')
Assert-Equal 'run-flows copies -Serial before the dot-source' $true ($runText.IndexOf('$script:RequestedSerial = $Serial') -ge 0 -and $runText.IndexOf('$script:RequestedSerial = $Serial') -lt $runText.IndexOf("emulator\common.ps1"))
Assert-Equal 'run-flows finds the phone with the copy' $true ($runText -match 'Find-Phone \$script:RequestedSerial')
Assert-Equal 'run-flows restarts the budget clock at the first flow' $true ($runText -match '(?s)function Invoke-LeasedRun.*\$script:RunStart = Get-Date.*Invoke-PlannedFlows')
Assert-Equal 'run-flows JAVA_TOOL_OPTIONS uses the space-free path' $true ($runText -match 'JAVA_TOOL_OPTIONS = "-Duser.home=\$\(Get-SpaceFreePath')
Assert-Equal 'run-flows catches an adb failure per flow' $true ($runText -match 'function Invoke-OneFlowSafely')
Assert-Equal 'run-flows writes the run marker' $true ($runText -match 'Request-RunMarker' -and $runText -match 'Remove-RunMarker')
Assert-Equal 'run-flows reads the handle before ExitCode' $true ($runText -match '\$null = \$p\.Handle')
Assert-Equal 'review 2: an unsaved stay-on setting ends the run before preflight -Fix' $true ($runText -match '(?s)function Invoke-LeasedRun[^\n]*\n\s*if \(\$Stage\.SaveFailed\) \{ return \$script:ExitNotReady \}')
Assert-Equal 'review 3: a timeout stops the driver and removes the forwards' $true ($runText -match 'Stop-MaestroTree \$p \}' -and $runText -match '-not \$finished -or \$p\.ExitCode -ne 0\) \{ Stop-MaestroLeftovers')
Assert-Equal 'review 4: the caller JAVA_TOOL_OPTIONS is restored' $true ($runText -match '\$env:JAVA_TOOL_OPTIONS = \$oldJavaOptions')
Assert-Equal 'review 5: stale run copies are swept at the start' $true ($runText -match '(?s)function Initialize-MaestroState.*Remove-StaleRunCopies')
Assert-Equal 'review 6: only the main logcat buffer is cleared' $true ($runText -match "'logcat', '-b', 'main', '-c'" -and $runText -notmatch "'-b', 'all'")

Assert-Equal 'check2 2: the rotation settings are saved before and restored in the run finally' $true ($runText -match '(?s)\$rotation = Save-RotationSettings.*Invoke-PlannedFlows.*Restore-RotationSettings.*RotationNotRestored = \$true.*Get-RunExitCodeAfterRestore')
Assert-Equal 'check4 5: the final rotation restore runs before the exit code is computed' $true ($runText.IndexOf('$script:RotationNotRestored = $true; Write-Host ''WARN rotation restore failed') -lt $runText.IndexOf('$code = Get-RunExitCodeAfterRestore'))
Assert-Equal 'check4: Maestro runs with a per-flow --debug-output folder and a per-flow log' $true ($runText -match 'debug-\$\(\$File\.BaseName\)' -and $runText -match 'phone-\$\(\$File\.BaseName\)\.log')
Assert-Equal 'check4: the first-run hint is gone from run-flows' $false ($runText -match 'FirstRunHint|StoppedAtFirstRun')
Assert-Equal 'check2 3: any non-zero exit cleans up the driver and forwards' $true ($runText -match 'ExitCode -ne 0\) \{ Stop-MaestroLeftovers')

if ($script:failed -gt 0) { Write-Host "test-run-flows: $($script:failed) FAILED"; exit 1 }
Write-Host 'test-run-flows: all passed'
