<#
.SYNOPSIS
  The reset ladder for a broken dev install on the real phone (Slice 206).
.DESCRIPTION
  Touches only the Debug dev packages uk.sirous.readthestupidtext.dbg and uk.sirous.dinoreader.dbg (a hard-coded allow-list, Assert-DevPackage); never the store app.
  -Level setting   force-stop, put the app language back to English in the app's shared preferences (RTST key "Language", MobilePreferencesSettingsStore;
                   Dino key "dino.ui.language", LanguagePage) through run-as, relaunch, confirm the app logs within -AliveSeconds (by pid, only lines after the
                   launch time; the shared log buffer is not cleared) and stays up.
  -Level clear     copy the INSTALLED packs (files/packs/<id>/active plus the pack.json of the version folder it names; never a *.zip.partial) to /sdcard/Download/rtst-packs-aside/<pkg>/packs-<timestamp>.tar and verify that archive (tar exit code, size > 0,
                   `tar -tf` file count equal to those folders', every pack's active file and pack.json in it) BEFORE pm clear; any run-as or tar failure stops the level with the packs untouched. Then pm clear,
                   write the no-usage marker (and the throttle marker if it was there) again, copy the packs back and say "packs restored" only after each pack passes the install check (Test-PackInstalled), relaunch with the
                   skip-first-run hook, confirm alive. The archive is kept (its path is printed); only the newest 3 archives per package are kept, older ones are deleted after each new one verifies. If putting the packs back fails the ladder stops (STOPPED line) and the copy is left for a hand restore.
  -Level reinstall as clear, but installs (adb install -r, never an uninstall) the APK from the shared build cache (audio-timing.ps1 builds and caches it) between the two.
  -Level auto      setting, then clear, then reinstall; stops at the first level where the app comes up alive and prints which one worked.
  Exit 0 alive, 1 not alive after the last level run, 4 the phone is leased by someone else.
  Android sources: `run-as <package>` runs a command as the app for a debuggable build; `pm clear`; `cmd package resolve-activity --brief`.
#>
param(
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [ValidateSet('setting', 'clear', 'reinstall', 'auto')][string]$Level = 'setting',
    [string]$Serial,
    [int]$AliveSeconds = 60
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot '..\emulator\build-cache.ps1')

$package = Get-DevPackage $App
$script:LanguageKeys = @{ rtst = 'Language'; dino = 'dino.ui.language' }  # MobilePreferencesSettingsStore.LanguageKey; Dino: LanguagePage.UiLanguageKey
$script:EnglishCode = 'en'
$script:AsideRoot = '/sdcard/Download/rtst-packs-aside'
$script:PacksDir = 'files/packs'                                    # relative to the app's data dir (MobileVoicePackService.PacksFolder)
$script:TransferSeconds = 900
$script:LogSince = ''                                               # the phone's clock at launch; logcat -T reads only lines after it (no logcat -c on the shared buffer)
$script:StaySeconds = 5
$script:PacksNotRestored = $null                                    # the aside archive of a clear whose restore failed: no later level may run on top of it

# Which file MAUI Preferences.Default writes depends on the MAUI version: older Essentials used <package>.microsoft.maui.essentials.preferences.xml, but
# on the S21 (Android 15) the Debug app's file was shared_prefs/uk.sirous.readthestupidtext.dbg_preferences.xml (<package>_preferences.xml, the default
# shared preferences). The MAUI Essentials source was not available offline to confirm the version, so the file is found by the language key it holds.
function Get-PrefsFile([string]$Phone) {
    $names = @(Invoke-Adb $Phone @('shell', "run-as $package ls shared_prefs")) | ForEach-Object { "$_".Trim() } | Where-Object { $_.EndsWith('.xml') }
    $contents = @{}
    foreach ($name in $names) { $contents[$name] = (Invoke-Adb $Phone @('shell', "run-as $package cat shared_prefs/$name")) -join "`n" }
    return Select-PrefsFileByKey $names $contents $script:LanguageKeys[$App] $package
}

