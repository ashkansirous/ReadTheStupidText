<#
.SYNOPSIS
  Self-test for the Slice 216 helpers in common.ps1: corrupt lease handling, the build path-length check, the shared build command, the lease note, the cold-read cache command.
  Run it in both `powershell` (5.1) and `pwsh`. Needs no device; the lease files go to a temp folder.
#>
$ErrorActionPreference = 'Stop'
if (-not $env:ANDROID_HOME -and -not $env:ANDROID_SDK_ROOT) { $env:ANDROID_HOME = $env:TEMP }
. (Join-Path $PSScriptRoot 'common.ps1')
$script:LeaseDir = Join-Path ([IO.Path]::GetTempPath()) ('rtst-leftovers-test-' + [guid]::NewGuid().ToString('N'))

$failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

try {
    # ---- item 7: a corrupt lease is named and removable; a valid one never is
    $key = 'leftovers-test'
    $null = New-Item -ItemType Directory -Force $script:LeaseDir
    Assert-Equal 'no file: not corrupt' $false (Test-LeaseFileCorrupt $key)
    Assert-Equal 'no file: nothing to clear' 'NotCorrupt' (Clear-CorruptLease $key)
    Assert-Equal 'valid json is not corrupt' $false (Test-LeaseTextCorrupt '{ "owner": "a", "taken": 1, "expires": 2 }')
    Assert-Equal 'broken json is corrupt' $true (Test-LeaseTextCorrupt '{ not json')
    Assert-Equal 'take a valid lease' $true (Request-Lease $key 'agent-a' 60)
    Assert-Equal 'valid lease: not corrupt' $false (Test-LeaseFileCorrupt $key)
    Assert-Equal 'valid lease: -Clear -Corrupt leaves it' 'NotCorrupt' (Clear-CorruptLease $key)
    Assert-Equal 'valid lease still held' 'agent-a' (Read-Lease $key).Owner
    Assert-Equal 'release' $true (Remove-Lease $key 'agent-a')
    $leasePath = Get-LeasePath $key
    Set-Content $leasePath '{ "owner": "old", "taken": 1, "expires": 2 }'
    Assert-Equal 'expired valid lease: -Clear -Corrupt leaves it' 'NotCorrupt' (Clear-CorruptLease $key)
    Assert-Equal 'expired valid lease file still there' $true (Test-Path $leasePath)
    Set-Content $leasePath '{ not json'
    Assert-Equal 'corrupt file is detected by key' $true (Test-LeaseFileCorrupt $key)
    $now = [DateTimeOffset]::new((Get-Item $leasePath).LastWriteTimeUtc).ToUnixTimeSeconds() + 3 * 3600 + 10 * 60
    $held = Read-Lease $key $now
    $text = Format-LeaseHolder $held $now
    Assert-Equal 'corrupt holder says corrupt' $true ($text -like 'a corrupt lease file*')
    Assert-Equal 'corrupt holder says its age' $true ($text -like '*written 3 h 10 min ago*')
    Assert-Equal 'corrupt holder says how to clear it' $true ($text -like '*lease.ps1 -Clear -Corrupt*')
    Assert-Equal 'emulator corrupt holder does not point at lease.ps1' $false ((Format-LeaseHolder $held $now 'emulator') -like '*lease.ps1*')
    Assert-Equal 'a normal holder keeps the old text' $true ((Format-LeaseHolder ([pscustomobject]@{ Owner = 'agent-b'; Expires = $now + 60 }) $now) -like "'agent-b' (until *)")
    Assert-Equal 'corrupt lease cleared' 'Cleared' (Clear-CorruptLease $key)
    Assert-Equal 'cleared: file gone' $false (Test-Path $leasePath)
    Assert-Equal 'cleared: phone is free' $null (Read-Lease $key $now)
    Assert-Equal 'span under an hour' '12 min' (Format-LeaseSpan 725)
    Assert-Equal 'span over an hour' '1 h 5 min' (Format-LeaseSpan 3900)
} finally {
    Remove-Item $script:LeaseDir -Recurse -Force -ErrorAction SilentlyContinue
}

# ---- item 8: a screen change prints the changed texts only, cut to 120 characters
$sigs = @('Hello world||id1|c=False|s=False', '|a description|id2|c=True|s=False', '||only.id|c=False|s=False')
Assert-Equal 'label: text first' 'Hello world' (Get-SignatureLabel $sigs[0])
Assert-Equal 'label: description when no text, checked state shown' 'a description [checked]' (Get-SignatureLabel $sigs[1])
Assert-Equal 'label: id when nothing else' 'only.id' (Get-SignatureLabel $sigs[2])
Assert-Equal 'short change line' 'Added: Hello world ; a description [checked] ; only.id' (Format-ScreenChangeLine 'Added' $sigs)
$wide = @(1..40 | ForEach-Object { "word$_||id$_|c=False|s=False" })
$cut = Format-ScreenChangeLine 'Added' $wide
Assert-Equal 'long change line is cut to 120' 120 $cut.Length
Assert-Equal 'cut line ends in an ellipsis' $true ($cut.EndsWith('...'))
Assert-Equal 'ids and flags are not printed' $false ($cut -match 'id\d|c=False')
$tapSource = Get-Content (Join-Path $PSScriptRoot 'tap.ps1') -Raw
Assert-Equal 'tap.ps1 prints the short line and logs the full one' $true (($tapSource -match 'Format-ScreenChangeLine') -and ($tapSource -match 'Write-ScreenChangeLog \$diff'))

# ---- item 6: the lease note says which length and why
Assert-Equal 'note: budget plus margin' '   phone lease: 25 min (a timed run needs 25 min: its budget plus the margin)' (Format-LeaseLengthNote 'a timed run' 25 25 0)
Assert-Equal 'note: own longer lease kept' $true ((Format-LeaseLengthNote 'a timed run' 90 25 90) -like '*your own lease still has 90 min, never shortened*')

