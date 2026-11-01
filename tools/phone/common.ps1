# Shared helpers for the real-phone scripts (Slice 165). Dot-sourced, not run. Wireless adb only, never USB.
$script:SdkRoot = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { $env:ANDROID_SDK_ROOT }
if (-not $script:SdkRoot) { throw 'ANDROID_HOME (or ANDROID_SDK_ROOT) is not set.' }
$script:Adb = Join-Path $script:SdkRoot 'platform-tools\adb.exe'
$script:RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
. (Join-Path $PSScriptRoot 'quoting.ps1')
# Wireless debugging shows up as `adb-<serial>-<suffix>._adb-tls-connect._tcp` or `<ip>:<port>`; emulators and USB serials never do.
$script:WirelessPattern = '^(adb-.+_adb-tls-connect\._tcp|(?!127\.)\d{1,3}(\.\d{1,3}){3}:\d+)$'

$script:MdnsTimeoutSeconds = 10
$script:DevicesTimeoutSeconds = 15
function Get-AdbDevices {
    # mdns only refreshes the wireless list: a hang there is said but not fatal. `adb devices` hanging is a dead adb server and fails loudly.
    try { Invoke-Adb '' @('mdns', 'services') $script:MdnsTimeoutSeconds | Out-Null } catch { Write-Warning $_.Exception.Message }
    $lines = Invoke-Adb '' @('devices') $script:DevicesTimeoutSeconds
    $lines | Where-Object { $_ -match '^(\S+)\s+(device|offline|unauthorized)\s*$' } |
        ForEach-Object { [pscustomobject]@{ Serial = $Matches[1]; State = $Matches[2] } }
}

# The wireless phone that is ready, or $null. With -Serial, only that exact serial counts.
function Find-WirelessPhone([string]$Serial) {
    $devices = @(Get-AdbDevices | Where-Object { $_.State -eq 'device' -and $_.Serial -match $script:WirelessPattern })
    if ($Serial) { $devices = @($devices | Where-Object { $_.Serial -eq $Serial }) }
    return $devices | Select-Object -First 1 -ExpandProperty Serial
}

function Write-PairingAsk {
    Write-Host 'The phone is not connected over wireless debugging. I need two things from you:'
    Write-Host '  1. On the phone: Settings > Developer options > Wireless debugging > Pair device with pairing code.'
    Write-Host '  2. Tell me the pairing IP:port and the 6-digit code shown there, then the IP:port on the main Wireless debugging screen.'
    Write-Host 'Then run: connect.ps1 -PairAddress <ip:port> -PairCode <6 digits> -ConnectAddress <ip:port>'
    Write-Host '(Wireless debugging only. USB is never used.)'
}

# Every adb call has a time limit, so a dropped wireless link fails loudly instead of blocking a wait loop in silence.
# install, pull and push move whole files, so they get the long limit.
. (Join-Path $PSScriptRoot 'deadline.ps1')
$script:AdbTimeoutSeconds = 30
$script:AdbTransferTimeoutSeconds = 300
# Kill(bool) (the process tree) exists only in .NET Core; Windows PowerShell 5.1 has Kill() alone. A kill that fails is said, never swallowed.
function Stop-HungProcess([Diagnostics.Process]$Process) {
    try { $Process.Kill($true); return } catch [System.Management.Automation.MethodException] { }
    try { $Process.Kill() } catch { Write-Warning "Could not stop the hung adb process (id $($Process.Id)): $($_.Exception.Message)" }
}

function Invoke-Adb([string]$Serial, [string[]]$Arguments, [int]$TimeoutSeconds = 0) {
    if ($TimeoutSeconds -le 0) { $TimeoutSeconds = if ($Arguments[0] -in 'install', 'pull', 'push') { $script:AdbTransferTimeoutSeconds } else { $script:AdbTimeoutSeconds } }
    $TimeoutSeconds = Limit-SecondsToDeadline $TimeoutSeconds (Get-ActiveDeadline) (Get-Date)   # inside a run-checks check: never past the budget (Slice 213)
    $info = New-Object Diagnostics.ProcessStartInfo $script:Adb
    # Windows PowerShell 5.1 (.NET Framework) has no ArgumentList: there the arguments go in one quoted string.
    # An empty $Serial is a call that names no device (adb devices, adb mdns services).
    $full = if ($Serial) { @('-s', $Serial) + $Arguments } else { $Arguments }
    if ($null -ne $info.ArgumentList) { foreach ($a in $full) { $info.ArgumentList.Add($a) } }
    else { $info.Arguments = ($full | ForEach-Object { ConvertTo-WindowsArgument $_ }) -join ' ' }
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $proc = [Diagnostics.Process]::Start($info)
    $out = $proc.StandardOutput.ReadToEndAsync(); $err = $proc.StandardError.ReadToEndAsync()
    if (-not $proc.WaitForExit($TimeoutSeconds * 1000)) {
        Stop-HungProcess $proc
        if (Test-PastDeadline (Get-ActiveDeadline) (Get-Date)) { throw (New-BudgetExceededMessage "waiting for 'adb $($full -join ' ')'") }
        throw "Phone not responding over adb: 'adb $($full -join ' ')' did not finish in $TimeoutSeconds s."
    }
    $proc.WaitForExit()
    $global:LASTEXITCODE = $proc.ExitCode
    return @(($out.Result + $err.Result) -split "`r?`n" | Where-Object { $_ -ne '' })
}

# Sends a Debug test hook to the running dev build; returns the phone's clock just before the send (one shell call, so adb latency sits in neither).
# Values are single-quoted for the phone's shell, with any single quote escaped.
# The broadcast action of the app with this test scheme: uk.sirous.rtst-test.TEST_HOOK or uk.sirous.dino-test.TEST_HOOK. A wrong action fails silently
# (no error, no log line), so the scripts name it when no HOOK line arrives.
$script:HookActionFormat = 'uk.sirous.{0}.TEST_HOOK'

function Send-TimedHook([string]$Serial, [string]$Scheme, [string]$Command, [hashtable]$HookArgs) {
    $extras = ($HookArgs.GetEnumerator() | ForEach-Object { $quoted = $_.Value.Replace("'", "'\''"); "--es $($_.Key) '$quoted'" }) -join ' '
    $out = Invoke-Adb $Serial @('shell', "date +%s.%N; am broadcast -a $($script:HookActionFormat -f $Scheme) --es cmd $Command $extras")
    return [double]("$(@($out)[0])".Trim())
}

# Slice 180: what keeps a run from waiting in silence. `svc power stayon true` only works while charging, so a phone on battery dozes and Android freezes the app.
function Get-PowerState([string]$Serial) {
    $battery = "$(Invoke-Adb $Serial @('shell', 'dumpsys battery'))"
    $wake = "$(Invoke-Adb $Serial @('shell', 'dumpsys power | grep mWakefulness='))"
    $powered = $battery -match '(AC|USB|Wireless) powered: true'
    $state = if ($wake -match 'mWakefulness=(\w+)') { $Matches[1] } else { 'unknown' }
    return [pscustomobject]@{ Powered = $powered; Wakefulness = $state }
}

# Bit of settings global stay_on_while_plugged_in that matches the charger in use (1 AC, 2 USB, 4 wireless); 0 when not charging.
function Get-ChargerStayOnBit([string]$BatteryText) {
    if ($BatteryText -match 'AC powered: true') { return 1 }
    if ($BatteryText -match 'USB powered: true') { return 2 }
    if ($BatteryText -match 'Wireless powered: true') { return 4 }
    return 0
}

function Test-StayOnCovers([string]$Value, [int]$Bit) {
    return ($Bit -gt 0 -and $Value -match '^\d+$' -and (([int]$Value -band $Bit) -ne 0))
}

function Write-PowerState([string]$Serial) {
    $power = Get-PowerState $Serial
    Write-Host ("   phone: {0}, {1}" -f $(if ($power.Powered) { 'on charger (AC/USB/wireless powered)' } else { 'ON BATTERY' }), $power.Wakefulness)
    if (-not $power.Powered) { Write-Host '   WARNING: plug the phone in. On battery it dozes and Android freezes the app; a keep-awake loop runs for this run, but it is only a fallback.' }
    return $power
}

# A background job that wakes the screen every 15 s (a key event only: no unlock, no taps). Always stop it with Stop-KeepAwake.
function Start-KeepAwake([string]$Serial) {
    return Start-Job -ArgumentList $script:Adb, $Serial -ScriptBlock {
        param($adb, $serial)
        while ($true) {
            & $adb -s $serial shell input keyevent KEYCODE_WAKEUP 2>&1 | Out-Null
            Start-Sleep -Seconds 15
        }
    }
}

