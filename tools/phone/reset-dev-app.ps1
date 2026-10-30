<#
.SYNOPSIS
  The reset ladder for a broken dev install on the real phone (Slice 206).
.DESCRIPTION
  Touches only the Debug dev packages uk.sirous.readthestupidtext.dbg and uk.sirous.dinoreader.dbg (a hard-coded allow-list, Assert-DevPackage); never the store app.
  -Level setting   force-stop, put the app language back to English in the app's shared preferences (RTST key "Language", MobilePreferencesSettingsStore;
                   Dino key "dino.ui.language", LanguagePage) through run-as, relaunch, confirm the app logs within -AliveSeconds (by pid, only lines after the
                   launch time; the shared log buffer is not cleared) and stays up.
  -Level clear     copy the downloaded packs to /sdcard/Download/rtst-packs-aside/<pkg>/packs-<timestamp>.tar and verify that archive (tar exit code, size > 0,
                   `tar -tf` file count equal to the packs folder's) BEFORE pm clear; any run-as or tar failure stops the level with the packs untouched. Then pm clear,
                   write the no-usage marker (and the throttle marker if it was there) again, copy the packs back and check the file count, relaunch with the
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

function Get-PacksFileCount([string]$Phone) {
    $out = @(Invoke-Adb $Phone @('shell', "run-as $package find $($script:PacksDir) -type f"))
    if ($global:LASTEXITCODE -ne 0) { throw "Could not count the packs on the phone ($($out -join ' | '))." }
    return @($out | Where-Object { "$_".Trim() -ne '' }).Count
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

# The packs are copied (never deleted) as one tar next to the Downloads, written to a .part file, checked, then renamed. Returns the verified archive, or
# $null when there are no packs. Anything that cannot be verified throws, before pm clear.
function Move-PacksAside([string]$Phone) {
    $state = Get-PacksState $Phone
    if ($state -eq 'Error') { throw "run-as on $package failed, so the packs cannot be kept aside: not clearing the app." }
    $dir = "$($script:AsideRoot)/$package"
    if ($state -eq 'None') { Write-Host '   no packs to keep aside'; Write-LeftoverArchives $Phone $dir; return $null }
    Assert-AsideSpace $Phone
    $expected = Get-PacksFileCount $Phone
    $archive = "$dir/packs-$(Get-Date -Format 'yyyyMMdd-HHmmss').tar"
    # Piped through cat on purpose: on the S21 (Android 15) `run-as tar -cf - ... > file` wrote 0 bytes when the file is on /sdcard (fuse); `| cat > file` wrote the real size.
    $out = Invoke-Adb $Phone @('shell', "mkdir -p $dir && run-as $package tar -cf - $($script:PacksDir) | cat > $archive.part") $script:TransferSeconds
    if ($global:LASTEXITCODE -ne 0) { Remove-PartArchive $Phone $archive; throw "tar of the packs failed ($($out -join ' | ')): not clearing the app." }
    Test-AsideArchive $Phone $archive $expected
    Invoke-Adb $Phone @('shell', "mv $archive.part $archive") | Out-Null
    Write-Host "   packs kept aside: $archive ($expected files)"
    Remove-OldArchives $Phone $dir
    return [pscustomobject]@{ Path = $archive; Files = $expected }
}

# Keeps the newest KeepAsideArchives packs-*.tar of this package; the one just written is always among them. Only files named like ours are touched.
function Remove-OldArchives([string]$Phone, [string]$Dir) {
    $names = @(Invoke-Adb $Phone @('shell', "ls $Dir"))
    foreach ($old in Get-ArchivesToPrune $names $script:KeepAsideArchives) {
        Invoke-Adb $Phone @('shell', "rm -f $Dir/$old") | Out-Null
        Write-Host "   removed the oldest aside archive $old (the newest $($script:KeepAsideArchives) are kept)"
    }
}

function Remove-PartArchive([string]$Phone, [string]$Archive) {
    Invoke-Adb $Phone @('shell', "rm -f $Archive.part") | Out-Null
}

function Test-AsideArchive([string]$Phone, [string]$Archive, [int]$ExpectedFiles) {
    $size = "$(Invoke-Adb $Phone @('shell', "stat -c %s $Archive.part"))".Trim()
    $listing = @(Invoke-Adb $Phone @('shell', "tar -tf $Archive.part"))
    $tarExit = $global:LASTEXITCODE
    $listed = Get-TarFileCount $listing
    if ($size -notmatch '^\d+$' -or [long]$size -le 0 -or $tarExit -ne 0 -or $listed -ne $ExpectedFiles) {
        Remove-PartArchive $Phone $Archive
        throw "The aside copy is not good (size '$size', tar -tf exit $tarExit, $listed of $ExpectedFiles files): not clearing the app."
    }
}

function Write-LeftoverArchives([string]$Phone, [string]$Dir) {
    $found = @(Invoke-Adb $Phone @('shell', "ls $Dir/*.tar")) | Where-Object { $_ -match '\.tar$' }
    if ($found) { Write-Host "   NOTE: earlier aside archives are still on the phone and were not restored: $($found -join ', ')" }
}

# Copies the archive back and checks the file count; the archive is kept either way and its path printed.
function Restore-Packs([string]$Phone, $Aside) {
    $out = Invoke-Adb $Phone @('shell', "cat $($Aside.Path) | run-as $package tar -xf -") $script:TransferSeconds
    $restoreExit = $global:LASTEXITCODE
    $restored = if ($restoreExit -eq 0) { Get-PacksFileCount $Phone } else { -1 }
    if ($restoreExit -ne 0 -or $restored -ne $Aside.Files) {
        throw "Putting the packs back failed (tar exit $restoreExit, $restored of $($Aside.Files) files; $($out -join ' | ')). The complete copy is still at $($Aside.Path); nothing was deleted."
    }
    Write-Host "   packs restored and listed back ($restored files). The aside copy stays at $($Aside.Path) (delete it when no longer needed)."
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
