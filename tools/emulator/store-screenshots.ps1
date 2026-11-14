<#
.SYNOPSIS
  Slice 291 item 5 (Android part): takes the Play store screenshots of one app on one emulator with the scripted flows .maestro/<app>/store-shot-<form>.yaml
  and writes them to docs/store-listings/screenshots/<app>/<form>/<shot>.png. -Serial must match -Form (the tablet AVD for tablet, a phone AVD for phone).
.DESCRIPTION
  The flows live in .maestro/<app>/needs-file/ (run by name only, so a normal run-flows.ps1 run skips them); the shots a flow takes are the `- takeScreenshot: store-<app>-<form>-<n>-<screen>` lines of that flow, in order (the flow lint exempts store-shot-* flows from screenshot-frames.json).
  The emulator is the one run-flows.ps1 drives: -Serial emulator-5554 is instance 1 (a phone AVD), emulator-5558 is instance 3 (the tablet AVD RTST_Tablet); without -Serial the form picks it
  (phone -> 1, tablet -> 3). The debug app must be installed already (run-flows.ps1 -NoBuild exits 6 on a build cache miss, and so does this script). Runs headless: it starts no window and takes
  over nothing. Maestro writes each PNG under the Java user.home that run-flows.ps1 sets; this script copies the newest one written after the start of the run (contact-sheet.ps1's finder).
  -DryRun prints the plan (flow file, emulator instance, shot names, output folder) and runs nothing. Exit 0 when every shot was written, 1 when the flow failed or a shot is missing, 6 on a build cache miss.
.EXAMPLE
  .\store-screenshots.ps1 -App dino -Form phone
  .\store-screenshots.ps1 -App rtst -Form tablet -Serial emulator-5558
  .\store-screenshots.ps1 -App dino -Form phone -DryRun
#>
param(
    [ValidateSet('rtst', 'dino')][string]$App,     # required, checked after the dot-source guard (a Mandatory parameter would fail the dot-source of the self-test)
    [ValidateSet('phone', 'tablet')][string]$Form, # required, same
    [string]$Serial,
    [string]$OutRoot,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
# common.ps1 sets $script:Serial (the warm emulator's serial), which is this script's -Serial parameter variable: keep the asked-for value first.
$requestedSerial = $Serial
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'contact-sheet.ps1')   # dot-sourced: Get-MaestroToolsDir only
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $OutRoot) { $OutRoot = Join-Path $repoRoot 'docs\store-listings\screenshots' }

# Pure: the emulator instance (as run-flows.ps1 -Instance takes it) for a serial `emulator-<port>`, else the form's default. Throws on a serial that is not one of the AVD ports.
function Get-StoreShotInstance([string]$Serial, [string]$Form) {
    if (-not $Serial) { return $(if ($Form -eq 'tablet') { [string]$script:TabletInstance } else { '1' }) }
    if ($Serial -notmatch '^emulator-(\d+)$') { throw "Serial '$Serial' is not an emulator serial (emulator-<port>); real phones are not used for store shots." }
    $n = ([int]$Matches[1] - $script:InstanceBasePort) / 2 + 1
    if ($n -lt 1 -or $n -gt $script:TabletInstance -or $n -ne [math]::Floor($n)) { throw "Serial '$Serial' is not one of the AVD ports (instance 1 to $($script:TabletInstance))." }
    $isTabletAvd = ([int]$n -eq $script:TabletInstance)
    if (($Form -eq 'tablet') -ne $isTabletAvd) { throw "Serial '$Serial' is the $(if ($isTabletAvd) { 'tablet' } else { 'phone' }) AVD but -Form is ${Form}: the PNGs would land in the wrong Play folder." }
    return [string][int]$n
}

# Pure: the shot names a flow's text takes for this app and form, in file order; empty when it takes none.
function Get-StoreShotNames([string]$FlowText, [string]$App, [string]$Form) {
    $pattern = '(?m)^\s*-\s+takeScreenshot:\s*(store-' + [regex]::Escape($App) + '-' + [regex]::Escape($Form) + '-\d+-[A-Za-z0-9-]+)\s*$'
    return @([regex]::Matches($FlowText, $pattern) | ForEach-Object { $_.Groups[1].Value })
}

