# Build-once-per-commit cache for test builds (Slice 180). Dot-sourced, not run.
# A build key is a hash of the build inputs as they are on disk now (committed and uncommitted), so the same key means the same
# APK or MSIX: a second agent, a fixer after a tester, or a rerun finds the finished build instead of making it again.
# Outputs live in <worktrees folder>\_builds\<app>-<platform>-<key>\ (outside every repo; a few newest per worktree are kept).
. (Join-Path $PSScriptRoot 'activity.ps1')
. (Join-Path $PSScriptRoot '..\path-length.ps1')  # Slice 266: Assert-BuildPathLength for the emulator build

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

# Slice 275 item 4: the "<path>=<tree hash>" lines the key is made of, so a miss can name the input that differs from an earlier key's.
# Returns the tree hash and the lines together: the key is hashed from exactly this list (one computation per key).
function Get-BuildKeyInputs {
    $tree = Get-BuildInputTree
    # Slice 294: one ls-tree call that throws on a git failure. The old per-path rev-parse sat in a try/catch that dropped the path on ANY failure,
    # so a transient git error turned into a key with one input fewer (a silent, different key); only a path that is not in the tree is left out now.
    # core.quotePath=false keeps a non-ASCII path unquoted so it still matches; empty output (no input path in the tree) gives no lines.
    $hashes = @{}
    foreach ($line in @(Invoke-Git (@('-c', 'core.quotePath=false', 'ls-tree', $tree, '--') + $script:BuildInputPaths)) | Where-Object { "$_".Trim() }) {
        $fields = ("$line" -split "`t", 2)
        if ($fields.Count -lt 2) { continue }
        $hashes[$fields[1]] = ($fields[0] -split ' ')[2]
    }
    $lines = @(foreach ($path in $script:BuildInputPaths) { if ($hashes.ContainsKey($path)) { "$path=$($hashes[$path])" } })
    # Slice 294: no input line means the wrong root or an empty tree; every such tree would hash to the same key and reuse a foreign build.
    if ($lines.Count -eq 0) { throw "No build input ($($script:BuildInputPaths -join ', ')) was found in git tree $tree of $($script:ToolsRepoRoot): refusing to make a build key from nothing." }
    return [pscustomobject]@{ Tree = $tree; Inputs = $lines }
}

# The result of the last Get-BuildKeyResult per key, so the miss printout and Save-BuildMeta use the list the key was hashed from and never recompute.
$script:BuildKeyResults = @{}

function Get-BuildKeyResult {
    $computed = Get-BuildKeyInputs
    $sha = [Security.Cryptography.SHA1]::Create()
    $bytes = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(@($computed.Inputs) -join "`n"))
    $key = ([BitConverter]::ToString($bytes) -replace '-', '').ToLowerInvariant().Substring(0, $script:BuildKeyLength)
    $result = [pscustomobject]@{ Key = $key; Tree = $computed.Tree; Inputs = @($computed.Inputs) }
    $script:BuildKeyResults[$key] = $result
    return $result
}

# Printed on a cache miss: compare two runs' lines to see which input changed between them (the key itself is only a hash of them).
# Diagnostic only: it never recomputes, and a failure here is logged, never turned into a different exit code.
function Write-BuildKeyInputs([string]$Key) {
    try {
        $result = $script:BuildKeyResults[$Key]
        if (-not $result) { Write-Host "Build key ${Key}: its inputs were not recorded in this process, so none are listed."; return }
        Write-Host "Build key $Key is made of (git tree $($result.Tree)):"
        foreach ($line in $result.Inputs) { Write-Host "  $line" }
        $newest = Get-NewestBuildInputs
        if ($newest) {
            $differ = @($result.Inputs | Where-Object { $newest.inputs -notcontains $_ })
            Write-Host "Inputs that differ from the newest build of this worktree (key $($newest.key)): $(if ($differ.Count) { $differ -join ', ' } else { 'none listed (an input path was added or removed)' })"
        } else { Write-Host 'No earlier build of this worktree recorded its inputs, so nothing to compare with.' }
    } catch { Write-Host "Could not list the inputs of build key ${Key}: $($_.Exception.Message)" }
}