function Stop-KeepAwake($Job) {
    if (-not $Job) { return }
    Stop-Job $Job -ErrorAction SilentlyContinue
    Remove-Job $Job -Force -ErrorAction SilentlyContinue
}

# ---- Slice 206: unattended-run helpers (lease, dev-app allow-list, lock/df/UI-dump parsing). The parsers are pure so test-phone-lib.ps1 and test-tap.ps1 need no device.
. (Join-Path $PSScriptRoot '..\emulator\activity.ps1')
$script:ExitNodeNotFound = 3

# The only packages the reset script may clear, force-stop or reinstall: the Debug dev installs. The store packages are never in this list.
$script:DevApps = @{
    rtst = @{ Package = 'uk.sirous.readthestupidtext.dbg'; Scheme = 'rtst-test' }
    dino = @{ Package = 'uk.sirous.dinoreader.dbg'; Scheme = 'dino-test' }
}
function Assert-DevPackage([string]$Package) {
    $allowed = @($script:DevApps.Values | ForEach-Object { $_.Package })
    if ($allowed -cnotcontains $Package) { throw "Refusing to touch '$Package': only the Debug dev installs ($($allowed -join ', ')) may be reset." }
}

function Get-DevPackage([string]$App) {
    $package = $script:DevApps[$App].Package
    Assert-DevPackage $package
    return $package
}

# --- Phone lease: one session or agent holds the phone at a time. The lease helpers are shared with the emulator (tools/lease-common.ps1, Slice 207).
# File <worktrees>\.phone\lease-<key>.json. The key is the phone's own `getprop ro.serialno` (Get-PhoneLeaseKey), not the adb
# serial, which differs between the mdns name and ip:port and changes with every Wireless debugging toggle. No lease at all is allowed.
. (Join-Path $PSScriptRoot '..\lease-common.ps1')
$script:LeaseOwnerVariable = 'RTST_PHONE_OWNER'

# The lease owner of a run-checks run: the caller's owner variable, else one name per phone (run-checks-<phone key>), so two default runs on one phone share an owner
# and the run marker refuses the second (Slice 221 item 6); a pid-based default would differ per shell and never match.
function Get-RunChecksOwner([string]$PhoneKey) {
    foreach ($name in $script:DeviceOwnerVariable, 'RTST_PHONE_OWNER') {
        $value = [Environment]::GetEnvironmentVariable($name)
        if ($value) { return $value }
    }
    return "run-checks-$PhoneKey"
}

function Get-PhoneLeaseKey([string]$AdbSerial) {
    $id = "$(Invoke-Adb $AdbSerial @('shell', 'getprop', 'ro.serialno'))".Trim()
    if ($id -notmatch '^[\w.-]{3,}$') { throw "Could not read the phone's ro.serialno (got '$id'): cannot key the phone lease." }
    return $id
}

# Exits 4 when a live lease belongs to someone other than $env:RTST_PHONE_OWNER; no lease means allowed (older scripts keep working).
# The caller's own lease is renewed when under 15 minutes remain, so a long run does not lose the phone to another session half-way.
function Assert-PhoneLease([string]$Serial) {
    $key = Get-PhoneLeaseKey $Serial
    $held = Read-Lease $key
    $me = [Environment]::GetEnvironmentVariable($script:LeaseOwnerVariable)
    if ($me -and (Test-LeaseNeedsRenewal $held $me (Get-UnixNow))) { Update-Lease $key $me $script:DefaultLeaseMinutes | Out-Null }
    if (-not $held -or $held.Owner -ceq $me) { return }
    Write-Host "Phone $key is leased by $(Format-LeaseHolder $held); this session's owner is '$me' (set `$env:$($script:LeaseOwnerVariable) to the lease owner, or wait). Not touching the phone."
    exit $script:ExitLeaseHeld
}

# --- Screen lock. The reads are NOT confirmed on a device running Android 15 (the S21 was busy): the keyguard flags in `dumpsys window` and
# `cmd lock_settings get-disabled` (a command of the lock_settings service, see `cmd lock_settings help`). Only an explicit "secure=false"
# or "disabled" counts as open; anything else is Unknown, which preflight treats as NO-GO rather than guessing.
$script:KeyguardSecurePattern = '(?i)(?:\bis)?keyguard\s*secure\b\W{0,3}(true|false)'
$script:KeyguardDelegatePattern = '(?i)KeyguardServiceDelegate[^\r\n]*\bsecure=(true|false)'
# A "true" anywhere is Secure (so contradictory reads are NO-GO too). Open needs a positive read: isKeyguardSecure=false (the window manager's own answer),
# or lock_settings saying the lock screen is disabled. The KeyguardServiceDelegate copy is cached and can be stale after a PIN is set, so a false there
# never makes Open on its own; it can only add a Secure.
function Resolve-LockState([string]$WindowDump, [string]$LockSettingsDisabled) {
    $direct = @([regex]::Matches($WindowDump, $script:KeyguardSecurePattern) | ForEach-Object { $_.Groups[1].Value.ToLowerInvariant() })
    $cached = @([regex]::Matches($WindowDump, $script:KeyguardDelegatePattern) | ForEach-Object { $_.Groups[1].Value.ToLowerInvariant() })
    if (($direct + $cached) -contains 'true') { return 'Secure' }
    if ($direct -contains 'false') { return 'Open' }
    if ($LockSettingsDisabled.Trim() -match '(?i)^true\b') { return 'Open' }
    return 'Unknown'
}

# `df -k /data`: the last line is "<fs> <1K-blocks> <used> <available> <use%> <mount>"; returns the available KB, or -1 when it cannot be read.
function ConvertFrom-DfAvailableKb([string[]]$Lines) {
    $last = @($Lines | Where-Object { "$_".Trim() -ne '' }) | Select-Object -Last 1
    $fields = "$last".Trim() -split '\s+'
    if ($fields.Count -lt 4 -or $fields[3] -notmatch '^\d+$') { return -1 }
    return [int64]$fields[3]
}

# --- uiautomator dump parsing. Each <node .../> becomes Text, Id, Desc, Clickable, Checked, Selected and the bounds rectangle.
function Read-UiNodes([string]$Xml) {
    foreach ($tag in [regex]::Matches($Xml, '<node\b[^>]*>')) {
        $attrs = @{}
        foreach ($a in [regex]::Matches($tag.Value, '([\w-]+)="([^"]*)"')) { $attrs[$a.Groups[1].Value] = [Net.WebUtility]::HtmlDecode($a.Groups[2].Value) }
        $b = [regex]::Match("$($attrs['bounds'])", '\[(\d+),(\d+)\]\[(\d+),(\d+)\]')
        if (-not $b.Success) { continue }
        [pscustomobject]@{
            Package = "$($attrs['package'])"; Text = "$($attrs['text'])"; Id = "$($attrs['resource-id'])"; Desc = "$($attrs['content-desc'])"; Clickable = ("$($attrs['clickable'])" -eq 'true')
            Checked = ("$($attrs['checked'])" -eq 'true'); Selected = ("$($attrs['selected'])" -eq 'true')
            Left = [int]$b.Groups[1].Value; Top = [int]$b.Groups[2].Value; Right = [int]$b.Groups[3].Value; Bottom = [int]$b.Groups[4].Value
        }
    }
}

# The nodes whose attribute ('Text', 'Id' or 'Desc') equals the value (case-sensitive) first; only when none does, those that contain it (case-insensitive).
# Kind says which of the two it was: 'Exact', 'Contains' or 'None'.
function Resolve-UiMatch($Nodes, [string]$Attribute, [string]$Value) {
    $all = @($Nodes | Where-Object { $_.$Attribute })
    $exact = @($all | Where-Object { $_.$Attribute -ceq $Value })
    if ($exact.Count -gt 0) { return [pscustomobject]@{ Kind = 'Exact'; Nodes = $exact } }
    $partial = @($all | Where-Object { $_.$Attribute.IndexOf($Value, [StringComparison]::OrdinalIgnoreCase) -ge 0 })
    return [pscustomobject]@{ Kind = $(if ($partial.Count -gt 0) { 'Contains' } else { 'None' }); Nodes = $partial }
}

function Find-UiNodes($Nodes, [string]$Attribute, [string]$Value) {
    return @((Resolve-UiMatch $Nodes $Attribute $Value).Nodes)
}

# The status bar and notification shade belong to Android, not to the app, and change by themselves (clock, notifications).
$script:SystemUiPackage = 'com.android.systemui'
function Select-AppNodes($Nodes) {
    return @($Nodes | Where-Object { $_.Package -cne $script:SystemUiPackage })
}

