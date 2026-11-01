<#
.SYNOPSIS
  Self-test for the Slice 206 helpers in common.ps1: the phone lease (take, held, expired, release), the dev-app allow-list, the lock-state and df parsers.
  Run it in both `powershell` (5.1) and `pwsh`. Needs no device; the lease files go to a temp folder.
#>
$ErrorActionPreference = 'Stop'
if (-not $env:ANDROID_HOME -and -not $env:ANDROID_SDK_ROOT) { $env:ANDROID_HOME = $env:TEMP }  # common.ps1 only needs the variable to exist
. (Join-Path $PSScriptRoot 'common.ps1')
$script:LeaseDir = Join-Path ([IO.Path]::GetTempPath()) ('rtst-lease-test-' + [guid]::NewGuid().ToString('N'))

$failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

try {
    $serial = 'adb-TEST123-abc._adb-tls-connect._tcp'
    $now = 1000000
    Assert-Equal 'no lease is free' $null (Read-Lease $serial $now)
    Assert-Equal 'take' $true (Request-Lease $serial 'agent-a' 120 $now)
    Assert-Equal 'owner recorded' 'agent-a' (Read-Lease $serial ($now + 1)).Owner
    Assert-Equal 'expiry recorded' ($now + 7200) (Read-Lease $serial ($now + 1)).Expires
    Assert-Equal 'held: another owner cannot take' $false (Request-Lease $serial 'agent-b' 60 ($now + 10))
    Assert-Equal 'held: holder unchanged' 'agent-a' (Read-Lease $serial ($now + 10)).Owner
    Assert-Equal 'same owner renews' $true (Request-Lease $serial 'agent-a' 240 ($now + 20))
    Assert-Equal 'renewed expiry' ($now + 20 + 14400) (Read-Lease $serial ($now + 21)).Expires
    Assert-Equal 'other owner cannot release' $false (Remove-Lease $serial 'agent-b' ($now + 30))
    Assert-Equal 'still held after refused release' 'agent-a' (Read-Lease $serial ($now + 31)).Owner
    Assert-Equal 'expired lease is free' $null (Read-Lease $serial ($now + 20 + 14400))
    Assert-Equal 'expired: another owner takes it' $true (Request-Lease $serial 'agent-b' 60 ($now + 20 + 14400))
    Assert-Equal 'release by owner' $true (Remove-Lease $serial 'agent-b' ($now + 20 + 14401))
    Assert-Equal 'released is free' $null (Read-Lease $serial ($now + 20 + 14402))
    Assert-Equal 'lease file name is sanitised' $true ((Get-LeasePath 'a/b:c') -match 'lease-a_b_c\.json$')
    Set-Content (Get-LeasePath $serial) '{ not json'
    $corruptPath = Get-LeasePath $serial
    $written = [DateTimeOffset]::new((Get-Item $corruptPath).LastWriteTimeUtc).ToUnixTimeSeconds()
    $limit = $script:MaxLeaseMinutes * 60
    Assert-Equal 'corrupt lease is held (fail closed)' $script:CorruptLeaseOwner (Read-Lease $serial $written).Owner
    Assert-Equal 'corrupt lease: nobody can take it' $false (Request-Lease $serial 'agent-a' 60 $written)
    Assert-Equal 'corrupt lease expiry = file time + the longest lease' ($written + $limit) (Read-Lease $serial $written).Expires
    Assert-Equal 'corrupt lease older than the longest lease is free' $null (Read-Lease $serial ($written + $limit + 1))
    Assert-Equal 'corrupt lease just under the limit is still held' $script:CorruptLeaseOwner (Read-Lease $serial ($written + $limit - 1)).Owner
    Remove-Item $corruptPath -Force
    Assert-Equal 'corrupt lease removed by hand is free' $null (Read-Lease $serial $written)
    # Renewal: only the owner, only a live lease; scripts renew their own lease when under 15 minutes remain.
    Assert-Equal 'renew with no lease' 'NotHeld' (Update-Lease $serial 'agent-a' 60 $now)
    Request-Lease $serial 'agent-a' 20 $now | Out-Null
    Assert-Equal 'renew by another owner' 'Other' (Update-Lease $serial 'agent-b' 60 ($now + 5))
    Assert-Equal 'renew by the owner' 'Renewed' (Update-Lease $serial 'agent-a' 60 ($now + 5))
    Assert-Equal 'renewed expiry' ($now + 5 + 3600) (Read-Lease $serial ($now + 6)).Expires
    Remove-Lease $serial 'agent-a' ($now + 7) | Out-Null
    $soon = [pscustomobject]@{ Owner = 'agent-a'; Taken = $now; Expires = $now + 14 * 60 }
    $later = [pscustomobject]@{ Owner = 'agent-a'; Taken = $now; Expires = $now + 16 * 60 }
    Assert-Equal 'renewal needed under 15 min' $true (Test-LeaseNeedsRenewal $soon 'agent-a' $now)
    Assert-Equal 'no renewal at 16 min' $false (Test-LeaseNeedsRenewal $later 'agent-a' $now)
    Assert-Equal 'no renewal of another owner lease' $false (Test-LeaseNeedsRenewal $soon 'agent-b' $now)
    Assert-Equal 'no renewal without a lease' $false (Test-LeaseNeedsRenewal $null 'agent-a' $now)
    foreach ($bad in 0, -5, 1441) {
        $refused = $false
        try { Request-Lease $serial 'agent-a' $bad $now | Out-Null } catch { $refused = $true }
        Assert-Equal "lease minutes $bad refused" $true $refused
    }
    # A lease file that is held open (a sharing violation) is read as held, never as free.
    Assert-Equal 'take for the lock test' $true (Request-Lease $serial 'agent-a' 60 $now)
    $stream = [IO.File]::Open((Get-LeasePath $serial), 'Open', 'Write', 'None')
    try { Assert-Equal 'unreadable lease is held' $script:UnreadableLeaseOwner (Read-Lease $serial ($now + 1)).Owner } finally { $stream.Dispose() }
    # Two processes racing to take a free lease: exactly one wins.
    Remove-Lease $serial 'agent-a' $now | Out-Null
    $engine = (Get-Process -Id $PID).Path
    $race = 'a', 'b' | ForEach-Object {
        Start-Process $engine -ArgumentList '-NoProfile', '-Command', "`$env:ANDROID_HOME='$env:ANDROID_HOME'; . '$PSScriptRoot\common.ps1'; `$script:LeaseDir='$($script:LeaseDir)'; if (Request-Lease '$serial' 'racer-$_' 60) { exit 10 } else { exit 11 }" -PassThru -WindowStyle Hidden
    }
    $race | Wait-Process -Timeout 60
    Assert-Equal 'exactly one racer takes the lease' 1 @($race | Where-Object { $_.ExitCode -eq 10 }).Count
    Assert-Equal 'the other racer is refused' 1 @($race | Where-Object { $_.ExitCode -eq 11 }).Count
} finally {
    Remove-Item $script:LeaseDir -Recurse -Force -ErrorAction SilentlyContinue
}