# ---- item 3: the path-length check fails early, naming what to shorten
$short = 'D:\w\s1\.artifacts\rtst-aot'
Assert-Equal 'short path passes' $null (Test-BuildPathLength $short)
$long = 'D:\' + ('x' * 100) + '\.artifacts\rtst-aot'
$problem = Test-BuildPathLength $long
Assert-Equal 'estimate-only path warns' 'Warn' $problem.Level
$warned = $null; Assert-BuildPathLength $long -WarningVariable warned -WarningAction SilentlyContinue
Assert-Equal 'estimate-only path does not throw, warns' $true (@($warned).Count -eq 1)
Assert-Equal 'boundary passes' $null (Test-BuildPathLength ('x' * ($script:WindowsPathLimit - $script:DeepestMauiIntermediateChars)))
Assert-Equal 'one over warns' 'Warn' (Test-BuildPathLength ('x' * ($script:WindowsPathLimit - $script:DeepestMauiIntermediateChars + 1))).Level
$tooLong = 'D:\' + ('x' * 200) + '\.artifacts\rtst-aot'
Assert-Equal 'known-too-long path fails' 'Fail' (Test-BuildPathLength $tooLong).Level
$threw = $false; try { Assert-BuildPathLength $tooLong } catch { $threw = $_.Exception.Message -like 'path too long*' }
Assert-Equal 'Assert-BuildPathLength throws on known-too-long' $true $threw

# ---- item 5: one build command; both scripts use it
$pkg = 'uk.sirous.readthestupidtext.dbg'
$debugArgs = Get-PhoneBuildArguments 'P.csproj' 'A' $pkg 'android-arm64' $false
$aotArgs = Get-PhoneBuildArguments 'P.csproj' 'A' $pkg 'android-arm64' $true
Assert-Equal 'debug: no AOT flag' $false (($debugArgs -join ' ') -match 'RunAOTCompilation')
Assert-Equal 'aot: AOT flags added to the debug command' (($debugArgs + '-p:RunAOTCompilation=true' + '-p:AndroidEnableProfiledAot=false') -join ' ') ($aotArgs -join ' ')
Assert-Equal 'rid and package in the command' $true ((($debugArgs -join ' ') -match 'RuntimeIdentifier=android-arm64') -and (($debugArgs -join ' ') -match [regex]::Escape("ApplicationId=$pkg")))
Assert-Equal 'x64 rid for the emulator' $true ((Get-PhoneBuildArguments 'P' 'A' $pkg 'android-x64' $false) -contains '-p:RuntimeIdentifier=android-x64')
Assert-Equal 'quoting only where there are spaces' 'a "b c"' ((ConvertTo-ProcessArguments @('a', 'b c')) -join ' ')
foreach ($script in 'audio-timing.ps1', 'run-checks.ps1') {
    $source = Get-Content (Join-Path $PSScriptRoot $script) -Raw
    Assert-Equal "$script builds through Get-PhoneBuildArguments" $true ($source -match 'Get-PhoneBuildArguments')
    Assert-Equal "$script has no copy of the dotnet build flags" $false ($source -match '-p:EmbedAssembliesIntoApk')
    Assert-Equal "$script checks the path length" $true ($source -match 'Assert-BuildPathLength')
}

# ---- item 2 and 4: cold reads clear the sentence cache (dev package only); the timing log folder is the shared one
$command = New-SentenceCacheClearCommand $pkg
Assert-Equal 'clear command runs as the dev package' $true ($command -like "run-as $pkg sh -c 'rm -rf cache/sentences;*")
$refused = $false; try { New-SentenceCacheClearCommand 'uk.sirous.readthestupidtext' | Out-Null } catch { $refused = $true }
Assert-Equal 'store package is refused' $true $refused
$timing = Get-Content (Join-Path $PSScriptRoot 'audio-timing.ps1') -Raw
Assert-Equal 'audio-timing clears the cache before each cold read' $true ($timing -match 'Clear-SentenceCache \$Phone \$package')
Assert-Equal 'audio-timing logs go to Get-WorktreesRoot' $true ($timing -match "Join-Path \(Get-WorktreesRoot\) '\.phone'")