# 'Match' when the app nodes on screen (SystemUI left out) include the expected package, 'Mismatch' when they are all someone else's (the store app, the
# user's apps), 'Unknown' when the dump carries no package at all or nothing but SystemUI.
function Resolve-ForegroundPackage($Nodes, [string]$Package) {
    $packages = @(Select-AppNodes $Nodes | Where-Object { $_.Package } | ForEach-Object { $_.Package } | Select-Object -Unique)
    if ($packages.Count -eq 0) { return 'Unknown' }
    if ($packages -ccontains $Package) { return 'Match' }
    return 'Mismatch'
}

function Get-NodeCentre($Node) {
    return [pscustomobject]@{ X = [int](($Node.Left + $Node.Right) / 2); Y = [int](($Node.Top + $Node.Bottom) / 2) }
}

# `uiautomator dump` prints "UI hierarchy dumped to: <file>" on success; on a screen that never goes idle it prints an error and leaves the old file.
function Test-UiDumpOk([string[]]$Output) {
    return ((@($Output) -join "`n") -match 'dumped to')
}

# One string per node that has anything to show: text, description, id and the toggle state, so a switch or a radio row that flips counts as a change.
function Get-ScreenSignature($Nodes) {
    return @(Select-AppNodes $Nodes | Where-Object { $_.Text -or $_.Desc -or $_.Id } |
        ForEach-Object { '{0}|{1}|{2}|c={3}|s={4}' -f $_.Text, $_.Desc, $_.Id, $_.Checked, $_.Selected } | Select-Object -Unique)
}

# What differs between two dumps: Added and Removed are signature strings; Changed is true when either is not empty.
function Compare-Screens($Before, $After) {
    $b = Get-ScreenSignature $Before; $a = Get-ScreenSignature $After
    $added = @($a | Where-Object { $b -cnotcontains $_ }); $removed = @($b | Where-Object { $a -cnotcontains $_ })
    return [pscustomobject]@{ Changed = ($added.Count + $removed.Count) -gt 0; Added = $added; Removed = $removed }
}

# logcat prints "--------- beginning of main" headers even when nothing was logged; they are not log lines.
function Select-RealLogLines([string[]]$Lines) {
    return @($Lines | Where-Object { "$_".Trim() -ne '' -and "$_" -notmatch '^\s*-{5,}' })
}

# The preferences file that holds $Key: the first of $Names (shared_prefs/*.xml) whose content in $Contents (name -> xml) stores the key, else
# <package>_preferences.xml when it is listed, else $null.
function Select-PrefsFileByKey([string[]]$Names, [hashtable]$Contents, [string]$Key, [string]$Package) {
    $xmlNames = @($Names | ForEach-Object { "$_".Trim() } | Where-Object { $_.EndsWith('.xml') })
    $hit = $xmlNames | Where-Object { $Contents.ContainsKey($_) -and $null -ne (Get-PrefValue $Contents[$_] $Key) } | Select-Object -First 1
    if ($hit) { return $hit }
    return $xmlNames | Where-Object { $_ -eq "${Package}_preferences.xml" } | Select-Object -First 1
}

# MAUI Preferences: <string name="KEY">value</string>; $null when the key is not stored.
function Get-PrefValue([string]$Xml, [string]$Key) {
    $m = [regex]::Match($Xml, '<string name="' + [regex]::Escape($Key) + '">([^<]*)</string>')
    if ($m.Success) { return $m.Groups[1].Value }
    return $null
}

# `run-as <pkg> ls files/packs` and its exit code: 'Packs' (a listing), 'None' (no folder or an empty one) or 'Error' (run-as refused or adb failed: never read as "no packs").
function Resolve-PacksListing([string[]]$Lines, [int]$ExitCode) {
    $text = (@($Lines) -join "`n")
    if ($text -match '(?i)run-as:|not debuggable|unknown package|inaccessible') { return 'Error' }
    if ($text -match '(?i)No such file or directory') { return 'None' }
    # The pack probe (New-PackProbeCommand) ends in `find` on a pack that is not installed, which exits 1 although run-as worked: its ACTIVE lines prove the probe ran.
    if ($text -match ('(?m)^\s*' + $script:PackListingMarker + '\s+\S')) { return 'Packs' }
    if ($ExitCode -ne 0) { return 'Error' }
    if ($text.Trim() -ne '') { return 'Packs' }
    return 'None'
}

# Entries of `tar -tf` that are files (a directory entry ends with "/").
function Get-TarFileCount([string[]]$Lines) {
    return @($Lines | Where-Object { "$_".Trim() -ne '' -and "$_".Trim() -notmatch '/$' }).Count
}

# Slice 179: counting off for every harness run, even a Release build (the marker the app reads, UsageEligibility.NoUsageMarkerFile). `pm clear` wipes the
# external files folder, so every script that clears writes it again.
function Set-DevNoUsageMarker([string]$Phone, [string]$Package) {
    $filesDir = "/sdcard/Android/data/$Package/files"
    $marker = "$filesDir/no-usage"
    Invoke-Adb $Phone @('shell', "mkdir -p $filesDir && touch $marker") | Out-Null
    $listed = "$(Invoke-Adb $Phone @('shell', "ls $marker"))"
    if ($listed -notmatch 'no-usage' -or $listed -match 'No such file|denied') { throw "Could not write the no-usage marker $marker on the phone ($listed): a Release run would send production counts." }
}

# Slice 183: the Debug marker that slows pack downloads. Kept across a clear only when it was there before.
$script:ThrottleMarkerName = 'throttle-pack-downloads'
# Only "No such file" means the marker is absent; any other answer (permission denied, an adb error) is not a yes or a no, so it throws.
function Resolve-MarkerListing([string]$Output, [string]$Name) {
    if ($Output -match 'No such file') { return $false }
    if ($Output.Trim().EndsWith($Name)) { return $true }
    throw "Could not tell whether the marker $Name exists (ls said: $Output)."
}

function Test-DevMarker([string]$Phone, [string]$Package, [string]$Name) {
    return (Resolve-MarkerListing "$(Invoke-Adb $Phone @('shell', "ls /sdcard/Android/data/$Package/files/$Name"))" $Name)
}

function Set-DevMarker([string]$Phone, [string]$Package, [string]$Name) {
    $filesDir = "/sdcard/Android/data/$Package/files"
    Invoke-Adb $Phone @('shell', "mkdir -p $filesDir && touch $filesDir/$Name") | Out-Null
}

# Slice 206: the aside archives are named packs-<yyyyMMdd-HHmmss>.tar. The names to delete so only the newest $Keep stay; anything not named like that is never listed.
$script:KeepAsideArchives = 3
function Get-ArchivesToPrune([string[]]$Names, [int]$Keep = 3) {
    $mine = @($Names | ForEach-Object { "$_".Trim() } | Where-Object { $_ -match '(^|/)packs-\d{8}-\d{6}\.tar$' } | Sort-Object -Descending)
    if ($mine.Count -le $Keep) { return @() }
    return @($mine | Select-Object -Skip $Keep)
}

# ---- voice packs on the phone (Slice 213, redesign)
# The real layout (PackVersionFolders, MobileVoicePackService): files/packs/<id>/active holds the name of the live version folder (e.g. v4) and
# files/packs/<id>/v4/pack.json is its manifest. A download in progress is files/packs/<id>-<version>.zip.partial, a file next to the pack folders (never a pack).
$script:PackManifestFile = 'pack.json'
$script:PacksDirectory = 'files/packs'
$script:PackActiveFile = 'active'
$script:PackListingMarker = 'ACTIVE'

# The phone shell command whose output Resolve-InstalledPackIds reads: for each id an "ACTIVE <id> <contents of its active file>" line, then every pack.json up to two levels down.
function New-PackProbeCommand([string]$Package, [string[]]$PackIds) {
    $ids = $PackIds -join ' '
    $dir = $script:PacksDirectory
    return "run-as $Package sh -c 'for p in $ids; do echo $($script:PackListingMarker) `$p `$(cat $dir/`$p/$($script:PackActiveFile) 2>/dev/null); find $dir/`$p -maxdepth 2 -name $($script:PackManifestFile) 2>/dev/null; done'"
}

# The version folder named by the ACTIVE line of $Id, or $null when the line is missing or the active file is empty.
function Get-ActivePackVersion([string[]]$Lines, [string]$Id) {
    foreach ($line in $Lines) {
        if ("$line" -match ('^' + $script:PackListingMarker + '\s+(\S+)\s+(\S+)$') -and $Matches[1] -ceq $Id) { return $Matches[2] }
    }
    return $null
}

# The ids among $PackIds that are installed, from the output of New-PackProbeCommand: `active` exists AND the version folder it names has pack.json.
# A partial download, an active file with no pack.json in its folder, or a pack.json only in an old version folder is not installed.
function Resolve-InstalledPackIds([string[]]$Listing, [string[]]$PackIds) {
    $lines = @($Listing | ForEach-Object { "$_".Trim() } | Where-Object { $_ })
    $found = @()
    foreach ($id in $PackIds) {
        $version = Get-ActivePackVersion $lines $id
        if ($version -and ($lines -ccontains "$($script:PacksDirectory)/$id/$version/$($script:PackManifestFile)")) { $found += $id }
    }
    return @($found)
}