# The reset allow-list: only the two .dbg dev packages; the store packages and look-alikes are refused.
foreach ($ok in 'uk.sirous.readthestupidtext.dbg', 'uk.sirous.dinoreader.dbg') {
    try { Assert-DevPackage $ok; Assert-Equal "allow $ok" 1 1 } catch { $failed++; Write-Host "FAIL: $ok was refused" }
}
foreach ($bad in 'uk.sirous.readthestupidtext', 'uk.sirous.dinoreader', 'uk.sirous.readthestupidtext.dbg2', 'UK.SIROUS.READTHESTUPIDTEXT.DBG', 'com.android.settings', '') {
    $refused = $false
    try { Assert-DevPackage $bad } catch { $refused = $true }
    Assert-Equal "refuse [$bad]" $true $refused
}
Assert-Equal 'rtst maps to the dev package' 'uk.sirous.readthestupidtext.dbg' (Get-DevPackage 'rtst')
Assert-Equal 'dino maps to the dev package' 'uk.sirous.dinoreader.dbg' (Get-DevPackage 'dino')

# Lock state: a secure keyguard wins over anything else; only an explicit false or a disabled lock is open; the rest is unknown.
Assert-Equal 'secure true' 'Secure' (Resolve-LockState "  isKeyguardSecure=true`n  mShowingLockscreen=false" 'false')
Assert-Equal 'contradictory reads are Secure (NO-GO)' 'Secure' (Resolve-LockState "isKeyguardSecure=false`nisKeyguardSecure=true" 'true')
Assert-Equal 'secure true beats a false' 'Secure' (Resolve-LockState "isKeyguardSecure=false`nKeyguardServiceDelegate: showing=false secure=true" '')
Assert-Equal 'secure false' 'Open' (Resolve-LockState 'isKeyguardSecure=false' 'false')
Assert-Equal 'cached delegate secure=false alone is not Open' 'Unknown' (Resolve-LockState 'KeyguardServiceDelegate: showing=true secure=false' '')
Assert-Equal 'delegate secure=true is Secure' 'Secure' (Resolve-LockState 'isKeyguardSecure=false KeyguardServiceDelegate: secure=true' '')
Assert-Equal 'empty reads are Unknown' 'Unknown' (Resolve-LockState '' '')
Assert-Equal 'lock disabled' 'Open' (Resolve-LockState 'nothing useful' 'true')
Assert-Equal 'nothing readable' 'Unknown' (Resolve-LockState 'nothing useful' 'Unknown command: get-disabled')