# The newest build of this worktree whose build.json lists its inputs (written by Save-BuildMeta since Slice 275), or $null.
function Get-NewestBuildInputs {
    $root = Join-Path (Get-WorktreesRoot) '_builds'
    if (-not (Test-Path $root)) { return $null }
    $mine = Split-Path $script:ToolsRepoRoot -Leaf
    foreach ($dir in (Get-ChildItem $root -Directory | Sort-Object LastWriteTime -Descending)) {
        $meta = Join-Path $dir.FullName $script:BuildMetaFile
        if (-not (Test-Path $meta)) { continue }
        $json = Get-Content $meta -Raw | ConvertFrom-Json
        if ($json.worktree -eq $mine -and $json.inputs -and $json.inputsRecorded -ne $false) { return $json }
    }
    return $null
}

# The key lookup itself fails loudly on a git failure (a wrong key is worse than none).
function Get-BuildKey {
    return (Get-BuildKeyResult).Key
}

function Get-BuildDir([string]$App, [string]$Platform, [string]$Key) {
    return Join-Path (Join-Path (Get-WorktreesRoot) '_builds') "$App-$Platform-$Key"
}

# Slice 242 item 3: -NoBuild (switch or RTST_NO_BUILD=1) on install-debug.ps1 and run-flows.ps1: a timed run never spends its minutes building. On a cache miss the script
# prints the line below and exits with this code. RTST_BUILD_CACHE_LOOKUP_ROOT is the self-test seam (same name as the phone runner's): the lookup then reads that folder.
$script:EmulatorExitCacheMissed = 6

function Test-NoBuildRequested([bool]$NoBuildSwitch) { return ($NoBuildSwitch -or $env:RTST_NO_BUILD -eq '1') }

function Format-EmulatorNoBuildMiss([string]$App, [string]$Key) { return "cache missed for $App, key ${Key}: run install-debug.ps1 -BuildOnly first" }

# Slice 294: every -NoBuild cache miss in the emulator scripts prints the miss line and then the key's inputs and tree hash through here.
function Write-EmulatorNoBuildMiss([string]$App, [string]$Key) {
    Write-Host (Format-EmulatorNoBuildMiss $App $Key)
    Write-BuildKeyInputs $Key
}

function Get-NoBuildLookupDir([string]$Dir) {
    if (-not $env:RTST_BUILD_CACHE_LOOKUP_ROOT) { return $Dir }
    return Join-Path $env:RTST_BUILD_CACHE_LOOKUP_ROOT (Split-Path $Dir -Leaf)
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
# finds the finished build) and one per checkout and scope (two builds sharing a checkout's obj folders must pass the same scope, so they run one after the other; every caller now passes one). A build with its own
# --artifacts-path (the phone builds) shares nothing with the others: it passes a $TreeScope (its variant), so only a second build of the same variant waits.
function Enter-BuildLocks([string]$App, [string]$Platform, [string]$Key, [string]$TreeScope = '') {
    $sha = [Security.Cryptography.SHA1]::Create()
    $tree = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($script:ToolsRepoRoot.Path.ToLowerInvariant()))) -replace '-', '').Substring(0, 12)
    if ($TreeScope) { $tree = "$tree-$TreeScope" }
    $byKey = Enter-BuildLock "Global\RTST_Build_$App-$Platform-$Key" "$App $Platform (key $Key)"
    try { $byTree = Enter-BuildLock "Global\RTST_BuildTree_$tree" "another app in this checkout" }
    catch { Exit-BuildLock $byKey; throw }
    return @($byKey, $byTree)
}

function Exit-BuildLocks($Locks) {
    foreach ($lock in @($Locks)) { Exit-BuildLock $lock }
}

