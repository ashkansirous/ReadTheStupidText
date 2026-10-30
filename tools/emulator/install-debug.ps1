<#
.SYNOPSIS
  Builds the x86_64 Debug APK of RTST or Dino Reader, installs it on the warm emulator and launches it.
.DESCRIPTION
  Slice 180: the APK is built once per build key (the build inputs as they are on disk, see build-cache.ps1) into
  <worktrees folder>\_builds\<app>-android-<key>\ and reused by every later run, agent and worktree with the same key;
  -Rebuild forces a build. The emulator boots while the build runs. -Instance 1|2 picks the emulator (see start-warm.ps1).
  The build holds no emulator lock: the device is only needed for the install and the run that follows.
#>
param(
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [ValidateSet(1, 2)][int]$Instance = 1,
    [switch]$Rebuild,
    # Slice 207: build (or find) the APK and stop: no emulator boot, no install. run-flows.ps1 uses it so the build happens before it takes the emulator lease.
    [switch]$BuildOnly
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'build-cache.ps1')
Set-EmulatorInstance $Instance

$apps = @{
    rtst = @{ Project = 'src\ReadTheStupidText.Mobile\ReadTheStupidText.Mobile.csproj'; Package = 'uk.sirous.readthestupidtext' }
    dino = @{ Project = 'src\DinoReader.App\DinoReader.App.csproj'; Package = 'uk.sirous.dinoreader' }
}
$cfg = $apps[$App]
$project = Join-Path $script:RepoRoot $cfg.Project
$apkFilter = '*-Signed.apk'