Assert-Equal 'df available KB' 41943040 (ConvertFrom-DfAvailableKb @('Filesystem 1K-blocks Used Available Use% Mounted on', '/dev/block/dm-9 113000000 71000000 41943040 63% /data'))
Assert-Equal 'df unreadable' -1 (ConvertFrom-DfAvailableKb @('df: /data: No such file'))

# Packs listing: a run-as failure is never "no packs".
Assert-Equal 'packs listed' 'Packs' (Resolve-PacksListing @('voice-a', 'voice-b') 0)
Assert-Equal 'no folder' 'None' (Resolve-PacksListing @('ls: files/packs: No such file or directory') 1)
Assert-Equal 'empty folder' 'None' (Resolve-PacksListing @() 0)
Assert-Equal 'run-as refused' 'Error' (Resolve-PacksListing @('run-as: package not debuggable: uk.sirous.x') 1)
Assert-Equal 'run-as no such file is still an error' 'Error' (Resolve-PacksListing @('run-as: couldn''t stat /data/user/0/x: No such file or directory') 1)
Assert-Equal 'unknown failure' 'Error' (Resolve-PacksListing @('error: closed') 1)
# Real S21 output of the pack probe for two packs that are not installed: the last `find` exits 1 but run-as worked.
Assert-Equal 'probe of absent packs, exit 1' 'Packs' (Resolve-PacksListing @('ACTIVE piper-fa', 'ACTIVE piper-nb') 1)
Assert-Equal 'tar file count ignores directories' 2 (Get-TarFileCount @('files/packs/', 'files/packs/a/', 'files/packs/a/m.onnx', 'files/packs/a/t.txt'))
Assert-Equal 'empty tar listing' 0 (Get-TarFileCount @())

# Stay-awake: the bit of the charger in use must be set (1 AC, 2 USB, 4 wireless).
Assert-Equal 'charger AC' 1 (Get-ChargerStayOnBit "Current Battery Service state:`n  AC powered: true`n  USB powered: false`n  Wireless powered: false")
Assert-Equal 'charger USB' 2 (Get-ChargerStayOnBit "  AC powered: false`n  USB powered: true`n  Wireless powered: false")
Assert-Equal 'charger wireless' 4 (Get-ChargerStayOnBit "  AC powered: false`n  USB powered: false`n  Wireless powered: true")
Assert-Equal 'on battery' 0 (Get-ChargerStayOnBit "  AC powered: false`n  USB powered: false`n  Wireless powered: false")
Assert-Equal 'value 1 on USB does not cover' $false (Test-StayOnCovers '1' 2)
Assert-Equal 'value 1 on AC covers' $true (Test-StayOnCovers '1' 1)
Assert-Equal 'value 7 covers wireless' $true (Test-StayOnCovers '7' 4)
Assert-Equal 'value 0 covers nothing' $false (Test-StayOnCovers '0' 1)
Assert-Equal 'unreadable value covers nothing' $false (Test-StayOnCovers 'null' 1)
Assert-Equal 'no charger covers nothing' $false (Test-StayOnCovers '7' 0)

