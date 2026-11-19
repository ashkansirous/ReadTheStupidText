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

# Slice 393 item 14: the status bar of a store shot is the same in every shot (the Play phone set had a download notification, a shield icon and a clock that moved from 8:54 to 8:55). The system UI's demo mode shows a fixed clock, full battery
# and signal and no notification icons. Pure: the adb argument lists that switch it on / off, in order (the broadcast is `am broadcast -a com.android.systemui.demo -e command <name> ...`).
$script:DemoClock = '1000'
function New-DemoBroadcast([string[]]$Extras) { return @('shell', 'am', 'broadcast', '-a', 'com.android.systemui.demo') + $Extras }
function Get-StatusBarDemoEnterCommands([string]$Clock = $script:DemoClock) {
    return @(
        , @('shell', 'settings', 'put', 'global', 'sysui_demo_allowed', '1')
        , (New-DemoBroadcast @('-e', 'command', 'enter'))
        , (New-DemoBroadcast @('-e', 'command', 'clock', '-e', 'hhmm', $Clock))
        , (New-DemoBroadcast @('-e', 'command', 'battery', '-e', 'level', '100', '-e', 'plugged', 'false'))
        , (New-DemoBroadcast @('-e', 'command', 'network', '-e', 'wifi', 'show', '-e', 'level', '4', '-e', 'fully', 'true'))
        , (New-DemoBroadcast @('-e', 'command', 'network', '-e', 'mobile', 'show', '-e', 'datatype', 'none', '-e', 'level', '4', '-e', 'fully', 'true'))
        , (New-DemoBroadcast @('-e', 'command', 'notifications', '-e', 'visible', 'false'))
    )
}
function Get-StatusBarDemoExitCommands {
    return @(
        , (New-DemoBroadcast @('-e', 'command', 'exit'))
        , @('shell', 'settings', 'put', 'global', 'sysui_demo_allowed', '0')
    )
}

# Runs the commands through the emulator's adb (the serial common.ps1 holds for the chosen instance). Never throws (each call is caught); returns the failed commands' messages (an empty list when every call went through).
# No setting read-back proves the demo mode (sysui_demo_allowed is persistent and reads 1 whether or not the broadcasts took): the proof is the first screenshot's status bar, checked in the device run.
function Invoke-StatusBarDemo([object[]]$Commands, [scriptblock]$AdbCall = { param($a) Invoke-EmulatorAdbText $a }) {
    $failures = @()
    foreach ($command in $Commands) {
        try { $null = & $AdbCall $command } catch { $failures += "'$($command -join ' ')' failed: $($_.Exception.Message)" }
    }
    return @($failures)
}

# A snapshot or cold boot (run-flows.ps1 does one on a cache miss or a failed preflight) drops the demo mode, so it is applied only once the emulator is up: starts it first when it is not.
function Confirm-EmulatorForDemo([int]$InstanceNumber) {
    if (Test-EmulatorOnline) { return }
    Write-Host "STORE SHOTS: $script:Serial is not up; starting it before the status bar demo mode."
    & (Join-Path $PSScriptRoot 'start-warm.ps1') -Instance $InstanceNumber | Out-Null
}

# Slice 404: the Dino full-shelf hook imports a PAID book from a package file, because the store refuses a paid download without a real receipt (no fake receipt, no live config). Decision 5: a package is never built here;
# .github/scripts/fetch-shelf-packages.mjs reads the one the store upload already stored (dinoreader/stories/<id>/story-v<n>.dino in the downloads bucket, through wrangler: a login on this PC or CLOUDFLARE_API_TOKEN and
# CLOUDFLARE_ACCOUNT_ID) into artifacts/full-shelf/<id>/story-v<n>.dino, and this copies the newest package of every book into the app's data folder, files/full-shelf/<id>.dino
# (the hook's FullShelfPlan.PackageFolder). The flow's first launch must not clear the app's data (it would delete them): the script clears it once, before the copy.
$script:FullShelfDeviceTemp = '/data/local/tmp/full-shelf'
$script:FullShelfAppFolder = 'files/full-shelf'