# ---- -Rates: "1,1" is two rates, not eleven; bad values are refused
Assert-Equal 'rates: "1,1" splits into two' '1 1' ((ConvertTo-RateList '1,1') -join ' ')
Assert-Equal 'rates: array of strings' '1 2' ((ConvertTo-RateList @('1', '2')) -join ' ')
Assert-Equal 'rates: array of doubles' '1.25 2' ((ConvertTo-RateList @(1.25, 2.0)) -join ' ')
Assert-Equal 'rates: single value' '1.5' ((ConvertTo-RateList '1.5') -join ' ')
foreach ($bad in '11', '0.4', '2.5', 'abc', '1,,x', '') {
    $refusedRate = $false; try { ConvertTo-RateList $bad | Out-Null } catch { $refusedRate = $true }
    Assert-Equal "rates: [$bad] refused" $true $refusedRate
}
# ---- Slice 221 item 6: a second run with the same lease owner and a live other pid is refused; a dead pid is free; -Force takes over
$runDir = Join-Path ([IO.Path]::GetTempPath()) ('rtst-runmarker-test-' + [guid]::NewGuid().ToString('N'))
$sleeper = $null
try {
    $runKey = 'run-marker-test'
    $nightKey = Get-RunMarkerKey $runKey 'night'
    $otherKey = Get-RunMarkerKey $runKey 'other'
    Assert-Equal 'run marker: no file, no marker' $null (Read-RunMarker $nightKey $runDir)
    $savedOwners = @{}
    foreach ($name in 'RTST_DEVICE_OWNER', 'RTST_PHONE_OWNER') { $savedOwners[$name] = [Environment]::GetEnvironmentVariable($name); [Environment]::SetEnvironmentVariable($name, $null) }
    try {
        Assert-Equal 'run owner: the default is one name per phone, not per pid' 'run-checks-ABC123' (Get-RunChecksOwner 'ABC123')
        [Environment]::SetEnvironmentVariable('RTST_PHONE_OWNER', 'phone-owner')
        Assert-Equal 'run owner: the owner variable wins' 'phone-owner' (Get-RunChecksOwner 'ABC123')
    }
    finally { foreach ($name in $savedOwners.Keys) { [Environment]::SetEnvironmentVariable($name, $savedOwners[$name]) } }
    Assert-Equal 'run marker: nothing blocks without a marker' $false (Test-RunMarkerBlocks $null 'night' $PID)
    $sleeper = Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList @('-NoProfile', '-Command', 'Start-Sleep -Seconds 120') -WindowStyle Hidden -PassThru
    $live = [pscustomobject]@{ owner = 'night'; pid = $sleeper.Id; procStart = $sleeper.StartTime.ToFileTimeUtc() }
    Assert-Equal 'run marker: same owner, live other pid blocks' $true (Test-RunMarkerBlocks $live 'night' $PID)
    Assert-Equal 'run marker: another owner is not this rule (the lease decides)' $false (Test-RunMarkerBlocks $live 'other' $PID)
    Assert-Equal 'run marker: own pid never blocks' $false (Test-RunMarkerBlocks ([pscustomobject]@{ owner = 'night'; pid = $PID; procStart = 0 }) 'night' $PID)
    $reused = [pscustomobject]@{ owner = 'night'; pid = $sleeper.Id; procStart = ($sleeper.StartTime.ToFileTimeUtc() + 1) }
    Assert-Equal 'run marker: a reused pid (other start time) is free' $false (Test-RunMarkerBlocks $reused 'night' $PID)

    # the file path: the first run claims it, the second (written as another live pid) is refused, -Force takes over
    $null = New-Item -ItemType Directory -Force $runDir
    $markerPath = Get-RunMarkerPath $nightKey $runDir
    @{ owner = 'night'; pid = $sleeper.Id; procStart = $sleeper.StartTime.ToFileTimeUtc() } | ConvertTo-Json | Set-Content $markerPath
    $refusal = Request-RunMarker $runKey 'night' $false $runDir
    Assert-Equal 'run marker: second run with the same owner is refused' $true ([bool]$refusal)
    Assert-Equal 'run marker: the refusal names the owner, the pid and -Force' $true ($refusal -like "*'night'*pid $($sleeper.Id)*-Force*")
    Assert-Equal 'run marker: the refused run left the first run marker' $sleeper.Id (Read-RunMarker $nightKey $runDir).pid
    Assert-Equal 'run marker: another owner is not refused' $null (Request-RunMarker $runKey 'other' $false $runDir)
    Assert-Equal 'run marker: another owner wrote its own file' $PID (Read-RunMarker $otherKey $runDir).pid
    Assert-Equal 'run marker: another owner did not overwrite the first' $sleeper.Id (Read-RunMarker $nightKey $runDir).pid
    Remove-RunMarker $runKey 'other' $runDir
    @{ owner = 'night'; pid = $sleeper.Id; procStart = $sleeper.StartTime.ToFileTimeUtc() } | ConvertTo-Json | Set-Content $markerPath
    Assert-Equal 'run marker: -Force starts anyway' $null (Request-RunMarker $runKey 'night' $true $runDir)
    Assert-Equal 'run marker: -Force took the marker over' $PID (Read-RunMarker $nightKey $runDir).pid
    Remove-RunMarker $runKey 'night' $runDir
    Assert-Equal 'run marker: removed by its own run' $false (Test-Path $markerPath)

    # a dead pid is free
    $dead = Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList @('-NoProfile', '-Command', 'exit 0') -WindowStyle Hidden -PassThru
    $dead.WaitForExit()
    @{ owner = 'night'; pid = $dead.Id; procStart = 0 } | ConvertTo-Json | Set-Content $markerPath
    Assert-Equal 'run marker: a dead pid is free' $null (Request-RunMarker $runKey 'night' $false $runDir)
    Assert-Equal 'run marker: the new run owns it' $PID (Read-RunMarker $nightKey $runDir).pid
    Set-Content $markerPath '{ not json'
    Assert-Equal 'run marker: a corrupt file is free' $null (Request-RunMarker $runKey 'night' $false $runDir)
    # Remove-RunMarker leaves another run's marker alone
    @{ owner = 'night'; pid = $sleeper.Id; procStart = $sleeper.StartTime.ToFileTimeUtc() } | ConvertTo-Json | Set-Content $markerPath
    Remove-RunMarker $runKey 'night' $runDir
    Assert-Equal 'run marker: a marker of another run is not removed' $true (Test-Path $markerPath)
    # the runner wires it: a flag, the claim before the builds, the refusal exit code, the removal after the stage
    $runner = Get-Content (Join-Path $PSScriptRoot 'run-checks.ps1') -Raw
    Assert-Equal 'run-checks has -Force' $true ($runner -match '\[switch\]\$Force')
    Assert-Equal 'run-checks claims before the builds start' $true ($runner.IndexOf('Request-RunMarker') -lt $runner.IndexOf('$script:BuildJobs = Start-VariantBuilds'))
    Assert-Equal 'run-checks covers the builds with the finally' $true ($runner -match '(?s)try \{\s+\$script:BuildJobs = Start-VariantBuilds')
    Assert-Equal 'run-checks removes the marker in a finally' $true ($runner -match 'finally \{ Remove-RunMarker')
}
finally {
    if ($sleeper -and -not $sleeper.HasExited) { Stop-Process -Id $sleeper.Id -Force -ErrorAction SilentlyContinue }
    Remove-Item $runDir -Recurse -Force -ErrorAction SilentlyContinue
}

