<#
.SYNOPSIS
  Self-test for Slice 294: install-debug.ps1 -BuildOnly's caller and run-flows.ps1 -NoBuild compute ONE build key for one tree. Needs no device, no build, no emulator.
  Both scripts are run through the -NoBuild cache miss (an empty lookup root), which prints the key each one computed; the keys must be equal to each other,
  to Get-BuildKey in this process, and stable across repeated calls. Also checks that only build-cache.ps1 builds a key. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
if (-not $env:ANDROID_HOME -and -not $env:ANDROID_SDK_ROOT) { $env:ANDROID_HOME = [IO.Path]::GetTempPath() }
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'build-cache.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

function Get-KeyFromMissLine([string]$Output) {
    if ($Output -match 'cache missed for \w+, key ([0-9a-f]{12}):') { return $Matches[1] }
    return ''
}

$expected = Get-BuildKey
Assert-Equal 'key is 12 hex characters' $true ($expected -match '^[0-9a-f]{12}$')
for ($i = 0; $i -lt 3; $i++) { Assert-Equal "Get-BuildKey call $($i + 2) equals the first" $expected (Get-BuildKey) }

# By construction: no other script under tools computes a key of its own (code only: comment text is stripped first).
$toolsRoot = Split-Path $PSScriptRoot -Parent
foreach ($script in Get-ChildItem $toolsRoot -Filter '*.ps1' -Recurse | Where-Object { $_.Name -notin @('build-cache.ps1', 'test-build-key.ps1') }) {
    $code = (Get-Content $script.FullName) -replace '(^|\s)#.*$', ''
    $own = [bool]($code | Select-String -Pattern 'Get-BuildInputTree|Get-BuildKeyInputs|BuildInputPaths|BuildKeyResults|ls-tree|write-tree' -Quiet)
    Assert-Equal "$($script.Name) computes no key of its own" $false $own
}

# Behaviour on a throw-away repo: a git failure throws, an unrelated file leaves the key alone, an input change gives a new key.
function Invoke-TempGit([string]$Repo, [string[]]$GitArguments) {
    & git -C $Repo -c user.name=t -c user.email=t@t -c commit.gpgsign=false @GitArguments 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "temp git $($GitArguments -join ' ') failed." }
}