# Markers: only "No such file" is absent; denied or garbage is neither yes nor no.
Assert-Equal 'marker present' $true (Resolve-MarkerListing '/sdcard/Android/data/x/files/throttle-pack-downloads' 'throttle-pack-downloads')
Assert-Equal 'marker absent' $false (Resolve-MarkerListing 'ls: /sdcard/Android/data/x/files/throttle-pack-downloads: No such file or directory' 'throttle-pack-downloads')
foreach ($unclear in 'ls: /sdcard/Android/data/x/files: Permission denied', '', 'error: closed') {
    $threw = $false
    try { Resolve-MarkerListing $unclear 'throttle-pack-downloads' | Out-Null } catch { $threw = $true }
    Assert-Equal "marker answer [$unclear] throws" $true $threw
}

# Aside archives: keep the newest 3, delete only files named like ours.
$names = 'packs-20261030-090000.tar', 'packs-20261030-100000.tar', 'packs-20261029-090000.tar', 'packs-20261031-080000.tar', 'packs-20261028-090000.tar', 'notes.tar', 'packs-x.tar', 'packs-20261030-090000.tar.part'
$prune = @(Get-ArchivesToPrune $names 3)
Assert-Equal 'prune count' 2 $prune.Count
Assert-Equal 'prune the older of the two' 'packs-20261029-090000.tar' $prune[0]
Assert-Equal 'prune the oldest' 'packs-20261028-090000.tar' $prune[1]
Assert-Equal 'nothing to prune at 3' 0 @(Get-ArchivesToPrune $names[0..2] 3).Count
Assert-Equal 'nothing to prune when empty' 0 @(Get-ArchivesToPrune @() 3).Count

# logcat headers do not count as log lines.
Assert-Equal 'headers only' 0 (Select-RealLogLines @('--------- beginning of main', '--------- beginning of system', '')).Count
Assert-Equal 'real line counts' 1 (Select-RealLogLines @('--------- beginning of main', '10-30 12:00:00.000 100 100 I App: hi')).Count

# Preferences: per-app keys, dots are literal.
$prefs = '<map><string name="Language">nb</string><string name="dino.ui.language">no</string></map>'
Assert-Equal 'RTST key' 'nb' (Get-PrefValue $prefs 'Language')
Assert-Equal 'Dino key' 'no' (Get-PrefValue $prefs 'dino.ui.language')
Assert-Equal 'missing key' $null (Get-PrefValue $prefs 'dinoXui.language')

# Prefs file is found by the language key, with <package>_preferences.xml as the fallback.
$pkg = 'uk.sirous.readthestupidtext.dbg'
$files = @('other.xml', "${pkg}_preferences.xml")
Assert-Equal 'file holding the key' "${pkg}_preferences.xml" (Select-PrefsFileByKey $files @{ 'other.xml' = '<map/>'; "${pkg}_preferences.xml" = $prefs } 'Language' $pkg)
Assert-Equal 'fallback when no key' "${pkg}_preferences.xml" (Select-PrefsFileByKey $files @{} 'Language' $pkg)
Assert-Equal 'no file' $null (Select-PrefsFileByKey @('other.xml') @{} 'Language' $pkg)

