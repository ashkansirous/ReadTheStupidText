<#
.SYNOPSIS
  Self-test for Slice 317 item 1: Select-AbandonedBuilds picks only the builds nobody used for 14 days (a gone worktree is no reason), ignores nulls,
  and an empty cache prunes nothing and does not throw. Pure logic, no build, no device. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
if (-not $env:ANDROID_HOME -and -not $env:ANDROID_SDK_ROOT) { $env:ANDROID_HOME = [IO.Path]::GetTempPath() }
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'build-cache.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

$now = Get-Date
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("rtst-prune-path-" + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Force $tmp
$gone = Join-Path $tmp 'removed-worktree'
$builds = @(
    [pscustomobject]@{ Name = 'live-new'; WorktreePath = $tmp; LastWriteTime = $now.AddDays(-1.5) },
    [pscustomobject]@{ Name = 'live-old'; WorktreePath = $tmp; LastWriteTime = $now.AddDays(-30) },
    [pscustomobject]@{ Name = 'gone-old-ish'; WorktreePath = $gone; LastWriteTime = $now.AddDays(-2) },
    [pscustomobject]@{ Name = 'gone-but-used-today'; WorktreePath = $gone; LastWriteTime = $now.AddHours(-1) },
    [pscustomobject]@{ Name = 'old-and-used-never-path'; WorktreePath = $null; LastWriteTime = $now.AddDays(-20) },
    [pscustomobject]@{ Name = 'no-path-recent'; WorktreePath = $null; LastWriteTime = $now.AddDays(-3) }
)
$picked = @(Select-AbandonedBuilds $builds $now | ForEach-Object { $_.Name })
Assert-Equal 'abandoned builds: only age counts, a gone worktree path does not' 'live-old,old-and-used-never-path' ($picked -join ',')
Assert-Equal 'an empty cache selects nothing' 0 @(Select-AbandonedBuilds @() $now).Count
Assert-Equal 'a null list selects nothing' 0 @(Select-AbandonedBuilds $null $now).Count
Assert-Equal 'a list holding one null selects nothing' 0 @(Select-AbandonedBuilds @($null) $now).Count

# The deleting path, on a temp cache folder.
$cache = Join-Path ([IO.Path]::GetTempPath()) ("rtst-prune-test-" + [guid]::NewGuid().ToString('N'))
function New-FakeBuild([string]$Name, [object]$MetaText, [int]$AgeDays) {
    $dir = Join-Path $cache $Name
    $null = New-Item -ItemType Directory -Force $dir
    if ($null -ne $MetaText) { Set-Content -LiteralPath (Join-Path $dir $script:BuildMetaFile) -Value $MetaText -Encoding ASCII }
    (Get-Item $dir).LastWriteTime = (Get-Date).AddDays(-$AgeDays)
}
function ConvertTo-MetaText([string]$Path) { return ('{"worktreePath":' + (ConvertTo-Json $Path) + '}') }
try {
    New-FakeBuild 'live-recent' (ConvertTo-MetaText $tmp) 1
    New-FakeBuild 'live-stale' (ConvertTo-MetaText $tmp) 30
    New-FakeBuild 'gone-worktree' (ConvertTo-MetaText $gone) 3
    New-FakeBuild 'gone-but-used-today' (ConvertTo-MetaText $gone) 0
    New-FakeBuild 'no-path-recent' '{"worktree":"s317"}' 3
    New-FakeBuild 'no-path-stale' '{"worktree":"s317"}' 30
    New-FakeBuild 'unreadable-meta' '{not json' 30
    New-FakeBuild 'pack-no-record' $null 30
    $removed = Remove-AbandonedBuilds $cache 6>$null
    $left = @(Get-ChildItem $cache -Directory | ForEach-Object { $_.Name } | Sort-Object)
    Assert-Equal 'deleting path: only the 30-day-old records go (a gone worktree path alone does not); the pack folder (no record) stays' 'gone-but-used-today,gone-worktree,live-recent,no-path-recent,pack-no-record' ($left -join ',')
    Assert-Equal 'it returns how many it removed' 3 $removed
    $empty = Join-Path $cache 'nothing-here'
    $null = New-Item -ItemType Directory -Force $empty
    Assert-Equal 'a cache with no build records removes nothing and does not throw' 0 (Remove-AbandonedBuilds $empty 6>$null)
}
finally {
    if (Test-Path $cache) { Remove-Item $cache -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
}

if ($script:failed) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-build-prune: all checks passed.'
exit 0