function New-TempKeyRepo {
    $repo = Join-Path ([IO.Path]::GetTempPath()) ('rtst-keyrepo-' + [guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Force (Join-Path $repo 'src'), (Join-Path $repo 'docs')
    Set-Content (Join-Path $repo 'src\a.txt') 'one'
    Set-Content (Join-Path $repo 'docs\notes.md') 'one'
    Invoke-TempGit $repo @('init', '-q')
    Invoke-TempGit $repo @('add', '-A')
    Invoke-TempGit $repo @('commit', '-q', '-m', 'init')
    return $repo
}

function Test-KeyBehaviour {
    $realRoot = $script:ToolsRepoRoot
    $repo = New-TempKeyRepo
    try {
        $script:ToolsRepoRoot = $repo
        $base = Get-BuildKey
        Set-Content (Join-Path $repo 'docs\notes.md') 'changed'
        Assert-Equal 'an unrelated (docs) change keeps the key' $base (Get-BuildKey)
        Set-Content (Join-Path $repo 'src\a.txt') 'changed'
        $changed = Get-BuildKey
        Assert-Equal 'an input change gives a new key' $true ($changed -ne $base)
        Set-Content (Join-Path $repo 'src\b.txt') 'new untracked input'
        Assert-Equal 'an untracked input file gives a new key' $true ((Get-BuildKey) -ne $changed)
        Assert-Equal 'the recorded inputs are the ones the key was hashed from' $true (@($script:BuildKeyResults[(Get-BuildKey)].Inputs).Count -gt 0)
        Test-SimulatedGitFailureThrows
        Test-NotARepoThrows
    } finally {
        $script:ToolsRepoRoot = $realRoot
        Remove-Item $repo -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Test-SimulatedGitFailureThrows {
    $threw = $false
    function Invoke-Git([string[]]$GitArguments) { throw 'simulated git failure' }   # local to this function: shadows build-cache's only while it runs
    try { $null = Get-BuildKey } catch { $threw = $true }
    Assert-Equal 'a git failure makes Get-BuildKey throw' $true $threw
}

# Runs the real Invoke-Git (no shadow in scope), so a swallowed git failure would make this fail.
function Test-NotARepoThrows {
    $threw = $false
    $null = New-Item -ItemType Directory -Force ($notRepo = Join-Path ([IO.Path]::GetTempPath()) ('rtst-notrepo-' + [guid]::NewGuid().ToString('N')))
    $script:ToolsRepoRoot = $notRepo
    $message = ''
    try { $null = Get-BuildKey } catch { $threw = $true; $message = $_.Exception.Message }
    Remove-Item $notRepo -Recurse -Force -ErrorAction SilentlyContinue
    Assert-Equal 'a directory that is not a git repo makes Get-BuildKey throw' $true $threw
    Assert-Equal 'the throw is Invoke-Git reporting the git failure (not a later empty-input error)' $true ($message -match '^git .* failed\.$')
}
Test-KeyBehaviour

# Zero input lines (a tree with no input path) is an error, not the key of the empty string.
function Test-EmptyInputsThrow {
    $realRoot = $script:ToolsRepoRoot
    $repo = Join-Path ([IO.Path]::GetTempPath()) ('rtst-emptyrepo-' + [guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Force (Join-Path $repo 'docs')
    Set-Content (Join-Path $repo 'docs\notes.md') 'one'
    try {
        Invoke-TempGit $repo @('init', '-q'); Invoke-TempGit $repo @('add', '-A'); Invoke-TempGit $repo @('commit', '-q', '-m', 'init')
        $script:ToolsRepoRoot = $repo
        $threw = $false
        try { $null = Get-BuildKey } catch { $threw = $true }
        Assert-Equal 'a tree with no build input makes Get-BuildKey throw' $true $threw
    } finally {
        $script:ToolsRepoRoot = $realRoot
        Remove-Item $repo -Recurse -Force -ErrorAction SilentlyContinue
    }
}
Test-EmptyInputsThrow

$engine = (Get-Process -Id $PID).Path
$emptyRoot = Join-Path ([IO.Path]::GetTempPath()) ('rtst-buildkey-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Force $emptyRoot
$priorRoot = $env:RTST_BUILD_CACHE_LOOKUP_ROOT
$priorState = $env:RTST_EMULATOR_STATE_DIR
$env:RTST_BUILD_CACHE_LOOKUP_ROOT = $emptyRoot
$env:RTST_EMULATOR_STATE_DIR = Join-Path $emptyRoot 'state'   # no installed-key file, so run-flows looks in the cache
try {
    foreach ($app in @('dino', 'rtst')) {
        $installOut = (& $engine -NoProfile -File (Join-Path $PSScriptRoot 'install-debug.ps1') -App $app -NoBuild 2>&1 | Out-String)
        $installExit = $LASTEXITCODE
        $flowsOut = (& $engine -NoProfile -File (Join-Path $PSScriptRoot 'run-flows.ps1') -App $app -NoBuild -Flow no-such-flow 2>&1 | Out-String)
        $flowsExit = $LASTEXITCODE
        Assert-Equal "$app install-debug reached the cache-miss line (exit code)" 6 $installExit
        Assert-Equal "$app run-flows reached the cache-miss line (exit code)" 6 $flowsExit
        Assert-Equal "$app install-debug reached the cache-miss line" $true ($installOut -match 'cache missed for')
        Assert-Equal "$app run-flows reached the cache-miss line" $true ($flowsOut -match 'cache missed for')
        if ($installOut -notmatch 'cache missed for') { Write-Host "install-debug output:`n$installOut" }
        if ($flowsOut -notmatch 'cache missed for') { Write-Host "run-flows output:`n$flowsOut" }
        Assert-Equal "$app install-debug key equals Get-BuildKey" $expected (Get-KeyFromMissLine $installOut)
        Assert-Equal "$app run-flows key equals Get-BuildKey" $expected (Get-KeyFromMissLine $flowsOut)
        Assert-Equal "$app install-debug miss prints the key's inputs" $true ($installOut -match "Build key $expected is made of \(git tree [0-9a-f]{40}\)")
        Assert-Equal "$app run-flows miss prints the key's inputs" $true ($flowsOut -match "Build key $expected is made of \(git tree [0-9a-f]{40}\)")
    }
} finally {
    $env:RTST_BUILD_CACHE_LOOKUP_ROOT = $priorRoot
    $env:RTST_EMULATOR_STATE_DIR = $priorState
    Remove-Item $emptyRoot -Recurse -Force -ErrorAction SilentlyContinue
}

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-build-key: all checks passed.'
exit 0   # the child scripts above end with exit 6 (cache miss) on purpose; without this a CI wrapper that reads $LASTEXITCODE would fail the step