function Test-PackInstalled([string[]]$Listing, [string[]]$PackIds) { return (@(Resolve-InstalledPackIds $Listing $PackIds).Count -gt 0) }

# Names in `ls files/packs` that are pack folders: a name with a dot is a .zip or .zip.partial file, never a pack.
function Get-PackFolderNames([string[]]$LsLines) {
    return @($LsLines | ForEach-Object { "$_".Trim() } | Where-Object { $_ -match '^[A-Za-z0-9][A-Za-z0-9_-]*$' })
}

# The pack ids a `tar -tf` listing holds completely (its active file and a pack.json): what an aside copy would restore.
function Get-ArchivePackIds([string[]]$TarLines) {
    $names = @($TarLines | ForEach-Object { "$_".Trim() })
    $root = [regex]::Escape($script:PacksDirectory)
    $manifest = [regex]::Escape($script:PackManifestFile)
    $withActive = @()
    foreach ($name in $names) { if ($name -match "^$root/([^/]+)/$($script:PackActiveFile)$") { $withActive += $Matches[1] } }
    $complete = @()
    foreach ($id in ($withActive | Select-Object -Unique)) {
        if (@($names | Where-Object { $_ -match "^$root/$([regex]::Escape($id))/[^/]+/$manifest$" }).Count -gt 0) { $complete += $id }
    }
    return @($complete)
}

# The ids among $PackIds installed on the phone now. Throws when run-as itself fails: that is never "no packs".
function Get-InstalledPackIds([string]$Phone, [string]$Package, [string[]]$PackIds) {
    Assert-DevPackage $Package
    $out = @(Invoke-Adb $Phone @('shell', (New-PackProbeCommand $Package $PackIds)))
    $exit = $global:LASTEXITCODE
    if ((Resolve-PacksListing $out $exit) -eq 'Error') { throw "run-as on $Package failed: cannot tell whether the voice packs are installed ($($out -join ' | '))." }
    return @(Resolve-InstalledPackIds $out $PackIds)
}

# Every installed pack folder on the phone (not only the ones an app needs); @() when there are none.
function Get-InstalledPackFolders([string]$Phone, [string]$Package) {
    Assert-DevPackage $Package
    $out = @(Invoke-Adb $Phone @('shell', "run-as $Package ls $($script:PacksDirectory)"))
    $state = Resolve-PacksListing $out $global:LASTEXITCODE
    if ($state -eq 'Error') { throw "run-as on $Package failed: cannot list the packs ($($out -join ' | '))." }
    $folders = @(Get-PackFolderNames $out)
    if ($state -eq 'None' -or $folders.Count -eq 0) { return @() }
    return @(Get-InstalledPackIds $Phone $Package $folders)
}

# The phone's own clock as a yyyyMMdd-HHmmss stamp (local time on the phone): archive names and their age check both use it, so a PC/phone clock or time-zone difference cannot matter.
function Get-PhoneStamp([string]$Phone) {
    $stamp = "$(Invoke-Adb $Phone @('shell', 'date', '+%Y%m%d-%H%M%S'))".Trim()
    if ($stamp -notmatch '^\d{8}-\d{6}$') { throw "Could not read the phone's clock (date said '$stamp')." }
    return $stamp
}

# Keeps only the INSTALLED pack folders aside as one tar (never a .partial): written to a .part file (tar piped through cat because `> file` on /sdcard wrote 0 bytes on the S21),
# checked (size > 0, `tar -tf` file count equal to the folders', every pack's active file and pack.json in it), renamed; the newest KeepAsideArchives kept.
# Returns Path, Files, PackIds. Throws when nothing is installed or the copy cannot be verified.
function Save-PacksAside([string]$Phone, [string]$Package, [string]$AsideRoot, [int]$TimeoutSeconds) {
    Assert-DevPackage $Package
    $ids = @(Get-InstalledPackFolders $Phone $Package)
    if ($ids.Count -eq 0) { throw 'no installed pack to keep aside (only partial downloads or folders without a manifest)' }
    $paths = ($ids | ForEach-Object { "$($script:PacksDirectory)/$_" }) -join ' '
    $files = @(Invoke-Adb $Phone @('shell', "run-as $Package find $paths -type f"))
    if ($global:LASTEXITCODE -ne 0) { throw "could not list the packs ($($files -join ' | '))" }
    $expected = @($files | Where-Object { "$_".Trim() -ne '' }).Count
    $dir = "$AsideRoot/$Package"
    $archive = "$dir/packs-$(Get-PhoneStamp $Phone).tar"
    $out = Invoke-Adb $Phone @('shell', "mkdir -p $dir && run-as $Package tar -cf - $paths | cat > $archive.part") $TimeoutSeconds
    if ($global:LASTEXITCODE -ne 0) { Invoke-Adb $Phone @('shell', "rm -f $archive.part") | Out-Null; throw "tar of the packs failed ($($out -join ' | '))" }
    $size = "$(Invoke-Adb $Phone @('shell', "stat -c %s $archive.part"))".Trim()
    $listing = @(Invoke-Adb $Phone @('shell', "tar -tf $archive.part"))
    $listed = Get-TarFileCount $listing
    $archived = @(Get-ArchivePackIds $listing)
    $complete = (@($ids | Where-Object { $archived -ccontains $_ }).Count -eq $ids.Count)
    if ($size -notmatch '^\d+$' -or [long]$size -le 0 -or $listed -ne $expected -or -not $complete) {
        Invoke-Adb $Phone @('shell', "rm -f $archive.part") | Out-Null
        throw "the aside copy is not good (size '$size', $listed of $expected files, complete packs: $complete)"
    }
    Invoke-Adb $Phone @('shell', "mv $archive.part $archive") | Out-Null
    foreach ($old in Get-ArchivesToPrune @(Invoke-Adb $Phone @('shell', "ls $dir")) $script:KeepAsideArchives) { Invoke-Adb $Phone @('shell', "rm -f $dir/$old") | Out-Null }
    return [pscustomobject]@{ Path = $archive; Files = $expected; PackIds = $ids }
}

# Copies an aside archive back and proves it: every pack in it must pass the install check afterwards. Returns the ids restored; throws (the archive stays) otherwise.
function Restore-PacksAside([string]$Phone, [string]$Package, [string]$Archive, [int]$TimeoutSeconds) {
    Assert-DevPackage $Package
    $ids = @(Get-ArchivePackIds @(Invoke-Adb $Phone @('shell', "tar -tf $Archive")))
    if ($ids.Count -eq 0) { throw "the aside copy $Archive holds no complete pack (no active file with a pack.json)" }
    $out = Invoke-Adb $Phone @('shell', "cat $Archive | run-as $Package tar -xf -") $TimeoutSeconds
    $exit = $global:LASTEXITCODE
    $installed = if ($exit -eq 0) { @(Get-InstalledPackIds $Phone $Package $ids) } else { @() }
    if ($exit -ne 0 -or $installed.Count -ne $ids.Count) {
        throw "putting the packs back failed (tar exit $exit, $($installed.Count) of $($ids.Count) packs pass the install check; $($out -join ' | ')). The copy is still at $Archive; nothing was deleted."
    }
    return @($installed)
}

# Slice 213: each phone build (one per variant) has its own --artifacts-path, so the Debug and the AOT build of one checkout can run at the same time
# and neither touches src\*\obj or src\*\bin. dotnet puts the project's output under <dir>\bin\<Project>\debug_net10.0-android_<rid>\.
$script:ArtifactsFolderName = '.artifacts'
# The folder name is short on purpose: aapt2 fails past the Windows path limit (APT2264) and the ML Kit asset paths are already ~230 characters long.
function Get-PhoneArtifactsPath([string]$RepoRoot, [string]$App, [string]$Platform) {
    $name = $App + $(if ($Platform -match 'aot$') { '-aot' } else { '-jit' }) + $(if ($Platform -match 'x64') { '-x' } else { '' })
    return Join-Path (Join-Path $RepoRoot $script:ArtifactsFolderName) $name
}