function Set-EnglishLanguage([string]$Phone) {
    $key = $script:LanguageKeys[$App]
    $file = Get-PrefsFile $Phone
    if (-not $file) { Write-Host '   no preferences file yet: nothing to reset (first run)'; return }
    $path = "shared_prefs/$file"
    $xml = (Invoke-Adb $Phone @('shell', "run-as $package cat $path")) -join "`n"
    $before = Get-PrefValue $xml $key
    if ($null -eq $before) { Write-Host "   no '$key' key stored: already the default"; return }
    $sed = 's|name="{0}">[^<]*<|name="{0}">{1}<|' -f ($key -replace '\.', '\.'), $script:EnglishCode
    Invoke-Adb $Phone @('shell', "run-as $package sed -i '$sed' $path") | Out-Null
    $after = Get-PrefValue ((Invoke-Adb $Phone @('shell', "run-as $package cat $path")) -join "`n") $key
    Write-Host "   language setting '$key': '$before' -> '$after'"
    if ($after -ne $script:EnglishCode) { throw "Could not set the language back to English in $path (now '$after')." }
}

function Get-LauncherActivity([string]$Phone) {
    $out = @(Invoke-Adb $Phone @('shell', 'cmd', 'package', 'resolve-activity', '--brief', '-c', 'android.intent.category.LAUNCHER', $package))
    $activity = ("$($out[-1])").Trim()
    if ($activity -notmatch '^[\w.]+/\S+$') { throw "$package is not installed on the phone (no launcher activity: '$activity')." }
    return $activity
}

function Get-AppPid([string]$Phone) {
    $out = "$(Invoke-Adb $Phone @('shell', 'pidof', $package))".Trim()
    return ($out -split '\s+' | Where-Object { $_ -match '^\d+$' } | Select-Object -First 1)
}

function Start-DevApp([string]$Phone, [bool]$SkipFirstRun) {
    Invoke-Adb $Phone @('shell', 'am', 'force-stop', $package) | Out-Null
    $script:LogSince = "$(Invoke-Adb $Phone @('shell', "date '+%m-%d %H:%M:%S.000'"))".Trim()
    $launch = @('shell', 'am', 'start', '-n', (Get-LauncherActivity $Phone))
    if ($SkipFirstRun) { $launch += @('--es', 'cmd', 'skip-first-run', '--es', 'lang', $script:EnglishCode) }
    Invoke-Adb $Phone $launch | Out-Null
}

# Alive = a process of the app exists, it logged at least one line since the launch (read by pid, not by tag; logcat header lines do not count) and the same pid is still there a few seconds later.
function Wait-AppAlive([string]$Phone) {
    $deadline = (Get-Date).AddSeconds($AliveSeconds)
    do {
        $appPid = Get-AppPid $Phone
        if ($appPid -and (Select-RealLogLines (Invoke-Adb $Phone @('shell', "logcat -d -T '$($script:LogSince)' --pid=$appPid"))).Count -gt 0) {
            Start-Sleep -Seconds $script:StaySeconds
            if ((Get-AppPid $Phone) -eq $appPid) { Write-Host "   alive: pid $appPid logging and still up after $($script:StaySeconds) s"; return $true }
            Write-Host "   pid $appPid died within $($script:StaySeconds) s of its first log line"; return $false
        }
        Start-Sleep -Milliseconds 700
    } while ((Get-Date) -lt $deadline)
    $crash = (Invoke-Adb $Phone @('logcat', '-d', '-b', 'crash', '-t', '5')) -join ' | '
    Write-Host "   not alive: no logging process within $AliveSeconds s. Crash buffer: $crash"
    return $false
}

# 'Packs', 'None' or 'Error' (run-as refused or adb failed: never read as "no packs").
function Get-PacksState([string]$Phone) {
    $listing = @(Invoke-Adb $Phone @('shell', "run-as $package ls $($script:PacksDir)"))
    return (Resolve-PacksListing $listing $global:LASTEXITCODE)
}

# Kilobytes the packs take (du -sk), to compare with the free space on /sdcard.
function Get-PacksSizeKb([string]$Phone) {
    $out = "$(Invoke-Adb $Phone @('shell', "run-as $package du -sk $($script:PacksDir)"))".Trim()
    if ($global:LASTEXITCODE -ne 0 -or $out -notmatch '^(\d+)') { throw "Could not read the size of the packs ($out): not clearing the app." }
    return [int64]$Matches[1]
}

