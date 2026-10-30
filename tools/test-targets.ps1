<#
.SYNOPSIS
  Which apps a change needs device tests for (Slice 180): rtst-android, rtst-windows, dino-android, dino-windows.
.DESCRIPTION
  Takes the files changed since the merge base with -Base (committed, uncommitted and untracked; or -Files) and maps them with
  the same rules CI uses to decide what to build (.github/scripts/ship-targets.mjs --local), plus a rule for the test tooling
  itself: a change under tools/ or .maestro/ runs the apps whose checks it edits, and a tooling file it cannot place runs
  every app (failing safe means testing more, never less). iOS stays CI-only (no Mac here).
  Prints the runs and the skipped apps with the reason. -Raw writes only the app names (rtst, dino) of -Platform to the
  pipeline, for run-flows.ps1 and Invoke-DesktopTest.ps1 -App auto. A caller may add an app; it never drops one silently.
  Slice 207: -Checks prints the fast checks the same diff needs instead: one `dotnet build` line per app the code change names
  (Mobile.Shared always names both Android apps) and one `dotnet test` line per test project it reaches (Domain, Application and
  Mobile.Shared reach both; DinoReader.Core reaches tests/DinoReader.Tests; Windows-only projects reach tests/ReadTheStupidText.Tests).
  A change that cannot be read names everything. A tooling-only change prints no build or test line.
.EXAMPLE
  .\test-targets.ps1                       # all four, with reasons
  .\test-targets.ps1 -Platform android -Raw
  .\test-targets.ps1 -Files src/DinoReader.App/Views/ReaderPage.xaml
