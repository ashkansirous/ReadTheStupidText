<#
.SYNOPSIS
  Builds the x86_64 Debug APK of RTST or Dino Reader, installs it on the warm emulator and launches it.
.DESCRIPTION
  Slice 180: the APK is built once per build key (the build inputs as they are on disk, see build-cache.ps1) into
  <worktrees folder>\_builds\<app>-android-<key>\ and reused by every later run, agent and worktree with the same key;
  -Rebuild forces a build. The emulator boots while the build runs. -Instance 1|2|3|tablet picks the emulator (see start-warm.ps1; 3 or tablet is RTST_Tablet).
  Slice 242: -NoBuild (or RTST_NO_BUILD=1) starts no build: a cache miss prints the "cache missed" line and exits 6; -Rebuild with -NoBuild is refused (exit 6).
  The build holds no emulator lock: the device is only needed for the install and the run that follows.
#>
param(
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [ValidateSet('1', '2', '3', 'tablet')][string]$Instance = '1',   # Slice 242: 3 or 'tablet' is RTST_Tablet
    [switch]$Rebuild,
    # Slice 207: build (or find) the APK and stop: no emulator boot, no install. run-flows.ps1 uses it so the build happens before it takes the emulator lease.
    [switch]$BuildOnly,
    # Slice 242: start no build; on a cache miss print `cache missed for <app>, key <k>: run install-debug.ps1 -BuildOnly first` and exit 6 (also RTST_NO_BUILD=1).
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'build-cache.ps1')
Set-EmulatorInstance (Resolve-EmulatorInstance $Instance)

$apps = @{
    rtst = @{ Project = 'src\ReadTheStupidText.Mobile\ReadTheStupidText.Mobile.csproj'; Package = $script:EmulatorPackages.rtst }
    dino = @{ Project = 'src\DinoReader.App\DinoReader.App.csproj'; Package = $script:DinoDevPackage }
}
$cfg = $apps[$App]
$platform = Get-EmulatorBuildPlatform $App  # Dino: android-dbg, its own cache name (the APK carries the .dbg id)
$project = Join-Path $script:RepoRoot $cfg.Project
$apkFilter = '*-Signed.apk'

# Slice 241: the boot overlaps the build through the shared detached starter (speed.ps1): no handle of this script's caller is inherited.
function Start-EmulatorInBackground { return (Start-EmulatorBootInBackground $script:EmuInstance) }
function Wait-EmulatorInBackground($Boot) { Wait-EmulatorBoot $Boot }

# Debug x86_64 build into the project's bin folder, then copied into a temp folder that is renamed into the cache folder once the
# metadata file is in it (the metadata file is written last). The caller holds the build locks (Enter-BuildLocks).
function Build-Apk([string]$Dir, [string]$Key) {
    $apkDir = Join-Path (Split-Path $project) 'bin\Debug\net10.0-android\android-x64'
    # Remove old signed APKs first, so the one found afterwards is certainly from this build.
    Get-ChildItem $apkDir -Filter $apkFilter -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
    # Slice 225 item 23: --disable-build-servers, so no VBCSCompiler or MSBuild node outlives the build and holds the worktree.
    dotnet build $project -f net10.0-android -c Debug --disable-build-servers -p:RuntimeIdentifier=android-x64 `
        -p:EmbedAssembliesIntoApk=true -p:IncludeWindows=false -p:AndroidPackageFormat=apk "-p:ApplicationId=$($cfg.Package)"
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $built = Get-ChildItem $apkDir -Filter $apkFilter -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $built) { throw 'Signed APK not found.' }
    $temp = New-BuildTempDir $Dir
    try {
        Copy-Item $built.FullName $temp
        Save-BuildMeta $temp $App $platform $Key
        Publish-BuildDir $temp $Dir
    } finally { Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue }
    Remove-OldBuilds $App $platform
}

# Builds the APK for this key unless another agent finished it while this one waited for the build locks. The activity is registered
# first (it may wait for a quiet-machine timing run) so that no build lock is held during that wait. The key is computed again once
# the locks are held: the build compiles what is on disk now, and the files may have changed while this run waited. $script:key and
# $script:buildDir are updated to the key the build really has.
function Get-ApkForKey {
    $activity = Start-Activity "build-$App-android"
    try {
        $locks = Enter-BuildLocks $App $platform $script:key
        try {
            $script:key = Get-BuildKey
            $script:buildDir = Get-BuildDir $App $platform $script:key
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
$buildDir = Get-BuildDir $App $platform $key
if ($Rebuild -and (Test-NoBuildRequested $NoBuild)) {
    Write-Host '-Rebuild and -NoBuild contradict each other (a rebuild is a build): nothing was built.'
    exit $script:EmulatorExitCacheMissed
}
$apk = if ($Rebuild) { $null } else { Find-CachedBuild $buildDir $apkFilter }
if ((Test-NoBuildRequested $NoBuild) -and -not (Find-CachedBuild (Get-NoBuildLookupDir $buildDir) $apkFilter)) {
    Write-Host (Format-EmulatorNoBuildMiss $App $key)
    exit $script:EmulatorExitCacheMissed
}
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
Remove-StaleDevPackages $App  # Slice 221: the plain Dino store id goes from an emulator, never from a phone
& $script:Adb -s $script:Serial install --no-incremental -r $apk.FullName
if ($LASTEXITCODE -ne 0) { throw 'adb install failed.' }
if ($App -eq 'dino') {
    $dinoProblem = Resolve-DinoPackageProblem "$(& $script:Adb -s $script:Serial shell pm list packages 2>&1 | Out-String)" $script:Serial
    if ($dinoProblem) { throw "Dino package check failed: $dinoProblem" }
    Write-Host "Dino package check: exactly one dinoreader package ($($script:DinoDevPackage))."
}
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