# The newest signed APK anywhere under the artifacts folder's bin folder, or $null.
function Find-PhoneArtifactApk([string]$Artifacts, [string]$Filter) {
    return Get-ChildItem (Join-Path $Artifacts 'bin') -Filter $Filter -Recurse -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

# ---- Slice 216: leftovers from the Slice 213 runs

# Item 7: a lease file that does not parse is held for up to 24 h (Read-Lease fails closed). The parse test is the same one Read-Lease uses, so the two cannot disagree.
function Test-LeaseTextCorrupt([string]$Text) {
    try { $null = $Text | ConvertFrom-Json -ErrorAction Stop; return $false } catch { return $true }
}

function Test-LeaseFileCorrupt([string]$Key) {
    $path = Get-LeasePath $Key
    if (-not (Test-Path $path)) { return $false }
    try { $text = [IO.File]::ReadAllText($path) } catch { return $false }   # an unreadable file is not proven corrupt: never removed
    return (Test-LeaseTextCorrupt $text)
}

# Removes the lease file only when it is corrupt, under the lease lock. 'Cleared', 'NotCorrupt' (a valid lease, expired or live, or none: left alone) or 'Unreadable'.
function Clear-CorruptLease([string]$Key) {
    return (Invoke-WithLeaseLock $Key {
        $path = Get-LeasePath $Key
        if (-not (Test-Path $path)) { return 'NotCorrupt' }
        try { $text = [IO.File]::ReadAllText($path) } catch { return 'Unreadable' }
        if (-not (Test-LeaseTextCorrupt $text)) { return 'NotCorrupt' }
        Remove-Item $path -Force
        return 'Cleared'
    })
}

# Clears every corrupt phone lease file (no phone needed: the keys come from the file names). Returns @{ Key; Result } per lease file.
function Clear-AllCorruptLeases {
    $dir = Get-LeaseDir 'phone'
    if (-not (Test-Path $dir)) { return @() }
    return @(Get-ChildItem $dir -Filter 'lease-*.json' -File | ForEach-Object {
        $key = $_.BaseName.Substring('lease-'.Length)
        [pscustomobject]@{ Key = $key; Result = (Clear-CorruptLease $key) }
    })
}

# Slice 221 item 6: one timed run per phone owner at a time. The phone lease cannot say "a run of mine is alive" (lease.ps1 exits at once, so its own pid is useless, and the same owner may
# renew its lease), so run-checks keeps a run marker next to the lease: <worktrees>\.phone\run-<key>.json with the owner, the run's pid and its start time. A second run with the same
# owner and a live other pid is refused (exit 4) unless -Force; a dead pid, a reused pid or no file is free. Claim and check are one step under the lease lock.
# One file per phone and owner, so two owners never overwrite each other's marker.
function Get-RunMarkerKey([string]$PhoneKey, [string]$Owner) { return "$PhoneKey-$Owner" }

function Get-RunMarkerPath([string]$Key, [string]$Dir = '') {
    if (-not $Dir) { $Dir = Get-LeaseDir 'phone' }
    return Join-Path $Dir ('run-{0}.json' -f ($Key -replace '[^\w.-]', '_'))
}

function Read-RunMarker([string]$Key, [string]$Dir = '') {
    $path = Get-RunMarkerPath $Key $Dir
    if (-not (Test-Path $path)) { return $null }
    try { $marker = [IO.File]::ReadAllText($path) | ConvertFrom-Json -ErrorAction Stop } catch { return $null }
    if (-not $marker.owner -or -not $marker.pid) { return $null }
    return $marker
}

# True when $Marker is a live run of the same owner that is not this process.
function Test-RunMarkerBlocks($Marker, [string]$Owner, [int]$MyPid) {
    if (-not $Marker -or $Marker.owner -cne $Owner -or [int]$Marker.pid -eq $MyPid) { return $false }
    return [bool](Test-ActivityProcessAlive $Marker)
}

# $null once this process owns the marker, else the refusal text. -Force takes the marker over from a live run.
function Request-RunMarker([string]$PhoneKey, [string]$Owner, [bool]$Force = $false, [string]$Dir = '') {
    $markerKey = Get-RunMarkerKey $PhoneKey $Owner
    return (Invoke-WithLeaseLock ('run-' + $PhoneKey) {
        $marker = Read-RunMarker $markerKey $Dir
        if ((Test-RunMarkerBlocks $marker $Owner $PID) -and -not $Force) {
            return "Another run with the owner '$Owner' is still running on phone $PhoneKey (pid $($marker.pid)). Not starting a second one; stop it, or pass -Force to start anyway."
        }
        $null = New-Item -ItemType Directory -Force (Split-Path (Get-RunMarkerPath $markerKey $Dir))
        $record = @{ owner = $Owner; pid = $PID; procStart = (Get-Process -Id $PID).StartTime.ToFileTimeUtc() }
        $record | ConvertTo-Json | Set-Content (Get-RunMarkerPath $markerKey $Dir)
        return $null
    } 'phone')
}

# Removes the marker only when this process wrote it (a -Force run that took it over keeps it for itself).
function Remove-RunMarker([string]$PhoneKey, [string]$Owner, [string]$Dir = '') {
    $markerKey = Get-RunMarkerKey $PhoneKey $Owner
    Invoke-WithLeaseLock ('run-' + $PhoneKey) {
        $marker = Read-RunMarker $markerKey $Dir
        if ($marker -and [int]$marker.pid -eq $PID) { Remove-Item (Get-RunMarkerPath $markerKey $Dir) -Force -ErrorAction SilentlyContinue }
    } 'phone'
}

# Item 6: one line that says how long a lease is written for and why (the budget plus its margin, or the caller's own longer lease, or the -Prepare allowance).
function Format-LeaseLengthNote([string]$Mode, [int]$Minutes, [int]$WantedMinutes, [int]$OwnRemainingMinutes) {
    $why = if ($OwnRemainingMinutes -gt $WantedMinutes) { "your own lease still has $OwnRemainingMinutes min, never shortened" } else { "$Mode needs $WantedMinutes min: its budget plus the margin" }
    return "   phone lease: $Minutes min ($why)"
}

# Item 3: the build fails early when its intermediate folders would pass the Windows path limit (APT2264 came minutes into a build). The deepest MAUI intermediate
# below the artifacts folder is an ESTIMATE (the ML Kit asset paths are ~230 characters from the project folder): tune it when a build fails with APT2264 although this passed.
$script:WindowsPathLimit = 260
$script:DeepestMauiIntermediateChars = 150   # unmeasured estimate, set just below the 160 that FAILS so the warning is rare and means something: over it is only a WARNING
$script:KnownMinimumIntermediateChars = 100  # a floor any MAUI intermediate path certainly has below the artifacts folder: over it the build cannot work (FAIL)
# $null when fine, else @{ Level = 'Warn' | 'Fail'; Message }.
function Test-BuildPathLength([string]$ArtifactsPath) {
    $floor = $ArtifactsPath.Length + $script:KnownMinimumIntermediateChars
    if ($floor -gt $script:WindowsPathLimit) {
        return [pscustomobject]@{ Level = 'Fail'; Message = "path too long: even the shortest intermediate path would be about $floor characters (limit $($script:WindowsPathLimit)). Move the worktree or shorten '$ArtifactsPath' by $($floor - $script:WindowsPathLimit) characters." }
    }
    $estimate = $ArtifactsPath.Length + $script:DeepestMauiIntermediateChars
    if ($estimate -le $script:WindowsPathLimit) { return $null }
    return [pscustomobject]@{ Level = 'Warn'; Message = "path may be too long: the deepest intermediate path is estimated at $estimate characters (limit $($script:WindowsPathLimit); the estimate is unmeasured). If the build fails with APT2264, move the worktree or shorten '$ArtifactsPath'." }
}

function Assert-BuildPathLength([string]$ArtifactsPath) {
    $problem = Test-BuildPathLength $ArtifactsPath
    if (-not $problem) { return }
    if ($problem.Level -eq 'Fail') { throw $problem.Message }
    Write-Warning $problem.Message
}

# Item 5: the one phone build command (audio-timing.ps1 and run-checks.ps1 both call it, so a change cannot miss one). Unquoted arguments for `dotnet build`;
# a caller that goes through Start-Process quotes the ones with spaces (ConvertTo-ProcessArguments).
function Get-PhoneBuildArguments([string]$Project, [string]$Artifacts, [string]$Package, [string]$Rid, [bool]$Aot) {
    # Slice 225 item 23: --disable-build-servers (= -nodeReuse:false, UseSharedCompilation=false, UseRazorBuildServer=false) so no VBCSCompiler or MSBuild node outlives the build and holds the worktree.
    $arguments = @($Project, '-f', 'net10.0-android', '-c', 'Debug', '--artifacts-path', $Artifacts, '--disable-build-servers', '-nodeReuse:false', '-p:UseSharedCompilation=false',
        "-p:RuntimeIdentifier=$Rid", '-p:EmbedAssembliesIntoApk=true', '-p:IncludeWindows=false', '-p:AndroidPackageFormat=apk', "-p:ApplicationId=$Package")
    if ($Aot) { $arguments += '-p:RunAOTCompilation=true', '-p:AndroidEnableProfiledAot=false' }
    return $arguments
}

function ConvertTo-ProcessArguments([string[]]$Arguments) {
    return @($Arguments | ForEach-Object { if ("$_" -match '\s') { "`"$_`"" } else { "$_" } })
}

# Item 2: an empty sentence audio cache is what makes a read cold (SentenceAudioCache writes <app data>/cache/sentences). Dev package only; the app must be stopped.
# The one copy: audio-timing.ps1 and checks/cold-start.ps1 both use it.
$script:SentenceCachePath = 'cache/sentences'   # FileSystem.CacheDirectory/sentences (AndroidSupertonicSpeechReader.SentenceFolderName), under the app's data dir
function New-SentenceCacheClearCommand([string]$Package) {
    Assert-DevPackage $Package
    return "run-as $Package sh -c 'rm -rf $($script:SentenceCachePath); mkdir -p $($script:SentenceCachePath); ls $($script:SentenceCachePath) | wc -l'"
}

# Slice 214: evict the phone's file cache so the model files are read from flash (the fresh-install case). Needs root (`su` or `adb root`); a stock phone has none,
# and then this returns $false and the run is only "force-stop cold". Without root the options are a reinstall (run-checks -Reinstall; packs stay, the new APK files are cold)
# or a reboot (adb reboot, reconnect over wireless debugging, unlock once), which this script never does by itself.
$script:DropCacheDoneMarker = 'RTST_DROP_RC='
$script:DropCachesCommand = 'sync; echo 3 > /proc/sys/vm/drop_caches; echo RTST_DROP_RC=$?'
# Slice 225 item 22: true when the phone's output holds the marker with exit code 0. Pure, so the self-test proves the pattern (a raw 0x08 byte once replaced a word boundary here).
function Test-DropCacheDone([string]$Output) {
    return ("$Output" -match "$([regex]::Escape($script:DropCacheDoneMarker))0(?![0-9])")
}

function Clear-PageCache([string]$Phone) {
    foreach ($wrap in @("su -c '$($script:DropCachesCommand)'", "su 0 sh -c '$($script:DropCachesCommand)'", $script:DropCachesCommand)) {
        try {
            $out = "$(Invoke-Adb $Phone @('shell', $wrap) 2>&1)"
            # The phone's own shell prints the exit code of the write (RTST_DROP_RC=<n>): that is the answer, whatever language the error text is in and however adb passes the exit code on.
            if (Test-DropCacheDone $out) { return $true }
        } catch {
            if (Test-PastDeadline (Get-ActiveDeadline) (Get-Date)) { throw }   # the run's budget ended while waiting (a root prompt can hang su): signal it, never swallow it
            Write-Verbose "drop_caches variant failed: $($_.Exception.Message)"
        }
    }
    return $false
}

function Clear-SentenceCache([string]$Phone, [string]$Package) {
    $out = @(Invoke-Adb $Phone @('shell', (New-SentenceCacheClearCommand $Package)))
    if ("$($out | Select-Object -Last 1)".Trim() -ne '0') { throw "Could not empty the sentence cache of $Package (run-as said: $($out -join ' | ')): the run would not be cold." }
}

# Item 8 (Slice 216): what tap.ps1 prints about a screen change: only the changed texts (a signature is text|desc|id|c=|s=; the first non-empty of the three), cut to $Max characters.
# The full signatures go to tap-changes.log, not the run output.
$script:ScreenChangeMaxChars = 120
function Get-SignatureLabel([string]$Signature) {
    $core = $Signature -replace '\|c=[^|]*\|s=[^|]*$', ''
    $label = @($core -split '\|', 3 | Where-Object { $_ })[0]
    if (-not $label) { $label = $core }
    # A toggle shows as the same label added and removed: say its state, so the direction is readable.
    $state = if ($Signature -match '\|c=True\|') { ' [checked]' } elseif ($Signature -match '\|s=True$') { ' [selected]' } else { '' }
    return "$label$state"
}

function Format-ScreenChangeLine([string]$Heading, [string[]]$Signatures, [int]$Max = 0) {
    if ($Max -le 0) { $Max = $script:ScreenChangeMaxChars }
    $text = "${Heading}: " + ((@($Signatures) | ForEach-Object { Get-SignatureLabel $_ }) -join ' ; ')
    if ($text.Length -le $Max) { return $text }
    return $text.Substring(0, $Max - 3) + '...'
}

# Slice 216: -Rates is a string list ("1,1", "1,2" or 1,2): a [double[]] parameter turned "1,1" into 11. Split on commas, parse with the invariant culture, keep to the PlaybackRate range (0.5-2.0).
$script:MinPlaybackRate = 0.5
$script:MaxPlaybackRate = 2.0
function ConvertTo-RateList($Rates) {
    $parts = @(@($Rates) | ForEach-Object { "$_" -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($parts.Count -eq 0) { throw '-Rates is empty; pass rates like 1,2 (0.5 to 2.0).' }
    foreach ($part in $parts) {
        $rate = 0.0
        $parsed = [double]::TryParse($part, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$rate)
        if (-not $parsed -or $rate -lt $script:MinPlaybackRate -or $rate -gt $script:MaxPlaybackRate) {
            throw "-Rates value '$part' is not a rate between $($script:MinPlaybackRate) and $($script:MaxPlaybackRate)."
        }
        $rate
    }
}

# Slice 208 (S21 offline 2026-10-31 ~13:57): a check once ran `svc wifi disable` while the only adb link was wireless, so its finally block could never put wifi back.
# Rule: a check never cuts the link it is driven over, so no check touches wifi, mobile data or airplane mode; it cuts one app's network instead (Set-AppNetworkBlocked).
# Slice 225 item 14: no check made a whole-device cut, so the guard function that nothing called is gone; Test-CheckRestoresFirst (checks/_lib.ps1) is the enforced rule instead.

# The seconds a device-side restore waits before it heals a cut by itself (no adb needed).
$script:DeviceRestoreSeconds = 240
# Slice 225 item 15: the detached restore writes its shell's pid here, so the run that started it can cancel it (a heal left to fire could undo a slower run's or a rerun's settings).
$script:DeviceRestorePidDir = '/data/local/tmp'

function Get-DeviceRestorePidFile([string]$Package) { return "$($script:DeviceRestorePidDir)/rtst-heal-$Package.pid" }

# The shell text that undoes an app network cut and a lowered dataSync timeout, run ON the phone by a detached `sh`. Pure: unit-tested.
# It does NOT move a pack back: a moved-aside pack is put back by the check's own finally block or by reset-dev-app.ps1.
function Get-DeviceRestoreScript([string]$Package, [int]$Uid, [int]$Seconds) {
    $pidFile = Get-DeviceRestorePidFile $Package
    $undo = "cmd netpolicy remove restrict-background-blacklist $Uid; device_config delete activity_manager data_sync_fgs_timeout_duration; am compat reset FGS_INTRODUCE_TIME_LIMITS $Package"
    # Review (check 2, finding 7): once the sleep is over the heal ignores TERM (`trap :`), so a cancel that lands while the undo runs cannot leave the phone half-restored; a cancel before that kills the heal while it sleeps.
    return "echo `$`$ > $pidFile; sleep $Seconds; trap : TERM HUP INT; $undo; rm -f $pidFile"
}

# The shell text that stops a pending detached restore (its pid file names the `sh`). Pure: unit-tested.
# Says: cancelled (killed while asleep; the pid file is still there), finished (the undo was already running, so it was waited for up to 30 s and ran whole), stuck (still alive after 30 s), stale or none.
function Get-DeviceRestoreCancelCommand([string]$Package) {
    $pidFile = Get-DeviceRestorePidFile $Package
    # The pid is killed only when its command line holds the pid file's own path (the heal's `sh -c` text does): a file that outlived its shell (reboot) names an unrelated process by now, so it is removed and nothing is killed ("stale").
    $wait = "i=0; while kill -0 `$p 2>/dev/null && [ `$i -lt 30 ]; do sleep 1; i=`$((i+1)); done"
    $verdict = "if kill -0 `$p 2>/dev/null; then echo stuck; elif [ -f $pidFile ]; then echo cancelled; else echo finished; fi"
    return "if [ -f $pidFile ]; then p=`$(cat $pidFile); if tr '\0' ' ' < /proc/`$p/cmdline 2>/dev/null | grep -qF $pidFile; then kill `$p 2>/dev/null; $wait; $verdict; else echo stale; fi; rm -f $pidFile; else echo none; fi"
}

# Stops a pending detached restore; returns $true when one was cancelled. Never throws (it runs in finally blocks).
function Stop-DeviceRestore([string]$Serial, [string]$Package) {
    try {
        $said = "$(Invoke-Adb $Serial @('shell', (Get-DeviceRestoreCancelCommand $Package)))"
        if ($said -match 'stale') { Write-Host "   WARNING: a stale heal pid file for $Package was removed; its pid belongs to another process, so nothing was killed." }
        if ($said -match 'stuck') { Write-Host "   WARNING: the device-side restore for $Package was still running 30 s after the cancel; look at the phone's dataSync timeout and network block." }
        if ($said -match 'finished') { Write-Host "   the device-side restore for $Package had already started and ran to the end (it was waited for, not killed)." }
        return ($said -match 'cancelled')
    }
    catch { Write-Host "   WARNING: could not cancel the device-side restore: $($_.Exception.Message)"; return $false }
}

# Starts the detached restore BEFORE any cut, so a lost link heals on its own. The check's finally block restores over adb as the fast path and then cancels it (Stop-DeviceRestore).
# A restore left by an earlier run of the same package is cancelled first, so it cannot fire in the middle of this one.
function Start-DeviceRestore([string]$Serial, [string]$Package, [int]$Uid, [int]$Seconds) {
    $null = Stop-DeviceRestore $Serial $Package
    $script = Get-DeviceRestoreScript $Package $Uid $Seconds
    Invoke-Adb $Serial @('shell', "nohup sh -c '$script' >/dev/null 2>&1 &") | Out-Null
}

function Get-PackageUid([string]$Serial, [string]$Package) {
    $line = "$(Invoke-Adb $Serial @('shell', 'pm', 'list', 'packages', '-U', $Package) | Where-Object { $_ -match "package:$([regex]::Escape($Package))\s+uid:(\d+)" } | Select-Object -First 1)"
    if ($line -notmatch 'uid:(\d+)') { throw "Could not read the uid of $Package (pm list packages -U said '$line')." }
    return [int]$Matches[1]
}

# Blocks (or frees) one app's background network through `cmd netpolicy ... restrict-background-blacklist <uid>`; wifi, mobile data and the adb link are untouched.
function Set-AppNetworkBlocked([string]$Serial, [int]$Uid, [bool]$Blocked) {
    $verb = if ($Blocked) { 'add' } else { 'remove' }
    Invoke-Adb $Serial @('shell', 'cmd', 'netpolicy', $verb, 'restrict-background-blacklist', "$Uid") | Out-Null
}

# ---- Slice 225 item 1: the device-wide leftovers a check can leave on the phone, found and cleared by script (restore-device-state.ps1; preflight.ps1 -Fix calls it too).
# Only the dev packages' own uids are ever taken off the background-network blacklist: another entry is the user's own data-saver choice for one of their apps.
$script:DataSyncTimeoutKey = 'data_sync_fgs_timeout_duration'
$script:CompatChange = 'FGS_INTRODUCE_TIME_LIMITS'
$script:BlockLabelPattern = '(?i)(black|block|deny)\s*-?list'   # the label of `cmd netpolicy list restrict-background-blacklist`; the exact text is NOT confirmed on a device, so both spellings are read
$script:BlockLabelExclude = '(?i)(white|allow)\s*-?list|status'  # a "restrict ... whitelist" or "restrict ... status" line is not the block list

# device_config prints "null" for a key that is not set.
function Test-DataSyncTimeoutLeftover([string]$Value) {
    $text = "$Value".Trim()
    return ($text -ne '' -and $text -ne 'null')
}

# Pure: 'Blocked', 'Clean' or 'Unknown'. The text is read line by line; the uid counts as blocked only as a whole token on a line whose label says blacklist/blocklist/denylist
# (or "restrict", except a whitelist or status line). With no such label line at all the text says nothing about the block list: 'Unknown', neither clean nor blocked.
function Get-UidBlockState([string]$Text, [int]$Uid) {
    $blockLines = @(Get-BlockListLines $Text)
    if ($blockLines.Count -eq 0) { return 'Unknown' }
    foreach ($line in $blockLines) { if ($line -match "(?<![\w.])$Uid(?![\w.])") { return 'Blocked' } }
    return 'Clean'
}

# Pure: the lines that belong to the block list. A label line starts a block; the uids may continue on the following lines, so the block runs until the next label (any "word...:" line) or a blank line.
# Returns nothing when there is no block label line at all.
function Get-BlockListLines([string]$Text) {
    $inBlock = $false
    foreach ($line in ("$Text" -split '\r?\n')) {
        $isBlockLabel = ($line -match $script:BlockLabelPattern) -or (($line -match '(?i)restrict') -and ($line -notmatch $script:BlockLabelExclude))
        if ($isBlockLabel) { $inBlock = $true; $line; continue }
        if ($line.Trim() -eq '' -or $line -match '^\s*[A-Za-z][^:]*:') { $inBlock = $false; continue }
        if ($inBlock) { $line }
    }
}

# Pure: the clear commands one dev package needs, each { What; Arguments } (adb shell arguments). The compat override cannot be read back reliably, so its reset is always planned (it is idempotent).
# An 'Unknown' block state still plans the removal (idempotent, this dev uid only) and says the state could not be read.
function Get-DeviceRestorePlan([string]$Package, [int]$Uid, [string]$DataSyncValue, [string]$BlacklistText) {
    $plan = @()
    if (Test-DataSyncTimeoutLeftover $DataSyncValue) { $plan += [pscustomobject]@{ What = "dataSync timeout $("$DataSyncValue".Trim()) ms"; Arguments = @('shell', 'device_config', 'delete', 'activity_manager', $script:DataSyncTimeoutKey) } }
    $plan += [pscustomobject]@{ What = "compat flag $($script:CompatChange) for $Package (always reset)"; Arguments = @('shell', 'am', 'compat', 'reset', $script:CompatChange, $Package) }
    $state = Get-UidBlockState $BlacklistText $Uid
    $remove = @('shell', 'cmd', 'netpolicy', 'remove', 'restrict-background-blacklist', "$Uid")
    if ($state -eq 'Blocked') { $plan += [pscustomobject]@{ What = "background-network block on uid $Uid ($Package)"; Arguments = $remove } }
    if ($state -eq 'Unknown') { $plan += [pscustomobject]@{ What = "background-network block on uid $Uid ($Package): the block list could not be read (no blacklist line in 'cmd netpolicy list')"; Arguments = $remove } }
    return $plan
}

# Review 1: another session's live lease means its run may be using the very timeout or block that looks like a leftover, so clearing is refused (throws) unless -Force.
function Assert-NoOtherPhoneLease([string]$Serial) {
    $key = Get-PhoneLeaseKey $Serial
    $held = Read-Lease $key
    $me = [Environment]::GetEnvironmentVariable($script:LeaseOwnerVariable)
    if (-not $held -or $held.Owner -ceq $me) { return }
    throw "Phone $key is leased by $(Format-LeaseHolder $held); this session's owner is '$me'. Not clearing device leftovers while another session's lease is live (its run may be using them). Wait for the lease, or pass -Force."
}

# Reads, clears and reads again. Returns Leftovers (what was found), Remaining (what a second read still shows) and Unknown (a block list the read-back could not read). A dev package that is not installed is skipped.
# Refuses (throws) while another owner's lease is live, unless -Force.
function Clear-DeviceLeftovers([string]$Serial, [string[]]$Packages, [switch]$Force) {
    if (-not $Force) { Assert-NoOtherPhoneLease $Serial }
    $found = @(); $remaining = @(); $unknown = @()
    foreach ($package in $Packages) {
        Assert-DevPackage $package
        try { $uid = Get-PackageUid $Serial $package } catch { continue }
        $value = "$(Invoke-Adb $Serial @('shell', 'device_config', 'get', 'activity_manager', $script:DataSyncTimeoutKey))"
        $black = (Invoke-Adb $Serial @('shell', 'cmd', 'netpolicy', 'list', 'restrict-background-blacklist')) -join "`n"
        if ((Get-UidBlockState $black $uid) -eq 'Unknown') { Write-Host "   WARNING: 'cmd netpolicy list' has no blacklist line, so the block state of uid $uid is unknown. It said: $("$black".Trim())" }
        foreach ($step in Get-DeviceRestorePlan $package $uid $value $black) {
            if ($step.What -notlike 'compat flag*' -and $step.What -notlike '*could not be read*') { $found += $step.What }
            Invoke-Adb $Serial $step.Arguments | Out-Null
        }
        $null = Stop-DeviceRestore $Serial $package
        $after = "$(Invoke-Adb $Serial @('shell', 'device_config', 'get', 'activity_manager', $script:DataSyncTimeoutKey))"
        $afterBlack = (Invoke-Adb $Serial @('shell', 'cmd', 'netpolicy', 'list', 'restrict-background-blacklist')) -join "`n"
        foreach ($step in Get-DeviceRestorePlan $package $uid $after $afterBlack) {
            if ($step.What -like 'compat flag*') { continue }
            # A block list that could not be read is Unknown, not "still set": it is reported apart so a clean phone never reads as a failed restore.
            if ($step.What -like '*could not be read*') { $unknown += $step.What } else { $remaining += $step.What }
        }
    }
    return [pscustomobject]@{ Leftovers = @($found | Select-Object -Unique); Remaining = @($remaining | Select-Object -Unique); Unknown = @($unknown | Select-Object -Unique) }
}

# ---- Slice 225 item 11: the screen timeout the pre-flight found is put back when the phone lease is released.
# Review 2/3 (rounds 3): the value is recorded only at the moment an owner TAKES the lease (lease.ps1 -Take, a timed run). The record is { phone, owner, pid, orig, set, savedUtc }:
# orig is the phone's stay_on value at the take, set is the value this tooling then sets (7). ONE rule restores (Invoke-StayOnRestore, used by every release path, by a new take and by preflight -Fix):
# orig is written back ONLY IF the phone's current value still equals set, so a value the user changed since is never undone; then the record is deleted.
# A record is never handed to another owner. One whose pid is alive and is not ours, or whose owner still holds a live lease, is left alone; a dead one older than 24 h is deleted without restoring.
# The restore never presses the power key: the phone's own timeout acts, so a phone in use is not blanked.
$script:StayOnSetting = 'stay_on_while_plugged_in'
$script:StayOnSetValue = '7'   # bit mask 1 AC + 2 USB + 4 wireless: what preflight.ps1 -Fix sets (it has its own constant for the same number)

function Get-StayOnRecordPath([string]$PhoneKey, [string]$Dir = '') {
    if (-not $Dir) { $Dir = Get-LeaseDir 'phone' }
    return Join-Path $Dir ('stayon-{0}.json' -f ($PhoneKey -replace '[^\w.-]', '_'))
}

# A record older than this is never written back: the setting may have been changed on purpose since.
$script:StayOnRecordMaxAge = [TimeSpan]::FromHours(24)

# The record as written, or $null when there is none, it is unreadable or it holds no usable numbers. No age check here: Get-StayOnRecordAction decides.
function Read-StayOnOriginal([string]$PhoneKey, [string]$Dir = '') {
    $path = Get-StayOnRecordPath $PhoneKey $Dir
    if (-not (Test-Path $path)) { return $null }
    try { $record = [IO.File]::ReadAllText($path) | ConvertFrom-Json -ErrorAction Stop } catch { return $null }
    if ("$($record.orig)" -notmatch '^\d+$' -or "$($record.set)" -notmatch '^\d+$') { return $null }
    if ("$($record.phone)" -cne $PhoneKey) { return $null }
    return $record
}

# Pure: the age of a record, or $null when its write time is missing or unreadable.
function Get-StayOnRecordAge($Record, [DateTime]$NowUtc) {
    $saved = [DateTime]::MinValue
    if ($Record.savedUtc -is [DateTime]) { $saved = $Record.savedUtc }   # PowerShell 7's ConvertFrom-Json turns the ISO text into a date; 5.1 leaves the text
    elseif (-not [DateTime]::TryParse("$($Record.savedUtc)", [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind, [ref]$saved)) { return $null }
    return ($NowUtc.ToUniversalTime() - $saved.ToUniversalTime())
}

# Pure: what to do with a record. 'Restore' (the restore rule, then delete), 'Delete' (stale and dead: delete, write nothing) or 'LeaveAlone' (a live process or a live lease of another owner may still need it).
# $Owner is the caller's lease owner ('' for a caller that owns nothing, such as preflight.ps1 -Fix); $PidAlive says whether the record's pid is a running process; $OwnerLeaseLive whether the record's owner still holds the phone lease.
function Get-StayOnRecordAction($Record, [string]$Owner, [int]$MyPid, [bool]$PidAlive, [bool]$OwnerLeaseLive, [DateTime]$NowUtc) {
    $mine = ("$($Record.owner)" -ceq $Owner -and $Owner -ne '')
    $age = Get-StayOnRecordAge $Record $NowUtc
    $stale = ($null -eq $age -or $age -lt [TimeSpan]::Zero -or $age -gt $script:StayOnRecordMaxAge)
    if (-not $mine) {
        $running = ($PidAlive -and [int]$Record.pid -ne $MyPid)
        if (($running -and -not $stale) -or $OwnerLeaseLive) { return 'LeaveAlone' }
    }
    if ($stale) { return 'Delete' }
    return 'Restore'
}

function Test-ProcessAlive([int]$ProcessId) {
    if ($ProcessId -le 0) { return $false }
    return [bool](Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)
}

# The one restore rule: orig is written back only if the phone's current value still equals set; the record is then deleted either way. Returns { Restored; Value; Action } or $null when there was no record.
# Never hands a record to another owner: a record of someone else is only healed when it is dead (see Get-StayOnRecordAction).
function Invoke-StayOnRestore([string]$Serial, [string]$PhoneKey, [string]$Owner, [string]$Dir = '', [DateTime]$NowUtc = [DateTime]::UtcNow) {
    $record = Read-StayOnOriginal $PhoneKey $Dir
    if (-not $record) { return $null }
    $held = Read-Lease $PhoneKey
    $leaseLive = [bool]($held -and "$($held.Owner)" -ceq "$($record.owner)")
    $action = Get-StayOnRecordAction $record $Owner $PID (Test-ProcessAlive ([int]$record.pid)) $leaseLive $NowUtc
    $result = [pscustomobject]@{ Restored = $false; Value = "$($record.orig)"; Action = $action }
    if ($action -eq 'LeaveAlone') { return $result }
    $path = Get-StayOnRecordPath $PhoneKey $Dir
    if ($action -eq 'Restore') {
        $current = "$(Invoke-Adb $Serial @('shell', 'settings', 'get', 'global', $script:StayOnSetting))".Trim()
        if ($current -ceq "$($record.set)") {
            Invoke-Adb $Serial @('shell', 'settings', 'put', 'global', $script:StayOnSetting, "$($record.orig)") | Out-Null
            $result.Restored = $true
        }
    }
    Remove-Item $path -Force -ErrorAction SilentlyContinue
    return $result
}

# $true when this call wrote a new record. Never replaces or hands over an existing one: the caller heals a dead record first (Invoke-StayOnRestore), so what is left is a live one and is kept.
# A value that is not a number ("null": never set) is recorded as 0.
function Save-StayOnOriginal([string]$PhoneKey, [string]$Value, [string]$Dir = '', [DateTime]$NowUtc = [DateTime]::UtcNow, [string]$Owner = '', [int]$ProcessId = $PID) {
    $path = Get-StayOnRecordPath $PhoneKey $Dir
    if (Read-StayOnOriginal $PhoneKey $Dir) { return $false }
    $number = if ("$Value".Trim() -match '^\d+$') { "$Value".Trim() } else { '0' }
    $null = New-Item -ItemType Directory -Force (Split-Path $path)
    @{ orig = $number; set = $script:StayOnSetValue; phone = $PhoneKey; owner = $Owner; pid = $ProcessId; savedUtc = $NowUtc.ToString('o') } | ConvertTo-Json | Set-Content $path
    return $true
}

# Called right after an owner took (not renewed) the phone lease: first heals a dead run's record (so its 7 is never read as the original), then records the setting as it is now for that owner.
function Save-StayOnForLease([string]$AdbSerial, [string]$PhoneKey, [string]$Owner) {
    $null = Invoke-StayOnRestore $AdbSerial $PhoneKey $Owner
    $value = "$(Invoke-Adb $AdbSerial @('shell', 'settings', 'get', 'global', $script:StayOnSetting))".Trim()
    return (Save-StayOnOriginal $PhoneKey $value '' ([DateTime]::UtcNow) $Owner)
}

# The one way a lease holder lets the phone go (lease.ps1 -Release and every exit of a timed run): it puts the stay-on setting back, then releases.
# Only the owner of a held lease does either; the restore never blocks the release (a dropped phone is said, not thrown). Returns $true when the lease was released.
function Close-PhoneLease([string]$AdbSerial, [string]$PhoneKey, [string]$Owner) {
    $held = Read-Lease $PhoneKey
    if (-not ($held -and $held.Owner -ceq $Owner)) { return (Remove-Lease $PhoneKey $Owner) }
    try {
        $restored = Invoke-StayOnRestore $AdbSerial $PhoneKey $Owner
        if ($restored -and $restored.Restored) { Write-Host "stay_on_while_plugged_in put back to $($restored.Value) (the screen is left to the phone's own timeout)." }
        elseif ($restored) { Write-Host "stay_on_while_plugged_in left as it is (it no longer holds the value this tooling set, or the record is not this lease's)." }
    } catch { Write-Host "WARNING: could not put the screen timeout back: $($_.Exception.Message)" }
    return (Remove-Lease $PhoneKey $Owner)
}