# ================= Slice 225 =================
# A fake adb from here on: every adb call is recorded, and the answers come from $script:Fake*. Nothing reaches a device.
$script:AdbCalls = New-Object System.Collections.Generic.List[string]
$script:FakeDataSync = 'null'
$script:FakeBlacklist = 'Restrict background blacklisted UIDs: none'
$script:FakeWakefulness = 'Awake'
$script:FakePackage = 'uk.sirous.readthestupidtext.dbg'   # the only dev package installed on the fake phone
$script:FakeStuck = $false                                  # true: the clear commands change nothing
$script:FakeHealPending = 'none'
$script:FakeStayOn = '0'
function Invoke-Adb([string]$Serial, [string[]]$Arguments, [int]$TimeoutSeconds = 0) {
    $line = $Arguments -join ' '
    $script:AdbCalls.Add($line)
    $global:LASTEXITCODE = 0
    if ($line -like '*getprop ro.serialno*') { return @('FAKESERIAL1') }
    if ($line -like '*settings get global stay_on_while_plugged_in*') { return @($script:FakeStayOn) }
    if ($line -like '*settings put global stay_on_while_plugged_in*') { $script:FakeStayOn = $Arguments[-1]; return @() }
    if ($line -like '*pm list packages -U*') { if ($Arguments[-1] -ceq $script:FakePackage) { return @("package:$($script:FakePackage) uid:10234") } else { return @() } }
    if ($line -like '*device_config get*') { return @($script:FakeDataSync) }
    if ($line -like '*device_config delete*') { if (-not $script:FakeStuck) { $script:FakeDataSync = 'null' }; return @() }
    if ($line -like '*netpolicy list*') { return @($script:FakeBlacklist) }
    if ($line -like '*netpolicy remove*') { if (-not $script:FakeStuck) { $script:FakeBlacklist = 'Restrict background blacklisted UIDs: 10555' }; return @() }
    if ($line -like '*dumpsys battery*') { return @('AC powered: true') }
    if ($line -like '*mWakefulness=*') { return @("mWakefulness=$($script:FakeWakefulness)") }
    if ($line -like '*/proc/$p/cmdline*') { return @($script:FakeHealPending) }
    return @()
}
function Reset-Fake { $script:AdbCalls.Clear(); $script:FakeDataSync = 'null'; $script:FakeBlacklist = 'Restrict background blacklisted UIDs: none'; $script:FakeWakefulness = 'Awake'; $script:FakeStuck = $false; $script:FakeHealPending = 'none'; $script:FakeStayOn = '0' }
$devPackages = @($script:DevApps.Values | ForEach-Object { $_.Package })

