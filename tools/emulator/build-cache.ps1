# Build-once-per-commit cache for test builds (Slice 180). Dot-sourced, not run.
# A build key is a hash of the build inputs as they are on disk now (committed and uncommitted), so the same key means the same
# APK or MSIX: a second agent, a fixer after a tester, or a rerun finds the finished build instead of making it again.
# Outputs live in <worktrees folder>\_builds\<app>-<platform>-<key>\ (outside every repo; a few newest per worktree are kept).
. (Join-Path $PSScriptRoot 'activity.ps1')

# What a build reads. Documentation, tools, tests and the plan are not inputs, so editing them never invalidates a build.
# Missing paths are skipped. A toolchain or workload change outside the repo is not visible to the key: use -Rebuild after one.
$script:BuildInputPaths = @('src', 'global.json', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Build.rsp', 'Directory.Packages.props',
    'Directory.Solution.props', 'Directory.Solution.targets', '.editorconfig', '.globalconfig', 'NuGet.config', 'nuget.config', 'ReadTheStupidText.slnx')
$script:BuildKeyLength = 12
$script:KeepBuildsPerWorktree = 3
$script:BuildMetaFile = 'build.json'
$script:BuildLockWaitMinutes = 60

function Invoke-Git([string[]]$GitArguments) {
    $ErrorActionPreference = 'Continue'
    $out = & git -C $script:ToolsRepoRoot @GitArguments 2>$null
    if ($LASTEXITCODE -ne 0) { throw "git $($GitArguments -join ' ') failed." }
    return $out
}

# HEAD's inputs with the working tree's changes laid over them, in a throw-away index (the real index and HEAD are never touched).
function Get-BuildInputTree {
    $tempIndex = Join-Path ([IO.Path]::GetTempPath()) ('rtst-key-index-' + [guid]::NewGuid().ToString('N'))
    $present = $script:BuildInputPaths | Where-Object { Test-Path (Join-Path $script:ToolsRepoRoot $_) }
    try {
        $env:GIT_INDEX_FILE = $tempIndex
        Invoke-Git @('read-tree', 'HEAD') | Out-Null
        Invoke-Git (@('add', '-A', '--') + @($present)) | Out-Null
        return (Invoke-Git @('write-tree')) | Select-Object -First 1
    } finally {
        Remove-Item Env:GIT_INDEX_FILE -ErrorAction SilentlyContinue
        Remove-Item $tempIndex -Force -ErrorAction SilentlyContinue
    }
}

function Get-BuildKey {
    $tree = Get-BuildInputTree
    $parts = foreach ($path in $script:BuildInputPaths) {
        $hash = try { Invoke-Git @('rev-parse', "${tree}:$path") | Select-Object -First 1 } catch { $null }
        if ($hash) { "$path=$hash" }
    }
    $sha = [Security.Cryptography.SHA1]::Create()
    $bytes = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($parts -join "`n"))
    return ([BitConverter]::ToString($bytes) -replace '-', '').ToLowerInvariant().Substring(0, $script:BuildKeyLength)
}

function Get-BuildDir([string]$App, [string]$Platform, [string]$Key) {
    return Join-Path (Join-Path (Get-WorktreesRoot) '_builds') "$App-$Platform-$Key"
}

# The finished artifact of a cached build (the metadata file is written last, so a half-copied folder never matches), or $null.
function Find-CachedBuild([string]$Dir, [string]$Filter) {
    if (-not (Test-Path (Join-Path $Dir $script:BuildMetaFile))) { return $null }
    $found = Get-ChildItem $Dir -Filter $Filter -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\Dependencies\\' } | Select-Object -First 1
    if ($found) { (Get-Item $Dir).LastWriteTime = Get-Date }  # a reused build is the newest again
    return $found
}

# A machine-wide lock by name; waits (and says what for) when another agent holds it. Pass the result to Exit-BuildLock in a finally.
function Enter-BuildLock([string]$Name, [string]$What) {
    $mutex = New-Object System.Threading.Mutex($false, $Name)
    try {
        $got = $mutex.WaitOne(0)
        if (-not $got) {
            Write-Host "Waiting for another build of $What to finish (up to $($script:BuildLockWaitMinutes) min)..."
            $got = $mutex.WaitOne([TimeSpan]::FromMinutes($script:BuildLockWaitMinutes))
        }
    } catch [System.Threading.AbandonedMutexException] { $got = $true }
    if (-not $got) { $mutex.Dispose(); throw "Another build of $What was still running after $($script:BuildLockWaitMinutes) minutes." }
    return $mutex
}

function Exit-BuildLock($Mutex) {
    if ($Mutex) { $Mutex.ReleaseMutex(); $Mutex.Dispose() }
}

# The two locks a build takes, always in this order: one per build key (two agents with the same inputs build once; the second
# finds the finished build) and one per checkout (two builds in one worktree share its obj folders, so they run one after the other).
function Enter-BuildLocks([string]$App, [string]$Platform, [string]$Key) {
    $sha = [Security.Cryptography.SHA1]::Create()
    $tree = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($script:ToolsRepoRoot.Path.ToLowerInvariant()))) -replace '-', '').Substring(0, 12)
    $byKey = Enter-BuildLock "Global\RTST_Build_$App-$Platform-$Key" "$App $Platform (key $Key)"
    try { $byTree = Enter-BuildLock "Global\RTST_BuildTree_$tree" "another app in this checkout" }
    catch { Exit-BuildLock $byKey; throw }
    return @($byKey, $byTree)
}

function Exit-BuildLocks($Locks) {
    foreach ($lock in @($Locks)) { Exit-BuildLock $lock }
}

# A fresh folder next to the build folder to build into; Publish-BuildDir renames it into place once the metadata file is in it.
function New-BuildTempDir([string]$Dir) {
    $temp = "$Dir.building-" + [guid]::NewGuid().ToString('N').Substring(0, 8)
    $null = New-Item -ItemType Directory -Force $temp
    return $temp
}

function Publish-BuildDir([string]$Temp, [string]$Dir) {
    Remove-Item $Dir -Recurse -Force -ErrorAction SilentlyContinue
    Move-Item $Temp $Dir
}

function Save-BuildMeta([string]$Dir, [string]$App, [string]$Platform, [string]$Key) {
    @{ app = $App; platform = $Platform; key = $Key; worktree = (Split-Path $script:ToolsRepoRoot -Leaf); built = (Get-Date).ToString('o') } |
        ConvertTo-Json | Set-Content (Join-Path $Dir $script:BuildMetaFile)
}

# Keeps the newest few builds of this app and platform made from this worktree; other worktrees' builds are theirs to prune.
function Remove-OldBuilds([string]$App, [string]$Platform) {
    $root = Join-Path (Get-WorktreesRoot) '_builds'
    if (-not (Test-Path $root)) { return }
    $mine = Split-Path $script:ToolsRepoRoot -Leaf
    $old = Get-ChildItem $root -Directory -Filter "$App-$Platform-*" | Where-Object {
        $meta = Join-Path $_.FullName $script:BuildMetaFile
        (Test-Path $meta) -and ((Get-Content $meta -Raw | ConvertFrom-Json).worktree -eq $mine)
    } | Sort-Object LastWriteTime -Descending | Select-Object -Skip $script:KeepBuildsPerWorktree
    foreach ($dir in $old) { Remove-Item $dir.FullName -Recurse -Force -ErrorAction SilentlyContinue }
}
