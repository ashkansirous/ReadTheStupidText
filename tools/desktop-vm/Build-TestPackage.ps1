<#
.SYNOPSIS
  Builds the unsigned dev MSIX of RTST or Dino Reader for the desktop test VM, once per build key (Slice 180).
.DESCRIPTION
  The README's build commands, scripted. The package goes to <worktrees folder>\_builds\<app>-windows-<key>\ (the key is a hash
  of the build inputs as they are on disk now, see tools/emulator/build-cache.ps1); a second call with the same inputs, from any agent
  or worktree, finds that folder and builds nothing. -Rebuild forces a build. The last output line is the MSIX path.
  Building takes no VM lock: the VM is only held while installing and testing.
.EXAMPLE
  $msix = .\Build-TestPackage.ps1 -App dino | Select-Object -Last 1
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('rtst', 'dino')] [string] $App,
    [switch] $Rebuild
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\emulator\build-cache.ps1')

$repoRoot = $script:ToolsRepoRoot
$msixFilter = if ($App -eq 'rtst') { 'ReadTheStupidText.App_*.msix' } else { 'DinoReader.App_*.msix' }

function Find-MsBuild {
    $onPath = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $found = if (Test-Path $vswhere) { & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1 }
    if (-not $found) { throw 'msbuild was not found (not on PATH, and vswhere found no Visual Studio with MSBuild).' }
    return $found
}

function Build-RtstMsix([string]$Dir) {
    $msbuild = Find-MsBuild
    # Slice 225 item 23: msbuild.exe has no --disable-build-servers (a dotnet CLI option): its twin is -nodeReuse:false plus no shared compiler.
    & $msbuild (Join-Path $repoRoot 'src\ReadTheStupidText.App\ReadTheStupidText.App.csproj') -restore -nodeReuse:false -p:UseSharedCompilation=false -p:Configuration=Release -p:Platform=x64 `
        -p:RuntimeIdentifier=win-x64 -p:GenerateAppxPackageOnBuild=true "-p:AppxPackageDir=$Dir\\" `
        -p:UapAppxPackageBuildMode=SideloadOnly -p:AppxBundle=Never -p:AppxPackageSigningEnabled=false
    if ($LASTEXITCODE -ne 0) { throw 'msbuild failed.' }
}

# dotnet publish writes the package under the project's AppPackages folder; copy it (with Dependencies\x64) into the build folder.
function Build-DinoMsix([string]$Dir) {
    $packages = Join-Path $repoRoot 'src\DinoReader.App\AppPackages'
    Remove-Item $packages -Recurse -Force -ErrorAction SilentlyContinue
    dotnet publish (Join-Path $repoRoot 'src\DinoReader.App') -f net10.0-windows10.0.19041.0 -c Release --disable-build-servers -p:IncludeAndroid=false `
        -p:Platform=x64 -p:AppxPackageSigningEnabled=false
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
    if (-not (Test-Path $packages)) { throw "dotnet publish wrote no AppPackages folder under src\DinoReader.App." }
    Copy-Item "$packages\*" $Dir -Recurse -Force
}

# Builds into a temp folder and renames it into place once the metadata file is in it. The caller holds the build locks.
function Build-Msix([string]$Dir, [string]$Key) {
    $temp = New-BuildTempDir $Dir
    try {
        if ($App -eq 'rtst') { Build-RtstMsix $temp } else { Build-DinoMsix $temp }
        if (-not (Find-NewBuild $temp)) { throw "No $msixFilter was produced under $temp." }
        Save-BuildMeta $temp $App 'windows' $Key
        Publish-BuildDir $temp $Dir
    } finally { Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue }
    Remove-OldBuilds $App 'windows'
}

# Builds the package for this key unless another agent finished it while this one waited for the build locks. The activity is
# registered first (it may wait for a quiet-machine timing run) so that no build lock is held during that wait. The key is computed
# again once the locks are held (the files may have changed meanwhile); $script:key and $script:buildDir end up as the real key.
function Get-MsixForKey {
    $activity = Start-Activity "build-$App-windows"
    try {
        $locks = Enter-BuildLocks $App 'windows' $script:key
        try {
            $script:key = Get-BuildKey
            $script:buildDir = Get-BuildDir $App 'windows' $script:key
            $cached = if ($Rebuild) { $null } else { Find-CachedBuild $script:buildDir $msixFilter }
            if ($cached) { Write-Host "Another agent built the $App MSIX for key $($script:key) meanwhile; reusing it."; return $cached }
            $priority = Set-BuildPriority  # BelowNormal: the build never starves flows or the VM (msbuild/dotnet inherit it)
            try { Build-Msix $script:buildDir $script:key | Out-Host }
            finally { Restore-ProcessPriority $priority }
        } finally { Exit-BuildLocks $locks }
    } finally { Stop-Activity $activity }
    return Find-CachedBuild $script:buildDir $msixFilter
}

# Before the metadata file exists, so a half-built folder is never reused: look for the package itself.
function Find-NewBuild([string]$Dir) {
    return Get-ChildItem $Dir -Filter $msixFilter -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\Dependencies\\' } | Select-Object -First 1
}

$key = Get-BuildKey
$buildDir = Get-BuildDir $App 'windows' $key
$msix = if ($Rebuild) { $null } else { Find-CachedBuild $buildDir $msixFilter }
if ($msix) { Write-Host "Reusing the $App MSIX built for key ${key}." }
else {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $msix = Get-MsixForKey
    Write-Host ("Built the {0} MSIX in {1:n1} s (key {2})." -f $App, $sw.Elapsed.TotalSeconds, $key)
}
Write-Output $msix.FullName