# Pure: the pixel width and height in a PNG's IHDR chunk, or $null when the file is not a PNG. Reads 24 bytes only.
function Get-PngSize([string]$Path) {
    $head = New-Object byte[] 24
    $stream = [IO.File]::OpenRead($Path)
    try { $read = $stream.Read($head, 0, 24) } finally { $stream.Dispose() }
    $signature = @(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)
    if ($read -lt 24) { return $null }
    for ($i = 0; $i -lt 8; $i++) { if ($head[$i] -ne $signature[$i]) { return $null } }
    $w = ([int]$head[16] -shl 24) -bor ([int]$head[17] -shl 16) -bor ([int]$head[18] -shl 8) -bor [int]$head[19]
    $h = ([int]$head[20] -shl 24) -bor ([int]$head[21] -shl 16) -bor ([int]$head[22] -shl 8) -bor [int]$head[23]
    return [pscustomobject]@{ Width = $w; Height = $h }
}

# Pure: the newest file named exactly "$Name.png" under the roots, that is a PNG with a non-zero pixel size; $null when there is none (a similarly named file is never used; old ones are deleted before the run).
function Find-StoreShotFile([string[]]$Roots, [string]$Name) {
    $hits = foreach ($root in $Roots) {
        if (-not (Test-Path $root)) { continue }
        Get-ChildItem -Path $root -Filter "$Name.png" -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq "$Name.png" }
    }
    foreach ($file in @($hits | Sort-Object LastWriteTime -Descending)) {
        $size = Get-PngSize $file.FullName
        if ($size -and $size.Width -gt 0 -and $size.Height -gt 0) { return $file }
    }
    return $null
}

# Deletes every old PNG named exactly after a shot under the roots, so only the run about to start can leave one (no clock or file time is trusted).
function Remove-OldStoreShots([string[]]$Names, [string[]]$Roots) {
    foreach ($root in $Roots) {
        if (-not (Test-Path $root)) { continue }
        foreach ($name in $Names) {
            Get-ChildItem -Path $root -Filter "$name.png" -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq "$name.png" } | Remove-Item -Force
        }
    }
}

# Pure: copies the newest valid PNG named exactly after each shot into $OutDir; returns the names that were not found.
function Copy-StoreShots([string[]]$Names, [string[]]$Roots, [string]$OutDir) {
    $null = New-Item -ItemType Directory -Force $OutDir
    $missing = @()
    foreach ($name in $Names) {
        $file = Find-StoreShotFile $Roots $name
        if ($file) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $OutDir "$name.png") -Force } else { $missing += $name }
    }
    return $missing
}

if ($MyInvocation.InvocationName -eq '.') { return }   # dot-sourced by a self-test: functions only

if (-not $App -or -not $Form) { Write-Host 'STORE SHOTS: -App and -Form are required.'; exit 1 }
$flowName = "store-shot-$Form"
# The flows sit in needs-file/ so a normal run-flows.ps1 run (no -Flow) skips them; -Flow <name> finds them there.
$flowFile = Join-Path $repoRoot ".maestro\$App\needs-file\$flowName.yaml"
if (-not (Test-Path $flowFile)) { Write-Host "STORE SHOTS: no flow $flowFile"; exit 1 }
$names = Get-StoreShotNames (Get-Content $flowFile -Raw) $App $Form
if ($names.Count -eq 0) { Write-Host "STORE SHOTS: $flowName takes no store-$App-$Form-<n>-<screen> shot"; exit 1 }
$instance = Get-StoreShotInstance $requestedSerial $Form
$outDir = Join-Path $OutRoot "$App\$Form"
if ($DryRun) {
    Write-Host "STORE SHOTS dry run: flow $flowName on emulator instance $instance, writes $($names.Count) PNG(s) to $outDir"
    $names | ForEach-Object { Write-Host "  $_.png" }
    exit 0
}

$toolsDir = Get-MaestroToolsDir $repoRoot $env:MAESTRO_HOME
$roots = @((Join-Path $toolsDir "maestro-home\i$instance"), (Join-Path $toolsDir "maestro-out\i$instance"))   # this emulator's folders only: another instance's run of the same flow must not be picked up
Remove-OldStoreShots $names $roots
& (Join-Path $PSScriptRoot 'run-flows.ps1') -App $App -Instance $instance -Flow $flowName -NoBuild
$runExit = $LASTEXITCODE
if ($runExit -ne 0) { Write-Host "STORE SHOTS: run-flows.ps1 exited $runExit; no shot was copied."; exit $(if ($runExit -eq 6) { 6 } else { 1 }) }
$missing = @(Copy-StoreShots $names $roots $outDir)
if ($missing.Count -gt 0) { Write-Host "STORE SHOTS: missing $($missing -join ', ') (searched $($roots -join ', '))."; exit 1 }
Write-Host "STORE SHOTS: $($names.Count) PNG(s) written to $outDir"
exit 0