#>
param(
    [ValidateSet('android', 'windows', 'all')][string]$Platform = 'all',
    [string]$Base = 'origin/main',
    [string[]]$Files,
    # The checkout to read the changes of; default: the checkout this script lives in (never the caller's current directory).
    [string]$RepoRoot,
    [switch]$Raw,
    [switch]$Checks
)
$ErrorActionPreference = 'Stop'
$repoRoot = if ($RepoRoot) { (Resolve-Path $RepoRoot).Path } else { (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }
$shipTargets = Join-Path $repoRoot '.github\scripts\ship-targets.mjs'
$unknown = '*'

$allRuns = @('rtst-android', 'rtst-windows', 'dino-android', 'dino-windows')
# Tooling that touches no device check (story builders and uploads): it never needs a device run.
$noDevicePrefixes = @('tools/DinoReader.BookBuilder/', 'tools/upload-store-story.ps1')

function Get-ChangedFiles {
    if ($Files) { return @($Files) }
    $out = & node $shipTargets --local --files --base $Base --repo-root $repoRoot
    # Unknown only on a node/git failure or its `*` answer; a clean tree is an empty list, not "unknown".
    if ($LASTEXITCODE -ne 0 -or @($out) -contains $unknown) { return @($unknown) }
    return @($out | Where-Object { $_ })
}

# The CI flags (windows, android, dinoapp, dinowin) for these files, as device runs.
function Get-ShipRuns([string[]]$Changed) {
    $flags = @{}
    $lines = @($Changed | & node $shipTargets --local --stdin)
    # A node failure or an answer with no flags must mean "test more", never "no apps".
    if ($LASTEXITCODE -ne 0 -or $lines.Count -eq 0) { return @($script:allRuns) }
    $lines | ForEach-Object { $k, $v = $_ -split '=', 2; $flags[$k] = ($v -eq 'true') }
    $runs = @()
    if ($flags['android']) { $runs += 'rtst-android' }
    if ($flags['windows']) { $runs += 'rtst-windows' }
    if ($flags['dinoapp']) { $runs += 'dino-android' }
    if ($flags['dinowin']) { $runs += 'dino-windows' }
    return $runs
}

# The apps a test-tooling file affects: an empty array means none (",@()" keeps it from collapsing to $null); $null means "cannot place it".
function Get-ToolRuns([string]$File) {
    $android = @('rtst-android', 'dino-android')
    $windows = @('rtst-windows', 'dino-windows')
    switch -Regex ($File) {
        '\.md$' { return ,@() }
        '^tools/emulator/(build-cache|activity)\.ps1$' { return $allRuns }  # dot-sourced by the Windows VM scripts too
        '^tools/(test-targets|test-test-targets)\.ps1$' { return ,@() }  # picks the apps; it is checked by running it, not by a device run
        '^tools/lease-common\.ps1$' { return $android }  # dot-sourced by the emulator and phone scripts only
        '^\.maestro/rtst/' { return @('rtst-android') }
        '^\.maestro/dino/' { return @('dino-android') }
        '^\.maestro/' { return $android }
        '^tools/desktop-vm/checks/rtst-' { return @('rtst-windows') }
        '^tools/desktop-vm/checks/dino-' { return @('dino-windows') }
        '^tools/desktop-vm/' { return $windows }
        '^tools/(emulator|phone)/' { return $android }
        default { }
    }
    if ($noDevicePrefixes | Where-Object { $File.StartsWith($_) }) { return ,@() }
    return $null
}

function Get-DeviceRuns([string[]]$Changed) {
    if ($Changed -contains $unknown) { return @{ Runs = $allRuns; Why = @{ all = 'the changed files could not be read (no merge base, or git/node failed), so everything runs' } } }
    if ($Changed.Count -eq 0) { return @{ Runs = @(); Why = @{ none = 'no changed files since the merge base' } } }
    $runs = @(Get-ShipRuns $Changed)
    $why = @{}
    foreach ($file in ($Changed | Where-Object { $_ -match '^(tools|\.maestro)/' })) {
        $toolRuns = Get-ToolRuns $file
        if ($null -eq $toolRuns) { $runs = $allRuns; $why['all'] = "$file is test tooling this script cannot place, so everything runs"; continue }
        $runs += $toolRuns
    }
    return @{ Runs = @($allRuns | Where-Object { $runs -contains $_ }); Why = $why }
}

function Select-Platform([string[]]$Runs) {
    if ($Platform -eq 'all') { return $Runs }
    return @($Runs | Where-Object { $_ -like "*-$Platform" })
}

# ---- Slice 207: -Checks, the fast checks (builds and unit tests) a change needs.
$bothTests = @('ReadTheStupidText.Tests', 'DinoReader.Tests')
# First match wins. A path reaches the test projects named here; builds come from the CI rules (Get-ShipRuns) plus Mobile.Shared below.
$testRules = @(
    @{ Pattern = '^src/ReadTheStupidText\.(Mobile\.Shared|Application|Domain|Infrastructure|Documents)/'; Tests = $bothTests }  # Dino references Infrastructure and Documents too
    @{ Pattern = '^src/ReadTheStupidText\.(App|Windows\.Tray|Mobile)/'; Tests = @('ReadTheStupidText.Tests') }
    @{ Pattern = '^(src/DinoReader\.(Core|App)|tools/DinoReader\.BookBuilder|content/dino)/'; Tests = @('DinoReader.Tests') }
    @{ Pattern = '^tests/ReadTheStupidText\.Tests/'; Tests = @('ReadTheStupidText.Tests') }
    @{ Pattern = '^tests/DinoReader\.Tests/'; Tests = @('DinoReader.Tests') }
    # Shared build inputs reach every project, so both test projects run (the same fail-safe as an unknown file).
    @{ Pattern = '^(Directory\.(Build|Packages)\.(props|targets)|global\.json|nuget\.config|[^/]+\.slnx)$'; Tests = $bothTests }
)
$buildCommands = [ordered]@{
    'rtst-android' = 'dotnet build src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj -f net10.0-android'
    'dino-android' = 'dotnet build src/DinoReader.App/DinoReader.App.csproj -f net10.0-android'
    'rtst-windows' = 'dotnet build src/ReadTheStupidText.App/ReadTheStupidText.App.csproj -p:Platform=x64'
    'dino-windows' = 'dotnet build src/DinoReader.App/DinoReader.App.csproj -f net10.0-windows10.0.19041.0 -p:IncludeAndroid=false -p:Platform=x64'
}

function Get-FastCheckTests([string[]]$Changed) {
    if ($Changed -contains $unknown) { return $bothTests }
    $tests = foreach ($file in $Changed) { foreach ($rule in $testRules) { if ($file -match $rule.Pattern) { $rule.Tests; break } } }
    return @($bothTests | Where-Object { $tests -contains $_ })
}

function Get-FastCheckBuilds([string[]]$Changed) {
    if ($Changed -contains $unknown) { return @($buildCommands.Keys) }
    $code = @($Changed | Where-Object { $_ -notmatch '^(tools|\.maestro)/' })
    if ($code.Count -eq 0) { return @() }
    $runs = @(Get-ShipRuns $code)
    if ($code | Where-Object { $_ -match '^src/ReadTheStupidText\.Mobile\.Shared/' }) { $runs += 'rtst-android', 'dino-android' }
    return @($buildCommands.Keys | Where-Object { $runs -contains $_ })
}

function Write-FastChecks([string[]]$Changed) {
    $builds = @(Get-FastCheckBuilds $Changed)
    $tests = @(Get-FastCheckTests $Changed)
    foreach ($app in $builds) { Write-Output $buildCommands[$app] }
    foreach ($project in $tests) { Write-Output "dotnet test tests/$project/$project.csproj" }
    if ($builds.Count + $tests.Count -eq 0) { Write-Host 'No build or unit-test project is touched by this change (test tooling and docs only).' }
    elseif ($builds | Where-Object { $_ -like '*-android' }) { Write-Host 'note: where only the maui-android workload is installed, add -p:IncludeWindows=false to the Android builds.' }
}

if ($Checks) { Write-FastChecks (Get-ChangedFiles); return }

$result = Get-DeviceRuns (Get-ChangedFiles)
$selected = @(Select-Platform $result.Runs)
$candidates = @(Select-Platform $allRuns)
if ($Raw) {
    $skipped = @($candidates | Where-Object { $selected -notcontains $_ })
    if ($skipped.Count -gt 0) { Write-Host "device tests skipped (the change does not touch them): $($skipped -join ', ')" }
    if ($selected.Count -eq 0) { Write-Host "NO $Platform APP IS TOUCHED BY THIS CHANGE: no device test will run. That is not a test pass, it is a skip." }
    $selected | ForEach-Object { ($_ -split '-')[0] } | Select-Object -Unique
    return
}
Write-Host ("device runs: " + $(if ($selected.Count) { $selected -join ', ' } else { '(none)' }))
if ($selected.Count -eq 0) { Write-Host "NO APP IS TOUCHED BY THIS CHANGE: no device test will run. That is not a test pass, it is a skip." }
foreach ($skip in ($candidates | Where-Object { $selected -notcontains $_ })) { Write-Host "skipped: $skip (no changed file maps to it)" }
foreach ($note in $result.Why.Values) { Write-Host "note: $note" }