# Boots the emulator in a second process so the boot (~45 s) overlaps the build.
function Start-EmulatorInBackground {
    $null = New-Item -ItemType Directory -Force $script:StateDir
    $log = Join-Path $script:StateDir "boot-$($script:EmuInstance).log"
    $pwsh = (Get-Process -Id $PID).Path
    $startScript = Join-Path $PSScriptRoot 'start-warm.ps1'
    return Start-Process -FilePath $pwsh -ArgumentList @('-NoProfile', '-File', "`"$startScript`"", '-Instance', $script:EmuInstance) `
        -WindowStyle Hidden -PassThru -RedirectStandardOutput $log -RedirectStandardError "$log.err" | Add-Member -NotePropertyName Log -NotePropertyValue $log -PassThru
}

function Wait-EmulatorInBackground($Boot) {
    if (-not $Boot.HasExited) { Write-Host "Waiting for $($script:Serial) to finish booting (up to ~4 min)..." }
    $Boot.WaitForExit()
    $null = $Boot.Handle  # keeps the process handle open so that ExitCode is read (it can be $null without it)
    Get-Content $Boot.Log -ErrorAction SilentlyContinue | Write-Host
    if ($null -ne $Boot.ExitCode -and $Boot.ExitCode -ne 0) { throw "start-warm.ps1 failed (exit $($Boot.ExitCode)): $(Get-Content "$($Boot.Log).err" -Raw -ErrorAction SilentlyContinue)" }
}

# Debug x86_64 build into the project's bin folder, then copied into a temp folder that is renamed into the cache folder once the
# metadata file is in it (the metadata file is written last). The caller holds the build locks (Enter-BuildLocks).
function Build-Apk([string]$Dir, [string]$Key) {
    $apkDir = Join-Path (Split-Path $project) 'bin\Debug\net10.0-android\android-x64'
    # Remove old signed APKs first, so the one found afterwards is certainly from this build.
    Get-ChildItem $apkDir -Filter $apkFilter -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
    dotnet build $project -f net10.0-android -c Debug -p:RuntimeIdentifier=android-x64 `
        -p:EmbedAssembliesIntoApk=true -p:IncludeWindows=false -p:AndroidPackageFormat=apk
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $built = Get-ChildItem $apkDir -Filter $apkFilter -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $built) { throw 'Signed APK not found.' }
    $temp = New-BuildTempDir $Dir
    try {
        Copy-Item $built.FullName $temp
        Save-BuildMeta $temp $App 'android' $Key
        Publish-BuildDir $temp $Dir
    } finally { Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue }
    Remove-OldBuilds $App 'android'
}

# Builds the APK for this key unless another agent finished it while this one waited for the build locks. The activity is registered
# first (it may wait for a quiet-machine timing run) so that no build lock is held during that wait. The key is computed again once
# the locks are held: the build compiles what is on disk now, and the files may have changed while this run waited. $script:key and
# $script:buildDir are updated to the key the build really has.
function Get-ApkForKey {
    $activity = Start-Activity "build-$App-android"
    try {
        $locks = Enter-BuildLocks $App 'android' $script:key
        try {
            $script:key = Get-BuildKey
            $script:buildDir = Get-BuildDir $App 'android' $script:key
            $cached = if ($Rebuild) { $null } else { Find-CachedBuild $script:buildDir $apkFilter }
            if ($cached) { Write-Host "Another agent built the $App APK for key $($script:key) meanwhile; reusing it."; return $cached }
            $priority = Set-BuildPriority  # BelowNormal: the build never starves flows or the VM (children inherit it)
            try { Build-Apk $script:buildDir $script:key | Out-Host }  # the build's own output must not become this function's result
            finally { Restore-ProcessPriority $priority }
        } finally { Exit-BuildLocks $locks }
    } finally { Stop-Activity $activity }
    return Find-CachedBuild $script:buildDir $apkFilter
}

$key = Get-BuildKey
$buildDir = Get-BuildDir $App 'android' $key
$apk = if ($Rebuild) { $null } else { Find-CachedBuild $buildDir $apkFilter }
$boot = if ($BuildOnly) { $null } else { Start-EmulatorInBackground }
$buildError = $null
try {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    if ($apk) { Write-Host "Reusing the $App APK built for key ${key}: $($apk.FullName)" }
    else {
        $apk = Get-ApkForKey
        Write-Host ("Ready in {0:n1} s (key {1}): {2}" -f $sw.Elapsed.TotalSeconds, $key, $apk.FullName)
    }
} catch { $buildError = $_ }
# A failed build is the error to see: the boot is still waited for (it must not be left half-started), but its outcome only matters after a good build.
if ($boot) { try { Wait-EmulatorInBackground $boot } catch { if (-not $buildError) { throw } else { Write-Warning "Emulator boot also failed: $($_.Exception.Message)" } } }
if ($buildError) { throw $buildError }
if ($BuildOnly) { return }

Remove-InstalledKey $App  # until the install succeeds, the device holds no known build
& $script:Adb -s $script:Serial install --no-incremental -r $apk.FullName
if ($LASTEXITCODE -ne 0) { throw 'adb install failed.' }
Save-InstalledKey $App $key
# Slice 179: counting off for every harness run, even a Release build. The app reads this marker in its external files folder (UsageEligibility.NoUsageMarkerFile).
$ErrorActionPreference = 'Continue'
$filesDir = "/sdcard/Android/data/$($cfg.Package)/files"
& $script:Adb -s $script:Serial shell "mkdir -p $filesDir && touch $filesDir/no-usage" 2>&1 | Out-Null
$markerListing = "$(& $script:Adb -s $script:Serial shell "ls $filesDir/no-usage" 2>&1)"
if ($markerListing -notmatch 'no-usage' -or $markerListing -match 'No such file|denied') { throw "Could not write the no-usage marker $filesDir/no-usage ($markerListing)." }
$ErrorActionPreference = 'Continue'  # monkey writes its banner to stderr
& $script:Adb -s $script:Serial shell monkey -p $cfg.Package -c android.intent.category.LAUNCHER 1 2>&1 | Out-Null
$abi = & $script:Adb -s $script:Serial shell getprop ro.product.cpu.abi
Write-Host "Launched $($cfg.Package) on $($script:Serial) ($abi)."