# ---- item 1: leftovers are found and cleared by script, each with its matching clear command; the user's own entries are left alone
Reset-Fake
$script:FakeDataSync = '30000'
$script:FakeBlacklist = 'Restrict background blacklisted UIDs: 10234 10555'
$result = Clear-DeviceLeftovers 'ph' $devPackages
Assert-Equal 'item 1: the dataSync timeout is deleted' $true ($script:AdbCalls -contains 'shell device_config delete activity_manager data_sync_fgs_timeout_duration')
Assert-Equal 'item 1: the dev app''s network block is removed' $true ($script:AdbCalls -contains 'shell cmd netpolicy remove restrict-background-blacklist 10234')
Assert-Equal 'item 1: another app''s block (10555) is never touched' $false ($script:AdbCalls -contains 'shell cmd netpolicy remove restrict-background-blacklist 10555')
Assert-Equal 'item 1: the compat flag is reset for the dev package' $true ($script:AdbCalls -contains 'shell am compat reset FGS_INTRODUCE_TIME_LIMITS uk.sirous.readthestupidtext.dbg')
Assert-Equal 'item 1: a pending device-side restore is cancelled' $true (@($script:AdbCalls | Where-Object { $_ -like '*/proc/$p/cmdline*' }).Count -ge 1)
Assert-Equal 'item 1: two leftovers reported' 2 $result.Leftovers.Count
Assert-Equal 'item 1: nothing remains after the clear' 0 $result.Remaining.Count
Assert-Equal 'item 1: the dev app that is not installed is skipped (no call for dino)' 0 @($script:AdbCalls | Where-Object { $_ -like '*dinoreader*' -and $_ -notlike '*pm list packages -U*' }).Count
Reset-Fake
$clean = Clear-DeviceLeftovers 'ph' $devPackages
Assert-Equal 'item 1: a clean phone reports no leftover' 0 $clean.Leftovers.Count
Assert-Equal 'item 1: a clean phone deletes nothing' 0 @($script:AdbCalls | Where-Object { $_ -like '*device_config delete*' -or $_ -like '*netpolicy remove*' }).Count
Assert-Equal 'item 1: the compat reset is idempotent and always sent' 1 @($script:AdbCalls | Where-Object { $_ -like '*am compat reset*' }).Count
Reset-Fake
$script:FakeDataSync = '30000'; $script:FakeStuck = $true
$stuck = Clear-DeviceLeftovers 'ph' $devPackages
Assert-Equal 'item 1: a leftover that survives the clear is reported as remaining' 1 $stuck.Remaining.Count
# round 3 (review 3): the uid is blocked only as a whole token on a blacklist/restrict label line; no such line at all is Unknown, never clean
Assert-Equal 'round 3: the uid on the blacklist line is Blocked' 'Blocked' (Get-UidBlockState 'Restrict background blacklisted UIDs: 10234 10555' 10234)
Assert-Equal 'round 3: none is Clean' 'Clean' (Get-UidBlockState 'Restrict background blacklisted UIDs: none' 10234)
Assert-Equal 'round 3: an empty list under the label is Clean' 'Clean' (Get-UidBlockState 'Restrict background blacklisted UIDs: ' 10234)
Assert-Equal 'round 3: the label is read in any case' 'Blocked' (Get-UidBlockState 'RESTRICT BACKGROUND BLACKLISTED UIDS: 10234' 10234)
Assert-Equal 'round 3: the blocklist spelling is read too' 'Blocked' (Get-UidBlockState 'Restrict background blocklisted UIDs: 10234' 10234)
Assert-Equal 'round 3: a uid that only starts with the same digits is Clean' 'Clean' (Get-UidBlockState 'Restrict background blacklisted UIDs: 102345' 10234)
Assert-Equal 'round 3: the same number on a line with no such label does not count' 'Clean' (Get-UidBlockState "Network policy rules:`nSome counter: 10234`nRestrict background blacklisted UIDs: 10555" 10234)
Assert-Equal 'round 3: a whitelist line is not the block list' 'Clean' (Get-UidBlockState "Restrict background status: false`nRestrict background whitelisted UIDs: 10234`nRestrict background blacklisted UIDs: " 10234)
Assert-Equal 'round 3: with no label line the state is Unknown' 'Unknown' (Get-UidBlockState 'Some other wording: 10234' 10234)
Assert-Equal 'round 3: empty output is Unknown' 'Unknown' (Get-UidBlockState '' 10234)
Reset-Fake
$script:FakeBlacklist = 'Some other wording: 10234'; $script:FakeStuck = $true
$odd = Clear-DeviceLeftovers 'ph' $devPackages 6>$null
Assert-Equal 'round 3: an Unknown first read still sends the (idempotent) removal' $true ($script:AdbCalls -contains 'shell cmd netpolicy remove restrict-background-blacklist 10234')
Assert-Equal 'check 2: an Unknown read-back is not "still set"' 0 $odd.Remaining.Count
Assert-Equal 'check 2: an Unknown read-back is reported apart' 1 $odd.Unknown.Count
Assert-Equal 'check 2: the Unknown read-back says the list could not be read' $true ($odd.Unknown[0] -like '*could not be read*')
Reset-Fake
$script:FakeBlacklist = 'Some other wording: nothing'
$unknownClean = Clear-DeviceLeftovers 'ph' $devPackages 6>$null
$restoreSrc = Get-Content (Join-Path $PSScriptRoot 'restore-device-state.ps1') -Raw
Assert-Equal 'check 2: a clean phone whose list has no label is Unknown, never Remaining' 0 $unknownClean.Remaining.Count
Assert-Equal 'check 2: restore-device-state exits with its own code for Unknown' $true ($restoreSrc -match 'UnknownExitCode = 5' -and $restoreSrc -match 'RESTORE UNKNOWN')
Assert-Equal 'check 2: uids on the lines after the label are read' 'Blocked' (Get-UidBlockState "Restrict background blacklisted UIDs:`n  10555`n  10234`nOther: x" 10234)
Assert-Equal 'check 2: a continuation list without the uid is Clean' 'Clean' (Get-UidBlockState "Restrict background blacklisted UIDs:`n  10555`n`nSome counter: 10234" 10234)
Assert-Equal 'check 2: a uid after the next label is not in the block' 'Clean' (Get-UidBlockState "Restrict background blacklisted UIDs:`n  10555`nRestrict background whitelisted UIDs:`n  10234" 10234)
Assert-Equal 'check 2: a uid after a blank line is not in the block' 'Clean' (Get-UidBlockState "Restrict background blacklisted UIDs:`n  10555`n`n  10234" 10234)
Assert-Equal 'item 1: null is no leftover' $false (Test-DataSyncTimeoutLeftover 'null')
Assert-Equal 'item 1: a number is a leftover' $true (Test-DataSyncTimeoutLeftover '30000')
$restoreScript = $restoreSrc
Assert-Equal 'item 1: the script resets the dev app only when something was found (or -Reset)' $true ($restoreScript -match 'Leftovers\.Count -gt 0 -or \$Reset')
Assert-Equal 'item 1: the script never touches wifi or data' $false ($restoreScript -match '\bsvc\b')
Assert-Equal 'item 1: preflight -Fix clears the leftovers' $true ((Get-Content (Join-Path $PSScriptRoot 'preflight.ps1') -Raw) -match 'Clear-DeviceLeftovers')

