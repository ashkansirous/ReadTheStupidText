<#
.SYNOPSIS
  Self-test for the concurrency limit in activity.ps1 and the emulator health check in common.ps1 (Slice 215). Needs no device.
  Run it in both `powershell` (5.1) and `pwsh`, the way CI does (tools-selftests.yml).
.DESCRIPTION
  Get-ConcurrencyConflict is tested with fake activity files that carry the pid of a real, sleeping child process (a file whose process is gone is deleted as a crash):
   - two emulators running flows: the second is refused by default, and allowed with RTST_ALLOW_TWO_EMULATORS=1;
   - one emulator plus the VM is allowed, in either order; two emulators plus the VM is refused for the VM.
  Resolve-HealthProblem (run-flows.ps1's check before every flow) is tested on the answers of a healthy and a dead system_server.
  -DeviceKillCheck is the scripted emulator check for Slice 215 item 2: it kills system_server on the emulator (`adb shell su 0 kill`), expects the health check to
  report it, then waits for the emulator to recover. It is NOT part of CI and runs only when asked, on an emulator, in the device stage.
#>
param(
    [switch]$DeviceKillCheck,
    [ValidateSet(1, 2)][int]$Instance = 1
)
$ErrorActionPreference = 'Stop'
$script:failed = 0

function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

function Assert-Conflict([string]$Name, $Reason, [string]$Contains) {
    if ($Contains) {
        if (-not $Reason -or -not "$Reason".Contains($Contains)) { $script:failed++; Write-Host "FAIL: $Name expected a conflict containing [$Contains] got [$Reason]" }
    }
    elseif ($Reason) { $script:failed++; Write-Host "FAIL: $Name expected no conflict, got [$Reason]" }
}

# A fake live job: the file says it is `Kind` and belongs to $Pid (a process that really runs, so Get-LiveActivities keeps it).
function Add-FakeActivity([string]$Kind, [int]$ProcessId) {
    @{ pid = $ProcessId; kind = $Kind; started = (Get-Date).ToString('o') } | ConvertTo-Json | Set-Content (Join-Path $script:ActivityDir "$Kind-$ProcessId.json")
}

function Clear-FakeActivities { Get-ChildItem $script:ActivityDir -Filter '*.json' -ErrorAction SilentlyContinue | Remove-Item -Force }

function Test-ConcurrencyLimit {
    $sleeper = Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList '-NoProfile', '-Command', 'Start-Sleep -Seconds 120' -PassThru -WindowStyle Hidden
    try {
        $script:ActivityDir = Join-Path ([IO.Path]::GetTempPath()) ('rtst-activity-test-' + [guid]::NewGuid().ToString('N'))
        $null = New-Item -ItemType Directory -Force $script:ActivityDir
        $savedAllow = $env:RTST_ALLOW_TWO_EMULATORS
        try {
            Remove-Item Env:RTST_ALLOW_TWO_EMULATORS -ErrorAction SilentlyContinue
            Assert-Conflict 'nothing running' (Get-ConcurrencyConflict 'flows-rtst-i1') $null

            Add-FakeActivity 'flows-rtst-i1' $sleeper.Id
            Assert-Conflict 'a second emulator is refused by default' (Get-ConcurrencyConflict 'flows-dino-i2') 'one emulator runs flows at a time'
            $env:RTST_ALLOW_TWO_EMULATORS = '0'
            Assert-Conflict 'RTST_ALLOW_TWO_EMULATORS=0 still refuses' (Get-ConcurrencyConflict 'flows-dino-i2') 'one emulator runs flows at a time'
            $env:RTST_ALLOW_TWO_EMULATORS = '1'
            Assert-Conflict 'RTST_ALLOW_TWO_EMULATORS=1 allows the second emulator' (Get-ConcurrencyConflict 'flows-dino-i2') $null
            Remove-Item Env:RTST_ALLOW_TWO_EMULATORS
            Assert-Conflict 'one emulator plus the VM: the VM may start' (Get-ConcurrencyConflict 'vm-rtst') $null

            Clear-FakeActivities
            Add-FakeActivity 'vm-rtst' $sleeper.Id
            Assert-Conflict 'the VM plus one emulator: the emulator may start' (Get-ConcurrencyConflict 'flows-rtst-i1') $null

            Clear-FakeActivities
            Add-FakeActivity 'flows-rtst-i1' $sleeper.Id
            $env:RTST_ALLOW_TWO_EMULATORS = '1'
            Add-FakeActivity 'flows-dino-i2' $sleeper.Id
            Assert-Conflict 'two emulators running: the VM is refused' (Get-ConcurrencyConflict 'vm-rtst') 'too much for this PC'

            Clear-FakeActivities
            Add-FakeActivity 'vm-rtst' $sleeper.Id
            Add-FakeActivity 'flows-rtst-i1' $sleeper.Id
            Assert-Conflict 'the VM plus an emulator: a second emulator is refused even with the override' (Get-ConcurrencyConflict 'flows-dino-i2') 'would overload this PC'
        } finally {
            if ($null -eq $savedAllow) { Remove-Item Env:RTST_ALLOW_TWO_EMULATORS -ErrorAction SilentlyContinue } else { $env:RTST_ALLOW_TWO_EMULATORS = $savedAllow }
            Remove-Item $script:ActivityDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    } finally { if (-not $sleeper.HasExited) { $sleeper.Kill() } }
}

function Test-HealthProblem {
    Assert-Equal 'healthy: no problem' $null (Resolve-HealthProblem "package:/system/framework/framework-res.apk`r`n" "1`r`n")
    Assert-Conflict 'pm gone (the 2026-10-30 death)' (Resolve-HealthProblem "cmd: Failure calling service package: Broken pipe`r`n" "1") 'system_server is gone'
    Assert-Conflict 'empty pm answer' (Resolve-HealthProblem '' '1') 'system_server is gone'
    Assert-Conflict 'not booted' (Resolve-HealthProblem 'package:/x.apk' '') 'sys.boot_completed'
    Assert-Conflict 'boot flag 0' (Resolve-HealthProblem 'package:/x.apk' '0') 'sys.boot_completed'
}

# Slice 215: the install check must not turn a dead package service into "not installed", and run-flows must run the health check before the install checks.
function Test-InstallProblem {
    Assert-Equal 'listed package: no problem' $null (Resolve-InstallProblem "package:uk.sirous.rtst`r`n" 'uk.sirous.rtst')
    Assert-Conflict 'package service gone is unhealthy' (Resolve-InstallProblem "cmd: Can't find service: package`r`n" 'uk.sirous.rtst') 'EMULATOR UNHEALTHY'
    # Slice 221 item 2: the plain Dino store id is uninstalled on an emulator serial only, never on a real phone; the dev id is never a stale package.
    $pmBoth = "package:uk.sirous.dinoreader`r`npackage:uk.sirous.dinoreader.dbg`r`n"
    Assert-Equal 'emulator: the plain Dino id is removed' 'uk.sirous.dinoreader' (@(Get-StalePackagesToRemove 'dino' 'emulator-5554' $pmBoth) -join ',')
    Assert-Equal 'second emulator: the plain Dino id is removed' 'uk.sirous.dinoreader' (@(Get-StalePackagesToRemove 'dino' 'emulator-5556' $pmBoth) -join ',')
    Assert-Equal 'wireless phone: nothing removed' 0 @(Get-StalePackagesToRemove 'dino' '192.168.1.20:37000' $pmBoth).Count
    Assert-Equal 'mdns phone serial: nothing removed' 0 @(Get-StalePackagesToRemove 'dino' 'adb-R5CT123-abc._adb-tls-connect._tcp' $pmBoth).Count
    Assert-Equal 'usb-looking serial: nothing removed' 0 @(Get-StalePackagesToRemove 'dino' 'R58M1234ABC' $pmBoth).Count
    Assert-Equal 'a serial that only contains emulator- is not an emulator' 0 @(Get-StalePackagesToRemove 'dino' 'my-emulator-5554' $pmBoth).Count
    Assert-Equal 'emulator: only the dev id installed, nothing to remove' 0 @(Get-StalePackagesToRemove 'dino' 'emulator-5554' "package:uk.sirous.dinoreader.dbg`r`n").Count
    Assert-Equal 'rtst never removes anything' 0 @(Get-StalePackagesToRemove 'rtst' 'emulator-5554' $pmBoth).Count
    Assert-Equal 'both Dino packages are listed' 'uk.sirous.dinoreader,uk.sirous.dinoreader.dbg' ((Get-DinoPackagesListed $pmBoth) -join ',')
    Assert-Equal 'other apps are not Dino packages' 0 @(Get-DinoPackagesListed "package:uk.sirous.readthestupidtext`r`npackage:uk.sirous.dinoreaderx.other`r`n").Count
    Assert-Equal 'only the dev id: no problem' $null (Resolve-DinoPackageProblem "package:uk.sirous.dinoreader.dbg`r`n")
    Assert-Equal 'two Dino packages is a problem' $true ([bool](Resolve-DinoPackageProblem $pmBoth))
    Assert-Equal 'only the store id is a problem' $true ([bool](Resolve-DinoPackageProblem "package:uk.sirous.dinoreader`r`n"))
    Assert-Equal 'no Dino package is a problem' $true ([bool](Resolve-DinoPackageProblem "package:com.android.settings`r`n"))
    Assert-Equal 'a phone with the store id and the dev id: no problem (the store app stays)' $null (Resolve-DinoPackageProblem $pmBoth 'adb-ABC123._adb-tls-connect._tcp')
    Assert-Equal 'a phone with only the store id is a problem (no dev install)' $true ([bool](Resolve-DinoPackageProblem "package:uk.sirous.dinoreader`r`n" '192.168.1.5:5555'))
    Assert-Equal 'an emulator with both ids is still a problem' $true ([bool](Resolve-DinoPackageProblem $pmBoth 'emulator-5554'))
    Assert-Equal 'emulator build platform: Dino has its own cache name' 'android-dbg' (Get-EmulatorBuildPlatform 'dino')
    Assert-Equal 'emulator build platform: rtst unchanged' 'android' (Get-EmulatorBuildPlatform 'rtst')
    Assert-Conflict 'other package only is not installed' (Resolve-InstallProblem "package:uk.sirous.rtst.other`r`n" 'uk.sirous.rtst') 'is not installed'
    Assert-Conflict 'empty answer is not installed' (Resolve-InstallProblem '' 'uk.sirous.rtst') 'is not installed'
    $lines = Get-Content (Join-Path $PSScriptRoot 'run-flows.ps1')
    $health = [array]::FindIndex($lines, [Predicate[string]]{ param($l) $l -match "Confirm-EmulatorHealthy 'before the install check'" })
    $key = [array]::FindLastIndex($lines, [Predicate[string]]{ param($l) $l -match 'Get-InstalledKey \$App' })
    $pm = [array]::FindLastIndex($lines, [Predicate[string]]{ param($l) $l -match 'pm list packages' })
    Assert-Equal 'run-flows checks health before the build-key check' $true ($health -ge 0 -and $health -lt $key)
    Assert-Equal 'run-flows checks health before pm list packages' $true ($health -ge 0 -and $health -lt $pm)
}

# Invoke-AdbBounded with a stand-in for adb: a quick answer comes back, a hung one is killed and reported as $null.
function Test-AdbBounded {
    $savedAdb = $script:Adb
    $script:Adb = (Get-Process -Id $PID).Path
    try {
        Assert-Equal 'a quick adb answer is returned' 'ok' ("$(Invoke-AdbBounded @('-NoProfile', '-Command', 'Write-Output', 'ok') 30)").Trim()
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $hung = Invoke-AdbBounded @('-NoProfile', '-Command', 'Start-Sleep', '60') 2
        Assert-Equal 'a hung adb call times out as null' $true ($null -eq $hung)
        Assert-Equal 'the timeout is honoured (well under the 60 s sleep)' $true ($sw.Elapsed.TotalSeconds -lt 30)
    } finally { $script:Adb = $savedAdb }
}

function Invoke-DeviceKillCheck {
    Set-EmulatorInstance $Instance
    Assert-Equal 'the emulator is healthy before the kill' $null (Get-EmulatorHealthProblem)
    $systemPid = (& $script:Adb -s $script:Serial shell pidof system_server | Out-String).Trim()
    if (-not $systemPid) { $script:failed++; Write-Host 'FAIL: system_server has no pid'; return }
    & $script:Adb -s $script:Serial shell su 0 kill $systemPid | Out-Null
    $deadline = (Get-Date).AddSeconds(30)
    $problem = $null
    while (-not $problem -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500; $problem = Get-EmulatorHealthProblem }
    if (-not $problem) { Write-Host 'NOTE: the health check never saw the kill (system_server restarted within one probe); run-flows would not have seen it either.' }
    else { Write-Host "Health check after the kill: $problem" }
    $recover = (Get-Date).AddSeconds(120)
    while ((Get-EmulatorHealthProblem) -and (Get-Date) -lt $recover) { Start-Sleep -Seconds 2 }
    Assert-Equal 'the emulator recovers after the kill' $null (Get-EmulatorHealthProblem)
}

# Slice 225 item 4: `pm list packages <package>` prints one `package:` line per match; with the store id AND the .dbg id installed the old space-joined text was "not installed".
function Test-InstallCheckSeesBothPackages {
    $both = @('package:uk.sirous.readthestupidtext', 'package:uk.sirous.readthestupidtext.dbg')
    Assert-Equal 'two package lines: the store id is installed' $null (Resolve-InstallProblem (Join-PmLines $both) 'uk.sirous.readthestupidtext')
    Assert-Equal 'two package lines: the .dbg id is installed' $null (Resolve-InstallProblem (Join-PmLines $both) 'uk.sirous.readthestupidtext.dbg')
    Assert-Conflict 'the old space-joined text could not match (the bug)' (Resolve-InstallProblem ($both -join ' ') 'uk.sirous.readthestupidtext') 'is not installed'
    Assert-Equal 'carriage returns from adb do not break the match' $null (Resolve-InstallProblem (Join-PmLines @("package:uk.sirous.dinoreader.dbg`r", "package:uk.sirous.dinoreader`r")) 'uk.sirous.dinoreader')
    Assert-Conflict 'one other package only is still not installed' (Resolve-InstallProblem (Join-PmLines @('package:uk.sirous.rtst.other')) 'uk.sirous.rtst') 'is not installed'
    $source = Get-Content (Join-Path $PSScriptRoot 'run-flows.ps1') -Raw
    Assert-Equal 'run-flows no longer passes the joined-with-a-space text' $false ($source -match 'Resolve-InstallProblem "\$installed"')
}

# Slice 225 items 4 and 19: -Flow takes a name or a path, and also finds a flow under needs-file/.
function Test-FlowSelection {
    $root = Join-Path ([IO.Path]::GetTempPath()) ('rtst-flows-test-' + [guid]::NewGuid().ToString('N'))
    try {
        $null = New-Item -ItemType Directory -Force (Join-Path $root 'needs-file')
        foreach ($name in 'alpha.yaml', 'beta.yaml', 'needs-file\gamma.yaml') { Set-Content (Join-Path $root $name) 'appId: x' }
        Assert-Equal 'a name is a name' 'alpha' (Get-FlowName 'alpha')
        Assert-Equal 'a relative path is reduced to the name' 'alpha' (Get-FlowName '.maestro/dino/alpha.yaml')
        Assert-Equal 'a Windows path is reduced to the name' 'alpha' (Get-FlowName '.maestro\dino\alpha.yaml')
        Assert-Equal 'no flow means no name' '' (Get-FlowName '')
        Assert-Equal 'no -Flow: the app folder only, needs-file skipped' 'alpha,beta' ((Find-FlowFiles $root '' | ForEach-Object { $_.BaseName }) -join ',')
        Assert-Equal '-Flow <name> and -Flow <path> pick the same flow' (@(Find-FlowFiles $root 'beta')[0].FullName) (@(Find-FlowFiles $root ".maestro/dino/beta.yaml")[0].FullName)
        Assert-Equal '-Flow finds a needs-file flow by name' 'gamma' (@(Find-FlowFiles $root 'gamma')[0].BaseName)
        Assert-Equal '-Flow finds a needs-file flow by path' 'gamma' (@(Find-FlowFiles $root '.maestro/dino/needs-file/gamma.yaml')[0].BaseName)
        Assert-Equal 'an unknown name finds nothing' 0 @(Find-FlowFiles $root 'nope').Count
    } finally { Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue }
}

# Slice 225 item 12: a stale Maestro driver is force-stopped before the UI dump; the dump still happens.
function Test-DumpStopsMaestroDriver {
    $script:DumpCalls = New-Object System.Collections.Generic.List[string]
    $fake = { param($a) $script:DumpCalls.Add($a -join ' '); 'dump-xml' }
    $xml = Get-UiDumpXml $fake
    Assert-Equal 'the dump answer comes back' 'dump-xml' $xml
    Assert-Equal 'two adb calls' 2 $script:DumpCalls.Count
    Assert-Equal 'the Maestro driver is force-stopped first' 'shell am force-stop dev.mobile.maestro' $script:DumpCalls[0]
    Assert-Equal 'then the dump runs' 'shell uiautomator dump /dev/tty' $script:DumpCalls[1]
    $source = Get-Content (Join-Path $PSScriptRoot 'run-flows.ps1') -Raw
    Assert-Equal 'run-flows reads its ANR dump through Get-UiDumpXml' $true ($source -match 'Get-UiDumpXml')
    Assert-Equal 'run-flows has no bare uiautomator dump of its own' $false ($source -match 'shell uiautomator dump')
}

# Slice 225 item 21: TalkBack is turned on and off with the secure settings, no Settings UI (so no permission dialog), and what was there before comes back.
function Test-TalkBackSettings {
    $service = 'com.google.android.marvin.talkback/com.google.android.marvin.talkback.TalkBackService'
    $on = @(Get-TalkBackEnableCommands 'null')
    Assert-Equal 'enable: two settings' 2 $on.Count
    Assert-Equal 'enable: the service is listed' "shell settings put secure enabled_accessibility_services $service" ($on[0] -join ' ')
    Assert-Equal 'enable: accessibility is switched on' 'shell settings put secure accessibility_enabled 1' ($on[1] -join ' ')
    Assert-Equal 'enable: another service already on is kept' "other/Svc:$service" ((@(Get-TalkBackEnableCommands 'other/Svc')[0])[-1])
    Assert-Equal 'enable: TalkBack is not listed twice' $service ((@(Get-TalkBackEnableCommands $service)[0])[-1])
    $off = @(Get-TalkBackDisableCommands 'null' 'null')
    Assert-Equal 'disable: nothing before means delete the service list' 'shell settings delete secure enabled_accessibility_services' ($off[0] -join ' ')
    Assert-Equal 'disable: nothing before means delete the flag' 'shell settings delete secure accessibility_enabled' ($off[1] -join ' ')
    $back = @(Get-TalkBackDisableCommands 'other/Svc' '1')
    Assert-Equal 'disable: the earlier service list comes back' 'shell settings put secure enabled_accessibility_services other/Svc' ($back[0] -join ' ')
    Assert-Equal 'disable: the earlier flag comes back' 'shell settings put secure accessibility_enabled 1' ($back[1] -join ' ')
    # With a fake adb: after the enable commands the flag reads 1 and the helper reports it; when the flag stays 0 it throws.
    $script:Secure = @{ enabled_accessibility_services = 'null'; accessibility_enabled = '0' }
    $script:Stick = $true
    $fake = {
        param($a)
        $line = $a -join ' '
        if ($line -match '^shell settings get secure (\S+)$') { return $script:Secure[$Matches[1]] }
        if ($line -match '^shell settings put secure (\S+) (.+)$') { if ($script:Stick) { $script:Secure[$Matches[1]] = $Matches[2] }; return '' }
        if ($line -match '^shell settings delete secure (\S+)$') { $script:Secure[$Matches[1]] = 'null'; return '' }
        return ''
    }
    $previous = Enable-EmulatorTalkBack $fake
    Assert-Equal 'helper: accessibility_enabled is 1 after the enable' '1' $script:Secure['accessibility_enabled']
    Assert-Equal 'helper: it reads as on' $true (Test-EmulatorTalkBackOn $fake)
    Disable-EmulatorTalkBack $previous $fake
    Assert-Equal 'helper: the flag is back to what it was (0)' '0' $script:Secure['accessibility_enabled']
    Assert-Equal 'helper: the service list is back to what it was (none)' 'null' $script:Secure['enabled_accessibility_services']
    Assert-Equal 'helper: it reads as off again' $false (Test-EmulatorTalkBackOn $fake)
    $script:Stick = $false
    $threw = $false; try { Enable-EmulatorTalkBack $fake | Out-Null } catch { $threw = $_.Exception.Message -like 'TalkBack did not switch on*' }
    Assert-Equal 'helper: a flag that stays off is an error, not a silent pass' $true $threw
}

# Dot-sourced here, at script level (inside a function the functions would vanish with it). common.ps1 loads activity.ps1 and needs ANDROID_HOME only to
# build paths, so a placeholder is enough in CI. Test-ConcurrencyLimit then points $script:ActivityDir at a temp folder.
if (-not $env:ANDROID_HOME -and -not $env:ANDROID_SDK_ROOT) { $env:ANDROID_HOME = [IO.Path]::GetTempPath() }
. (Join-Path $PSScriptRoot 'common.ps1')

Test-ConcurrencyLimit
Test-HealthProblem
Test-InstallProblem
Test-InstallCheckSeesBothPackages
Test-FlowSelection
Test-DumpStopsMaestroDriver
Test-TalkBackSettings
Test-AdbBounded
if ($DeviceKillCheck) { Invoke-DeviceKillCheck }

if ($script:failed) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host "All activity and health checks pass (PowerShell $($PSVersionTable.PSVersion))."
exit 0