function Assert-AsideSpace([string]$Phone) {
    $needKb = Get-PacksSizeKb $Phone
    $freeKb = ConvertFrom-DfAvailableKb (Invoke-Adb $Phone @('shell', 'df', '-k', '/sdcard'))
    if ($freeKb -lt 0 -or $freeKb -lt ($needKb * 1.1)) { throw "Not enough room on /sdcard to keep the packs aside ($needKb KB needed, $freeKb KB free): not clearing the app." }
}

# The INSTALLED packs (active marker plus pack.json; never a .partial or a half folder) are copied (never deleted) as one tar next to the Downloads, checked, then renamed
# (Save-PacksAside in common.ps1). Returns the verified copy (Path, Files, PackIds), or $null when there is no installed pack. Anything that cannot be verified throws, before pm clear.
function Move-PacksAside([string]$Phone) {
    $state = Get-PacksState $Phone
    if ($state -eq 'Error') { throw "run-as on $package failed, so the packs cannot be kept aside: not clearing the app." }
    $dir = "$($script:AsideRoot)/$package"
    if ($state -eq 'None') { Write-Host '   no packs to keep aside'; Write-LeftoverArchives $Phone $dir; return $null }
    if (@(Get-InstalledPackFolders $Phone $package).Count -eq 0) { Write-Host '   no installed pack to keep aside (only partial downloads or folders without a manifest)'; Write-LeftoverArchives $Phone $dir; return $null }
    Assert-AsideSpace $Phone
    try { $aside = Save-PacksAside $Phone $package $script:AsideRoot $script:TransferSeconds }
    catch { throw "$($_.Exception.Message): not clearing the app." }
    Write-Host "   packs kept aside: $($aside.Path) ($($aside.PackIds -join ', '), $($aside.Files) files)"
    return $aside
}

function Write-LeftoverArchives([string]$Phone, [string]$Dir) {
    $found = @(Invoke-Adb $Phone @('shell', "ls $Dir/*.tar")) | Where-Object { $_ -match '\.tar$' }
    if ($found) { Write-Host "   NOTE: earlier aside archives are still on the phone and were not restored: $($found -join ', ')" }
}

# Copies the archive back; "packs restored" is said only after every pack in it passes the install check (Restore-PacksAside). The archive is kept either way and its path printed.
function Restore-Packs([string]$Phone, $Aside) {
    $ids = Restore-PacksAside $Phone $package $Aside.Path $script:TransferSeconds
    Write-Host "   packs restored and verified installed ($($ids -join ', ')). The aside copy stays at $($Aside.Path) (delete it when no longer needed)."
}

function Install-CachedApk([string]$Phone) {
    $platform = 'android-dbg-arm64'
    $apk = Find-CachedBuild (Get-BuildDir $App $platform (Get-BuildKey)) '*-Signed.apk'
    if (-not $apk) { throw "No cached $App APK for the current build inputs ($platform). Run audio-timing.ps1 -App $App once (it builds and caches it), then retry." }
    $result = "$(Invoke-Adb $Phone @('install', '-r', $apk.FullName))"
    if ($result -notmatch 'Success') { throw "adb install -r of $package failed: $result" }
    Write-Host "   reinstalled $package from the cache (adb install -r)"
}

function Invoke-LevelSetting([string]$Phone) {
    Invoke-Adb $Phone @('shell', 'am', 'force-stop', $package) | Out-Null
    Set-EnglishLanguage $Phone
    Start-DevApp $Phone $false
    return (Wait-AppAlive $Phone)
}

# pm clear wipes the external files folder too: the harness markers are written again (no-usage always, throttle only if it was there).
function Restore-Markers([string]$Phone, [bool]$Throttle) {
    Set-DevNoUsageMarker $Phone $package
    if ($Throttle) { Set-DevMarker $Phone $package $script:ThrottleMarkerName }
}

function Invoke-LevelClear([string]$Phone, [bool]$Reinstall) {
    Invoke-Adb $Phone @('shell', 'am', 'force-stop', $package) | Out-Null
    $aside = Move-PacksAside $Phone
    $throttle = Test-DevMarker $Phone $package $script:ThrottleMarkerName
    if ($Reinstall) { Install-CachedApk $Phone }
    Invoke-Adb $Phone @('shell', 'pm', 'clear', $package) | Out-Null
    Restore-Markers $Phone $throttle
    if ($aside) {
        try { Restore-Packs $Phone $aside } catch { $script:PacksNotRestored = $aside.Path; throw }
    } else {
        # No installed pack was there to keep aside: an older leftover archive goes back now, AFTER the clear, so nothing it restores is wiped again.
        try { Restore-LeftoverAside $Phone } catch { Write-Host "   WARNING: could not look for a leftover aside archive: $($_.Exception.Message)" }
    }
    Start-DevApp $Phone $true
    return (Wait-AppAlive $Phone)
}