# ---- item 11 (round 3): the screen timeout recorded at the lease take is written back by one rule: only if the phone still holds the value this tooling set
$stayDir = Join-Path ([IO.Path]::GetTempPath()) ('rtst-stayon-test-' + [guid]::NewGuid().ToString('N'))
$deadPid = 2147483000   # no such process
try {
    Reset-Fake
    $now = [DateTime]::new(2026, 11, 1, 10, 0, 0, [DateTimeKind]::Utc)
    Assert-Equal 'round 3: nothing recorded: nothing to restore' $null (Invoke-StayOnRestore 'ph' 'KEY1' 'own' $stayDir)
    Assert-Equal 'round 3: nothing recorded: no adb call' 0 $script:AdbCalls.Count
    Assert-Equal 'round 3: orig and set are recorded' $true (Save-StayOnOriginal 'KEY1' '0' $stayDir $now 'own')
    $rec = Read-StayOnOriginal 'KEY1' $stayDir
    Assert-Equal 'round 3: the record holds orig' '0' "$($rec.orig)"
    Assert-Equal 'round 3: the record holds the value we set' '7' "$($rec.set)"
    Assert-Equal 'round 3: the record holds the pid' "$PID" "$($rec.pid)"
    Assert-Equal 'round 3: an existing record is never replaced or handed over' $false (Save-StayOnOriginal 'KEY1' '3' $stayDir $now 'other')
    Assert-Equal 'round 3: the record keeps its owner' 'own' (Read-StayOnOriginal 'KEY1' $stayDir).owner
    # branch: the phone still holds the value we set -> orig is written back, the record is deleted
    $script:FakeStayOn = '7'
    $done = Invoke-StayOnRestore 'ph' 'KEY1' 'own' $stayDir $now
    Assert-Equal 'round 3: current == set: orig is written back' $true ($script:AdbCalls -contains 'shell settings put global stay_on_while_plugged_in 0')
    Assert-Equal 'round 3: the result says restored' $true $done.Restored
    Assert-Equal 'round 3: the record is used up' $null (Read-StayOnOriginal 'KEY1' $stayDir)
    # branch: the user changed it since -> never undone, record still deleted
    Reset-Fake
    Save-StayOnOriginal 'KEY2' '0' $stayDir $now 'own' | Out-Null
    $script:FakeStayOn = '3'
    $kept = Invoke-StayOnRestore 'ph' 'KEY2' 'own' $stayDir $now
    Assert-Equal 'round 3: current != set: nothing is written' 0 @($script:AdbCalls | Where-Object { $_ -like '*settings put*' }).Count
    Assert-Equal 'round 3: the result says not restored' $false $kept.Restored
    Assert-Equal 'round 3: the record is deleted anyway' $null (Read-StayOnOriginal 'KEY2' $stayDir)
    # branch: a dead run's fresh record of another owner is healed by the same rule (never handed over)
    Reset-Fake
    Save-StayOnOriginal 'KEY3' 'null' $stayDir $now 'dead-run' $deadPid | Out-Null
    Assert-Equal 'round 3: a never-set value is recorded as 0' '0' (Read-StayOnOriginal 'KEY3' $stayDir).orig
    $script:FakeStayOn = '7'
    $healed = Invoke-StayOnRestore 'ph' 'KEY3' 'new-run' $stayDir $now
    Assert-Equal 'round 3: a dead run''s record is healed' $true $healed.Restored
    Assert-Equal 'round 3: a dead run''s record is gone' $null (Read-StayOnOriginal 'KEY3' $stayDir)
    # branch: dead pid, older than 24 h -> deleted without restoring
    Reset-Fake
    Save-StayOnOriginal 'KEY4' '0' $stayDir $now 'dead-run' $deadPid | Out-Null
    $script:FakeStayOn = '7'
    $old = Invoke-StayOnRestore 'ph' 'KEY4' 'new-run' $stayDir $now.AddHours(25)
    Assert-Equal 'round 3: a stale record is deleted' 'Delete' $old.Action
    Assert-Equal 'round 3: a stale record writes nothing' 0 @($script:AdbCalls | Where-Object { $_ -like '*settings put*' }).Count
    Assert-Equal 'round 3: a stale record is gone' $null (Read-StayOnOriginal 'KEY4' $stayDir)
    # pure action branches
    $fresh = [pscustomobject]@{ owner = 'a'; pid = 111; savedUtc = $now.ToString('o') }
    Assert-Equal 'round 3: own record restores' 'Restore' (Get-StayOnRecordAction $fresh 'a' 999 $false $false $now)
    Assert-Equal 'round 3: own record restores even with its pid alive' 'Restore' (Get-StayOnRecordAction $fresh 'a' 111 $true $true $now)
    Assert-Equal 'round 3: another owner''s live pid is left alone' 'LeaveAlone' (Get-StayOnRecordAction $fresh 'b' 999 $true $false $now)
    Assert-Equal 'round 3: another owner with a live lease is left alone' 'LeaveAlone' (Get-StayOnRecordAction $fresh 'b' 999 $false $true $now)
    Assert-Equal 'round 3: another owner, dead pid, no lease: healed' 'Restore' (Get-StayOnRecordAction $fresh 'b' 999 $false $false $now)
    Assert-Equal 'round 3: a caller with no owner heals a dead record' 'Restore' (Get-StayOnRecordAction $fresh '' 999 $false $false $now)
    Assert-Equal 'round 3: stale and dead: delete' 'Delete' (Get-StayOnRecordAction $fresh 'b' 999 $false $false $now.AddHours(30))
    Assert-Equal 'round 3: a record with no write time is stale' 'Delete' (Get-StayOnRecordAction ([pscustomobject]@{ owner = 'a'; pid = 1 }) 'b' 999 $false $false $now)
    # the lease flow: a take heals a dead record first, so the new record's orig is the user's value, not the dead run's 7
    Reset-Fake
    $leaseKey = 'KEY-LEASE'
    Save-StayOnOriginal $leaseKey '0' '' ([DateTime]::UtcNow) 'dead-run' $deadPid | Out-Null
    $script:FakeStayOn = '7'
    Assert-Equal 'round 3: take the lease' $true (Request-Lease $leaseKey 'run-a' 60)
    Assert-Equal 'round 3: the take writes a new record' $true (Save-StayOnForLease 'ph' $leaseKey 'run-a')
    Assert-Equal 'round 3: the dead run''s 7 was put back to 0 first' $true ($script:AdbCalls -contains 'shell settings put global stay_on_while_plugged_in 0')
    Assert-Equal 'round 3: the new record is the new owner''s' 'run-a' (Read-StayOnOriginal $leaseKey).owner
    $script:FakeStayOn = '0'
    $script:AdbCalls.Clear()
    Assert-Equal 'round 3: a non-owner releases nothing' $false (Close-PhoneLease 'ph' $leaseKey 'run-b')
    Assert-Equal 'round 3: a non-owner restores nothing' 0 @($script:AdbCalls | Where-Object { $_ -like '*settings put*' }).Count
    $script:FakeStayOn = '7'
    Assert-Equal 'round 3: the owner releases' $true (Close-PhoneLease 'ph' $leaseKey 'run-a' 6>$null)
    Assert-Equal 'round 3: the lease is gone' $null (Read-Lease $leaseKey)
    Assert-Equal 'round 3: the record is used up' $null (Read-StayOnOriginal $leaseKey)
    Reset-Fake
    Request-Lease $leaseKey 'run-a' 60 | Out-Null
    $script:FakeStayOn = '0'
    Save-StayOnForLease 'ph' $leaseKey 'run-a' | Out-Null
    $script:FakeStayOn = '3'
    $script:AdbCalls.Clear()
    Close-PhoneLease 'ph' $leaseKey 'run-a' 6>$null | Out-Null
    Assert-Equal 'round 3: release leaves a value the user changed since' 0 @($script:AdbCalls | Where-Object { $_ -like '*settings put*' }).Count
    Reset-Fake
    Assert-Equal 'round 3: preflight -Fix heals through the same function' $true ((Get-Content (Join-Path $PSScriptRoot 'preflight.ps1') -Raw) -match '(?s)if \(\$Fix\).*Invoke-StayOnRestore')
    $leaseScript = Get-Content (Join-Path $PSScriptRoot 'lease.ps1') -Raw
    Assert-Equal 'item 11: lease.ps1 -Release goes through Close-PhoneLease' $true ($leaseScript -match 'Close-PhoneLease')
    Assert-Equal 'review 2: the timed run releases through Close-PhoneLease, not Remove-Lease' $true (((Get-Content (Join-Path $PSScriptRoot 'run-checks.ps1') -Raw) -match 'Close-PhoneLease') -and ((Get-Content (Join-Path $PSScriptRoot 'run-checks.ps1') -Raw) -notmatch 'Remove-Lease'))
    Assert-Equal 'review 2: a plain preflight never records the setting' $false ((Get-Content (Join-Path $PSScriptRoot 'preflight.ps1') -Raw) -match 'Save-StayOn')
    Assert-Equal 'review 2: lease.ps1 -Take records it only when it is a new take' $true ($leaseScript -match '(?s)\$wasMine.*if \(-not \$wasMine\).*Save-StayOnForLease')
    Assert-Equal 'review 2: the timed run records it only when it takes the lease itself' $true ((Get-Content (Join-Path $PSScriptRoot 'run-checks.ps1') -Raw) -match 'if \(-not \$heldBy -and -not \$State\.Mine\)[^}]*Save-StayOnForLease')
    Assert-Equal 'review 2: no power key press is left in the release code' $false ((Get-Content (Join-Path $PSScriptRoot 'common.ps1') -Raw) -match 'PowerKeyEvent')

    # review 1: another owner's live lease stops the clear (unless -Force); the holder and an unleased phone are allowed
    Reset-Fake
    $env:RTST_PHONE_OWNER = 'me'
    $script:FakeDataSync = '30000'
    Assert-Equal 'review 1: another owner holds the lease' $true (Request-Lease 'FAKESERIAL1' 'someone-else' 60)
    $refused = $null
    try { Clear-DeviceLeftovers 'ph' $devPackages | Out-Null } catch { $refused = $_.Exception.Message }
    Assert-Equal 'review 1: the clear is refused' $true ([bool]$refused)
    Assert-Equal 'review 1: the refusal names the holder and -Force' $true ($refused -like '*someone-else*-Force*')
    Assert-Equal 'review 1: nothing was cleared' 0 @($script:AdbCalls | Where-Object { $_ -like '*device_config delete*' -or $_ -like '*netpolicy remove*' -or $_ -like '*am compat reset*' }).Count
    Assert-Equal 'review 1: -Force clears anyway' 1 (Clear-DeviceLeftovers 'ph' $devPackages -Force).Leftovers.Count
    Remove-Lease 'FAKESERIAL1' 'someone-else' | Out-Null
    Reset-Fake
    $script:FakeDataSync = '30000'
    Assert-Equal 'review 1: no lease: the clear runs' 1 (Clear-DeviceLeftovers 'ph' $devPackages).Leftovers.Count
    Request-Lease 'FAKESERIAL1' 'me' 60 | Out-Null
    Reset-Fake
    $script:FakeDataSync = '30000'
    Assert-Equal 'review 1: the holder itself may clear' 1 (Clear-DeviceLeftovers 'ph' $devPackages).Leftovers.Count
    Remove-Lease 'FAKESERIAL1' 'me' | Out-Null
    Remove-Item Env:RTST_PHONE_OWNER -ErrorAction SilentlyContinue
} finally { Remove-Item $stayDir, $script:LeaseDir -Recurse -Force -ErrorAction SilentlyContinue }

