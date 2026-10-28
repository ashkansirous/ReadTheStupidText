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
    [switch]$Raw
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
        '^tools/test-targets\.ps1$' { return ,@() }  # picks the apps; it is checked by running it, not by a device run
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
