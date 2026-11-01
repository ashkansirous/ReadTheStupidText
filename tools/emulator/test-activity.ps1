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

# Dot-sourced here, at script level (inside a function the functions would vanish with it). common.ps1 loads activity.ps1 and needs ANDROID_HOME only to
# build paths, so a placeholder is enough in CI. Test-ConcurrencyLimit then points $script:ActivityDir at a temp folder.
if (-not $env:ANDROID_HOME -and -not $env:ANDROID_SDK_ROOT) { $env:ANDROID_HOME = [IO.Path]::GetTempPath() }
. (Join-Path $PSScriptRoot 'common.ps1')

Test-ConcurrencyLimit
Test-HealthProblem
Test-InstallProblem
Test-AdbBounded
if ($DeviceKillCheck) { Invoke-DeviceKillCheck }

if ($script:failed) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host "All activity and health checks pass (PowerShell $($PSVersionTable.PSVersion))."
exit 0