# ---- item 15: the device-side heal can be cancelled, an older one is cancelled when a new one starts, and a missing uid never reaches netpolicy
Reset-Fake
$script = Get-DeviceRestoreScript 'uk.sirous.rtst.dbg' 10234 240
Assert-Equal 'item 15: the heal records its pid' $true ($script -like 'echo $$ > */rtst-heal-uk.sirous.rtst.dbg.pid; sleep 240; *')
$cancel = Get-DeviceRestoreCancelCommand 'uk.sirous.rtst.dbg'
Assert-Equal 'item 15: the cancel kills the recorded pid and removes the file' $true ($cancel -like '*p=$(cat /data/local/tmp/rtst-heal-uk.sirous.rtst.dbg.pid)*kill $p*rm -f /data/local/tmp/rtst-heal-uk.sirous.rtst.dbg.pid*')
Assert-Equal 'check 2: the heal ignores TERM once its sleep is over, before the undo' $true ($script -like '*sleep 240; trap : TERM HUP INT; cmd netpolicy remove*')
Assert-Equal 'check 2: the cancel waits for a heal that is mid-undo and says finished' $true ($cancel -like '*kill -0 $p*sleep 1*echo stuck*echo cancelled*echo finished*')
Assert-Equal 'review 6: the pid is killed only when its command line holds the heal pid file path' $true ($cancel -like '*/proc/$p/cmdline*grep -qF /data/local/tmp/rtst-heal-uk.sirous.rtst.dbg.pid; then kill $p*')
$script:FakeHealPending = 'cancelled'
Assert-Equal 'item 15: a pending heal reports cancelled' $true (Stop-DeviceRestore 'ph' 'uk.sirous.rtst.dbg')
$script:FakeHealPending = 'stale'
Assert-Equal 'review 6: a stale pid file kills nothing and reports not cancelled' $false (Stop-DeviceRestore 'ph' 'uk.sirous.rtst.dbg' 6>$null)
$script:FakeHealPending = 'none'
Assert-Equal 'item 15: no heal pending reports not cancelled' $false (Stop-DeviceRestore 'ph' 'uk.sirous.rtst.dbg')
$script:AdbCalls.Clear()
Start-DeviceRestore 'ph' 'uk.sirous.rtst.dbg' 10234 240
Assert-Equal 'item 15: a new heal cancels the older one first' $true ($script:AdbCalls[0] -like '*/proc/$p/cmdline*' -and $script:AdbCalls[1] -like '*nohup sh -c*')
$paused = Get-Content (Join-Path $PSScriptRoot 'checks\paused-notification.ps1') -Raw
Assert-Equal 'item 15: the check skips netpolicy when no uid was read, and does not count it as restored' $true ($paused -match '(?s)function Set-Network.*?if \(-not \$script:AppUid\).*?not restored \(no uid\).*?return \$false')
Assert-Equal 'item 15: the check cancels the heal after a finished fast restore' $true ($paused -match 'Stop-HealWhenRestored \$networkRestored \$timeLimitRestored')
Assert-Equal 'item 15: the heal is left running when the fast restore failed' $true ($paused -match "if \(-not \(\`$NetworkRestored -and \`$TimeLimitRestored\)\)")
Assert-Equal 'item 15: the heal text says it does not move a pack back' $true ((Get-Content (Join-Path $PSScriptRoot 'common.ps1') -Raw) -match 'does NOT move a pack back')