# Pure: the newest package of each book under $StoreRoot as @{ Id; Path } (the highest story-v<n>.dino of each <id> folder); empty when the root or a package is missing.
function Get-FullShelfPackages([string]$StoreRoot) {
    if (-not (Test-Path $StoreRoot)) { return @() }
    $books = foreach ($folder in Get-ChildItem $StoreRoot -Directory) {
        $newest = Get-ChildItem $folder.FullName -Filter 'story-v*.dino' -File |
            Where-Object { $_.BaseName -match '^story-v(\d+)$' } |
            Sort-Object { [int]($_.BaseName -replace '^story-v', '') } -Descending | Select-Object -First 1
        if ($newest) { [pscustomobject]@{ Id = $folder.Name; Path = $newest.FullName } }
    }
    return @($books)
}

# Pure: the node argument list that fetches the stored packages (the same script the iOS job runs) into $Folder.
function Get-FetchShelfPackagesArguments([string]$RepoRoot, [string]$Folder) {
    return @((Join-Path $RepoRoot '.github\scripts\fetch-shelf-packages.mjs'), $Folder)
}

# Fetches the packages the store upload already stored (never builds one). Returns the failure text, or an empty string when the script exited 0. $Run takes the node argument list and returns the exit code (a seam for the self-test).
function Invoke-FetchShelfPackages([string]$RepoRoot, [string]$Folder, [scriptblock]$Run = { param($a) & node @a | Out-Host; $LASTEXITCODE }) {
    $code = & $Run (Get-FetchShelfPackagesArguments $RepoRoot $Folder)
    if ($code -ne 0) { return "fetch-shelf-packages.mjs exited $code (it reads dinoreader/stories/<id>/story-v<n>.dino from the downloads bucket: run npm ci in the repo and be logged in to wrangler, or set CLOUDFLARE_API_TOKEN and CLOUDFLARE_ACCOUNT_ID)" }
    return ''
}

# Pure: the adb argument lists that clear the app's data, copy each package in (through /data/local/tmp and run-as, the debug app's own files folder) and remove the temp folder, in order.
function Get-FullShelfPushCommands([string]$PackageName, [object[]]$Books) {
    $commands = @(
        , @('shell', 'pm', 'clear', $PackageName)
        , @('shell', 'rm', '-rf', $script:FullShelfDeviceTemp)
        , @('shell', 'mkdir', '-p', $script:FullShelfDeviceTemp)
        , @('shell', 'run-as', $PackageName, 'mkdir', '-p', $script:FullShelfAppFolder)
    )
    foreach ($book in $Books) {
        $commands += , @('push', $book.Path, "$($script:FullShelfDeviceTemp)/$($book.Id).dino")
        $commands += , @('shell', 'run-as', $PackageName, 'cp', "$($script:FullShelfDeviceTemp)/$($book.Id).dino", "$($script:FullShelfAppFolder)/$($book.Id).dino")
    }
    $commands += , @('shell', 'rm', '-rf', $script:FullShelfDeviceTemp)
    return $commands
}

# Returns the problem text when the debug app is not installed on the emulator (checked before `pm clear` and `run-as` touch it), or an empty string when `pm list packages` lists it exactly.
function Get-AppNotInstalledProblem([string]$PackageName, [scriptblock]$AdbCall = { param($a) Invoke-EmulatorAdbText $a }) {
    try { $listed = [string](& $AdbCall @('shell', 'pm', 'list', 'packages', $PackageName)) } catch { return "could not ask the emulator whether $PackageName is installed: $($_.Exception.Message)" }
    if ($listed -notmatch "(?im)^package:$([regex]::Escape($PackageName))\s*$") { return "$PackageName is not installed on the emulator (install the Debug build first: tools/emulator/install-debug.ps1); a Release build cannot take the packages (run-as needs a debuggable app)" }
    return ''
}