# ---- voice pack layout (Slice 213 redesign): files/packs/<id>/active names the version folder; that folder must hold pack.json
$idsAll = @('supertonic-3', 'kokoro-en')
$installed = @('ACTIVE supertonic-3 v4', 'files/packs/supertonic-3/v4/pack.json', 'ACTIVE kokoro-en')
Assert-Equal 'installed: active + pack.json in its folder' 'supertonic-3' (@(Resolve-InstalledPackIds $installed $idsAll) -join ',')
Assert-Equal 'Test-PackInstalled true when installed' $true (Test-PackInstalled $installed $idsAll)
Assert-Equal 'partial only: nothing installed' $false (Test-PackInstalled @('ACTIVE supertonic-3 ', 'ACTIVE kokoro-en ') $idsAll)
Assert-Equal 'active without pack.json: not installed' $false (Test-PackInstalled @('ACTIVE supertonic-3 v4') @('supertonic-3'))
Assert-Equal 'pack.json without active: not installed' $false (Test-PackInstalled @('ACTIVE supertonic-3', 'files/packs/supertonic-3/v4/pack.json') @('supertonic-3'))
$twoVersions = @('ACTIVE supertonic-3 v4', 'files/packs/supertonic-3/v3/pack.json', 'files/packs/supertonic-3/v4/pack.json')
Assert-Equal 'two versions: the active one has pack.json' $true (Test-PackInstalled $twoVersions @('supertonic-3'))
Assert-Equal 'two versions: active points at a folder without it' $false (Test-PackInstalled @('ACTIVE supertonic-3 v5', 'files/packs/supertonic-3/v4/pack.json') @('supertonic-3'))
Assert-Equal 'another pack''s pack.json does not count' $false (Test-PackInstalled @('ACTIVE supertonic-3 v4', 'files/packs/kokoro-en/v4/pack.json') @('supertonic-3'))
Assert-Equal 'a run-as error line is not a pack' $false (Test-PackInstalled @('run-as: package not debuggable: x') @('supertonic-3'))
$probe = New-PackProbeCommand 'uk.sirous.readthestupidtext.dbg' $idsAll
Assert-Equal 'probe names the package and ids' $true ($probe -like "run-as uk.sirous.readthestupidtext.dbg sh -c 'for p in supertonic-3 kokoro-en;*")
Assert-Equal 'probe reads the active file' $true ($probe -like '*cat files/packs/$p/active*')
Assert-Equal 'probe finds pack.json' $true ($probe -like '*find files/packs/$p -maxdepth 2 -name pack.json*')
Assert-Equal 'pack folders skip .zip.partial files' 'supertonic-3,kokoro-en' ((Get-PackFolderNames @('supertonic-3', 'supertonic-3-4.zip.partial', 'kokoro-en', 'x.zip')) -join ',')
$tar = @('files/packs/supertonic-3/', 'files/packs/supertonic-3/active', 'files/packs/supertonic-3/v4/', 'files/packs/supertonic-3/v4/pack.json', 'files/packs/kokoro-en/active', 'files/packs/kokoro-en/v1/model.onnx')
Assert-Equal 'archive packs: complete ones only' 'supertonic-3' ((Get-ArchivePackIds $tar) -join ',')
Assert-Equal 'a .partial in an archive is no pack' 0 @(Get-ArchivePackIds @('files/packs/supertonic-3-4.zip.partial')).Count

# Slice 208: a whole-device network cut is refused over a wireless serial (the S21 went offline 2026-10-31 when a check cut the link it ran over).
function Test-Throws([scriptblock]$Block) { try { & $Block; return $false } catch { return $true } }
Assert-Equal 'refuses ip:port serial' $true (Test-Throws { Assert-WholeDeviceNetworkCutAllowed '192.168.1.20:37123' })
Assert-Equal 'refuses mdns serial' $true (Test-Throws { Assert-WholeDeviceNetworkCutAllowed 'adb-R5CT123ABC-xYz12a._adb-tls-connect._tcp' })
Assert-Equal 'allows an emulator' $false (Test-Throws { Assert-WholeDeviceNetworkCutAllowed 'emulator-5554' })
$restore = Get-DeviceRestoreScript 'uk.sirous.rtst.dbg' 10234 240
Assert-Equal 'restore waits first' $true ($restore.StartsWith('sleep 240; '))
Assert-Equal 'restore lifts the app block' $true ($restore -like '*netpolicy remove restrict-background-blacklist 10234*')
Assert-Equal 'restore deletes the timeout' $true ($restore -like '*device_config delete activity_manager data_sync_fgs_timeout_duration*')
Assert-Equal 'restore resets the compat flag' $true ($restore -like '*am compat reset FGS_INTRODUCE_TIME_LIMITS uk.sirous.rtst.dbg*')
Assert-Equal 'paused check never calls svc' $false ((Get-Content (Join-Path $PSScriptRoot 'checks\paused-notification.ps1') -Raw) -match "'svc'")

if ($failed) { Write-Host "$failed phone-lib checks failed (PowerShell $($PSVersionTable.PSVersion))."; exit 1 }
Write-Host "All phone-lib checks pass (PowerShell $($PSVersionTable.PSVersion))."
exit 0