function Invoke-Level([string]$Phone, [string]$Name) {
    Write-Host "== $package, level $Name"
    switch ($Name) {
        'setting' { return (Invoke-LevelSetting $Phone) }
        'clear' { return (Invoke-LevelClear $Phone $false) }
        'reinstall' { return (Invoke-LevelClear $Phone $true) }
    }
}

$script:AsideMaxAgeHours = 24
# packs-yyyyMMdd-HHmmss.tar (Save-PacksAside, stamped with the phone's clock): recent when its stamp is within AsideMaxAgeHours of $PhoneNow (the phone's clock, Get-PhoneStamp parsed); an unreadable name counts as not recent.
function Test-AsideArchiveRecent([string]$Archive, [datetime]$PhoneNow) {
    if ($Archive -notmatch 'packs-(\d{8}-\d{6})\.tar$') { return $false }
    $stamp = [datetime]::MinValue
    if (-not [datetime]::TryParseExact($Matches[1], 'yyyyMMdd-HHmmss', [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref]$stamp)) { return $false }
    return ($PhoneNow - $stamp).TotalHours -le $script:AsideMaxAgeHours
}

# Slice 216: an aside archive left by an earlier failed restore goes back after a clear level's pm clear (never before it, so a later clear cannot wipe it again; only when the app has no installed pack: a pack that is there is never overwritten).
# Restore-PacksAside verifies every pack with the install check afterwards. When it cannot, the reason is said and the run goes on; the archive stays.
function Restore-LeftoverAside([string]$Phone) {
    $archive = @(Invoke-Adb $Phone @('shell', "ls $($script:AsideRoot)/$package/packs-*.tar")) | ForEach-Object { "$_".Trim() } |
        Where-Object { $_ -match '\.tar$' } | Sort-Object -Descending | Select-Object -First 1
    if (-not $archive) { return }
    $phoneNow = [datetime]::ParseExact((Get-PhoneStamp $Phone), 'yyyyMMdd-HHmmss', [Globalization.CultureInfo]::InvariantCulture)
    if (-not (Test-AsideArchiveRecent $archive $phoneNow)) { Write-Host "   NOTE: aside archive $archive is older than $($script:AsideMaxAgeHours) h: not restored (a stale pack version)."; return }
    if (@(Get-InstalledPackFolders $Phone $package).Count -gt 0) { Write-Host "   leftover aside archive $archive kept: the app already has installed packs, nothing to restore."; return }
    Write-Host "   leftover aside archive $archive and no installed pack: restoring it before the reset"
    try { Restore-Packs $Phone ([pscustomobject]@{ Path = $archive }) }
    catch { Write-Host "   WARNING: could not restore the leftover archive ($($_.Exception.Message)); it stays at $archive." }
}

$phone = Find-WirelessPhone $Serial
if (-not $phone) { Write-PairingAsk; exit 2 }
Assert-PhoneLease $phone
foreach ($name in $(if ($Level -eq 'auto') { 'setting', 'clear', 'reinstall' } else { $Level })) {
    # A level that throws (run-as refused, no cached APK, an aside copy that did not verify) counts as not alive and ends in the RESET FAILED line
    # below; auto goes on to the next level. A failed aside stops each clear level before its pm clear.
    $alive = $false
    try { $alive = Invoke-Level $phone $name } catch { Write-Host "   level '$name' failed: $($_.Exception.Message)" }
    if ($alive) { Write-Host "RESET OK at level '$name'"; exit 0 }
    if ($script:PacksNotRestored) {
        # A later clear would see no packs and call that the baseline; stop and leave the complete copy for a hand restore.
        Write-Host "STOPPED: the packs were cleared but not put back. The complete copy is at $($script:PacksNotRestored); no further level is run."
        break
    }
}
Write-Host 'RESET FAILED: the app is still not alive after the last level.'
exit 1