# Runs the commands in order through the emulator's adb; stops at the first one that fails and returns its message (an empty string when all went through). `run-as` and `push` print their failure, so the text is checked too.
function Invoke-FullShelfPush([object[]]$Commands, [scriptblock]$AdbCall = { param($a) Invoke-EmulatorAdbText $a }) {
    foreach ($command in $Commands) {
        try { $out = [string](& $AdbCall $command) } catch { return "'$($command -join ' ')' failed: $($_.Exception.Message)" }
        if ($out -imatch 'error:|No such file|not debuggable|Permission denied|is unknown|failed|failure|not installed|unknown package|exception') { return "'$($command -join ' ')' failed: $($out.Trim())" }
    }
    return ''
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
Set-EmulatorInstance $instance
try { Confirm-EmulatorForDemo ([int]$instance) } catch { Write-Host "STORE SHOTS: could not start $script:Serial first: $($_.Exception.Message)" }
if ((Get-Content $flowFile -Raw) -match '(?m)^\s*cmd:\s*full-shelf\s*$') {
    $shelfFolder = Join-Path $repoRoot 'artifacts\full-shelf'
    $fetchFailure = Invoke-FetchShelfPackages $repoRoot $shelfFolder
    if ($fetchFailure) { Write-Host "STORE SHOTS: $flowName uses the full-shelf hook but the stored book packages could not be fetched, no flow was run: $fetchFailure"; exit 1 }
    $packages = @(Get-FullShelfPackages $shelfFolder)
    if ($packages.Count -eq 0) { Write-Host "STORE SHOTS: $flowName uses the full-shelf hook but $shelfFolder holds no package after the fetch."; exit 1 }
    $appPackage = [regex]::Match((Get-Content $flowFile -Raw), '(?m)^appId:\s*(\S+)').Groups[1].Value
    $notInstalled = Get-AppNotInstalledProblem $appPackage
    if ($notInstalled) { Write-Host "STORE SHOTS: $notInstalled; no flow was run."; exit 1 }
    $pushFailure = Invoke-FullShelfPush (Get-FullShelfPushCommands $appPackage $packages)
    if ($pushFailure) { Write-Host "STORE SHOTS: could not put the book packages on $script:Serial, no flow was run: $pushFailure"; exit 1 }
    Write-Host "STORE SHOTS: app data cleared and $($packages.Count) book package(s) copied to $appPackage files/full-shelf on $script:Serial."
}
$demoFailures = @(Invoke-StatusBarDemo (Get-StatusBarDemoEnterCommands))
if ($demoFailures.Count -gt 0) {
    $null = Invoke-StatusBarDemo (Get-StatusBarDemoExitCommands)
    Write-Host "STORE SHOTS: the status bar demo mode commands failed on $script:Serial, no flow was run: $($demoFailures -join '; ')"
    exit 1
}
Write-Host "STORE SHOTS: status bar demo mode requested ($script:DemoClock, full battery and signal, no notification icons) on $script:Serial; the first screenshot's status bar is the proof."
try {
    & (Join-Path $PSScriptRoot 'run-flows.ps1') -App $App -Instance $instance -Flow $flowName -NoBuild
    $runExit = $LASTEXITCODE
} finally {
    $null = Invoke-StatusBarDemo (Get-StatusBarDemoExitCommands)   # a flow that failed or was stopped must not leave the emulator in demo mode
}
if ($runExit -ne 0) { Write-Host "STORE SHOTS: run-flows.ps1 exited $runExit; no shot was copied."; exit $(if ($runExit -eq 6) { 6 } else { 1 }) }
$missing = @(Copy-StoreShots $names $roots $outDir)
if ($missing.Count -gt 0) { Write-Host "STORE SHOTS: missing $($missing -join ', ') (searched $($roots -join ', '))."; exit 1 }
Write-Host "STORE SHOTS: $($names.Count) PNG(s) written to $outDir"
exit 0