# Slice 266: the emulator (Android) and Windows test builds each build into their own --artifacts-path (restore included), so they share no obj or bin folder
# and the per-checkout lock is per scope, not per checkout. The scope names are short on purpose (aapt2 fails past the Windows path limit, APT2264).
$script:TestArtifactsFolderName = '.artifacts'
function Get-TestBuildScope([string]$Prefix, [string]$App) { return "$Prefix-$App" }
function Get-TestBuildArtifactsPath([string]$Scope) { return Join-Path (Join-Path $script:ToolsRepoRoot $script:TestArtifactsFolderName) $Scope }

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

# The inputs and tree are the ones the key was hashed from (never recomputed); they are diagnostic, so a git failure is logged and a finished build is still saved.
function Get-RecordedBuildInputs([string]$Key) {
    try {
        $result = $script:BuildKeyResults[$Key]
        if (-not $result) { $result = Get-BuildKeyResult }
        if ($result.Key -ne $Key) { Write-Host "Inputs recorded for build key $($result.Key), not ${Key}: the tree changed since the key was computed."; return $null }
        return $result
    } catch {
        Write-Host "Could not record the inputs of build key ${Key}: $($_.Exception.Message)"
        return $null
    }
}

function Save-BuildMeta([string]$Dir, [string]$App, [string]$Platform, [string]$Key) {
    $recorded = Get-RecordedBuildInputs $Key
    $inputs = if ($recorded) { @($recorded.Inputs) } else { @() }
    $tree = if ($recorded) { $recorded.Tree } else { $null }
    # inputsRecorded = $false marks a record that failed (git error, or the tree changed since the key): an empty list there is "unknown", not "no inputs".
    @{ app = $App; platform = $Platform; key = $Key; worktree = (Split-Path $script:ToolsRepoRoot -Leaf); built = (Get-Date).ToString('o'); tree = $tree; inputs = $inputs; inputsRecorded = [bool]$recorded } |
        ConvertTo-Json | Set-Content (Join-Path $Dir $script:BuildMetaFile)
}

# Keeps the newest few builds of this app and platform made from this worktree; other worktrees' builds are theirs to prune.
function Remove-OldBuilds([string]$App, [string]$Platform) {
    $root = Join-Path (Get-WorktreesRoot) '_builds'
    if (-not (Test-Path $root)) { return }
    $mine = Split-Path $script:ToolsRepoRoot -Leaf
    # The exact prefix: platform "android" must not match "android-dbg-arm64-..." and "android-dbg-arm64" must not match "...-aot-...".
    $prefix = [regex]::Escape("$App-$Platform-") + "[0-9a-f]{$($script:BuildKeyLength)}"
    $dirs = @(Get-ChildItem $root -Directory)
    $old = $dirs | Where-Object { $_.Name -match "^$prefix$" } | Where-Object {
        $meta = Join-Path $_.FullName $script:BuildMetaFile
        (Test-Path $meta) -and ((Get-Content $meta -Raw | ConvertFrom-Json).worktree -eq $mine)
    } | Sort-Object LastWriteTime -Descending | Select-Object -Skip $script:KeepBuildsPerWorktree
    # A build killed half way leaves its temp folder behind (an APK or MSIX each); an hour is longer than any build.
    $stale = $dirs | Where-Object { $_.Name -match "^$prefix\.building-" -and $_.LastWriteTime -lt (Get-Date).AddHours(-1) }
    foreach ($dir in @($old) + @($stale) | Where-Object { $_ }) { Remove-Item $dir.FullName -Recurse -Force -ErrorAction SilentlyContinue }
}

# The newest cached build of this app and platform (any key), or $null. For callers that must not make a build when the current inputs have none.
function Find-NewestCachedBuild([string]$App, [string]$Platform, [string]$Filter) {
    $root = Join-Path (Get-WorktreesRoot) '_builds'
    if (-not (Test-Path $root)) { return $null }
    $prefix = [regex]::Escape("$App-$Platform-") + "[0-9a-f]{$($script:BuildKeyLength)}"
    $newest = Get-ChildItem $root -Directory | Where-Object { $_.Name -match "^$prefix$" } | Sort-Object LastWriteTime -Descending
    foreach ($dir in $newest) {
        $found = Find-CachedBuild $dir.FullName $Filter
        if ($found) { return $found }
    }
    return $null
}
