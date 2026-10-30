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
    $archive = "$dir/packs-$(Get-Date -Format 'yyyyMMdd-HHmmss').tar"
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