# ---- item 22: the drop-cache marker pattern matches a log line that has the marker, and no backspace byte hides in the scripts
Assert-Equal 'item 22: the marker with exit code 0 is done' $true (Test-DropCacheDone 'RTST_DROP_RC=0')
Assert-Equal 'item 22: the marker inside a longer log line' $true (Test-DropCacheDone "sync`r`nRTST_DROP_RC=0`r`n")
Assert-Equal 'item 22: a failing exit code is not done' $false (Test-DropCacheDone 'RTST_DROP_RC=1')
Assert-Equal 'item 22: exit code 10 is not 0' $false (Test-DropCacheDone 'RTST_DROP_RC=10')
Assert-Equal 'item 22: no marker is not done' $false (Test-DropCacheDone 'Permission denied')
foreach ($file in 'common.ps1', 'run-checks.ps1') {
    $bytes = [IO.File]::ReadAllBytes((Join-Path $PSScriptRoot $file))
    Assert-Equal "item 22: $file has no backspace (0x08) byte" 0 @($bytes | Where-Object { $_ -eq 8 }).Count
}

# ---- item 23: the builds start no shared build server that outlives them
Assert-Equal 'item 23: the phone build command disables the build servers' $true ((Get-PhoneBuildArguments 'P' 'A' 'uk.sirous.readthestupidtext.dbg' 'android-arm64' $false) -contains '--disable-build-servers')
$tools = Join-Path $PSScriptRoot '..'
Assert-Equal 'item 23: the emulator build disables the build servers' $true ((Get-Content (Join-Path $tools 'emulator\install-debug.ps1') -Raw) -match 'dotnet build \$project[^\r\n]*--disable-build-servers')
$vmBuild = Get-Content (Join-Path $tools 'desktop-vm\Build-TestPackage.ps1') -Raw
Assert-Equal 'item 23: the VM publish disables the build servers' $true ($vmBuild -match 'dotnet publish[^\r\n]*--disable-build-servers')
Assert-Equal 'item 23: the VM msbuild turns node reuse off' $true ($vmBuild -match 'msbuild[^\r\n]*-nodeReuse:false')

# ---- item 6: a failed build's log is kept past the next build
$runnerSource = Get-Content (Join-Path $PSScriptRoot 'run-checks.ps1') -Raw
Assert-Equal 'item 6: a failed build keeps its log' $true (([regex]::Matches($runnerSource, 'Save-FailedBuildLog \$')).Count -ge 2)

# ---- item 17 hook: the PowerShell lint settings file exists and the workflow runs it
Assert-Equal 'item 17: the analyzer settings file exists' $true (Test-Path (Join-Path $PSScriptRoot '..\..\PSScriptAnalyzerSettings.psd1'))

if ($failed) { Write-Host "$failed phone-leftovers checks failed (PowerShell $($PSVersionTable.PSVersion))."; exit 1 }
Write-Host "All phone-leftovers checks pass (PowerShell $($PSVersionTable.PSVersion))."
exit 0
