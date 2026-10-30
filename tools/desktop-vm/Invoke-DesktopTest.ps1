<#
.SYNOPSIS
  Desktop acceptance run in the headless test VM (Slice 163): restore 'clean', install the dev MSIX, run the checks,
  restore 'clean' again. Nothing is installed on, or shown by, the host.
.EXAMPLE
  .\Invoke-DesktopTest.ps1 -App rtst
  .\Invoke-DesktopTest.ps1 -App dino -Package D:\path\DinoReader.App_1.0.0.0_x64.msix
  .\Invoke-DesktopTest.ps1 -App auto      # only the apps the change touches (tools/test-targets.ps1), skipped ones are listed
.NOTES
  Slice 180: without -Package the MSIX is the one built for the current build key (Build-TestPackage.ps1; built first, outside
  the VM lock, when no build with this key exists), never "the newest file", so a stale package is not tested by accident.
  Exit code 0 when every check passes, 1 otherwise. -Sabotage KillApp stops the app after its launch check, so the
  remaining checks must fail (proof that a check can fail). Checks run in order and share the one running app.
  Slice 194 (opt-in): -Check <name> runs the app's first (launch) check, then only that check (a file in checks\, even one
  not in the default list); -CopyOut <host folder> copies the guest's results\*.png there before the VM is restored.
  Slice 207 (preflight): a check script may carry a header line `# needs: network, voice-pack`. The guest's network (an HTTPS request to the site, 10 s)
  is tested once the guest answers, before the package is copied, and only when a selected check declares `
  network` (the VM is offline by design, so declare it only for a check that truly needs the internet); the app's voice pack folder, which exists only once the package is installed,
  is tested right after the install and before the first check runs. Every declared probe runs before the decision; all missing items print in one `VM PREFLIGHT NO-GO: <items>` line and exits 3 without running any check. -SkipPreflight bypasses it.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('rtst', 'dino', 'auto')] [string] $App,
    [string] $Package,
    [string] $VmRoot = 'D:\ReadTheStupidText-worktrees\_desktop-vm',
    [ValidateSet('None', 'KillApp')] [string] $Sabotage = 'None',
    [switch] $KeepRunning,
    [string] $Check,
    [string] $CopyOut,
    [switch] $SkipPreflight
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"
$script:ActiveVmRoot = $VmRoot
. "$PSScriptRoot\..\emulator\build-cache.ps1"

# -App auto: one run per touched app, each through this same script, so a failing app does not hide the next one.
if ($App -eq 'auto') {
    if ($Package) { throw '-Package names one MSIX; it cannot be combined with -App auto.' }
    if ($Check -or $CopyOut) { throw '-Check and -CopyOut belong to one app; name it with -App rtst or -App dino, not -App auto.' }
    $touched = @(& "$PSScriptRoot\..\test-targets.ps1" -Platform windows -Raw)
    if ($touched.Count -eq 0) { Write-Host 'NO WINDOWS APP IS TOUCHED BY THIS CHANGE: no desktop test ran. That is a skip, not a test pass.'; exit 0 }
    $worst = 0
    foreach ($name in $touched) {
        & $PSCommandPath -App $name -VmRoot $VmRoot -Sabotage $Sabotage -KeepRunning:$KeepRunning -SkipPreflight:$SkipPreflight
        # A real failed check (1) outranks a NO-GO (3) from another app, whatever the order, so a caller that reads 3 as "environment, ask" never misses a failure.
        if ($LASTEXITCODE -eq $script:PreflightExitCode) { if ($worst -eq 0) { $worst = $script:PreflightExitCode } }
        elseif ($LASTEXITCODE -ne 0) { $worst = 1 }
    }
    exit $worst
}

# One row per product: the package identity name, where a fresh MSIX is usually found and the ordered checks.
$Apps = @{
    rtst = @{ Name = 'AshkanSirous.ReadTheStupidText'; Exe = 'ReadTheStupidText.App'; Checks = @('rtst-launch', 'rtst-tray-icon', 'rtst-hotkey-read', 'rtst-selection-hotkey', 'rtst-panel-speed', 'rtst-startup-task') }
    dino = @{ Name = 'AshkanSirous.DinoReader'; Exe = 'DinoReader.App'; Checks = @('dino-launch', 'dino-open-book', 'dino-page-turn', 'dino-settings') }
}
$AppProfile = $Apps[$App]
if ($Check) {
    if ($Check -notmatch "^$App-[a-z0-9-]+$") { throw "-Check ${Check}: the name must start with '$App-' (a check of this app) and use only lower-case letters, digits and dashes." }
    if (-not (Test-Path "$PSScriptRoot\checks\$Check.ps1")) { throw "-Check ${Check}: no such file in tools\desktop-vm\checks." }
    $AppProfile.Checks = @($AppProfile.Checks[0]) + @($Check | Where-Object { $_ -ne $AppProfile.Checks[0] })
}

function Find-Package {
    if ($Package) { return (Resolve-Path $Package).Path }
    $pattern = if ($App -eq 'rtst') { 'ReadTheStupidText.App_*.msix' } else { 'DinoReader.App_*.msix' }
    $cached = Find-CachedBuild (Get-BuildDir $App 'windows' (Get-BuildKey)) $pattern
    if ($cached) { Write-Host "Using the cached $App MSIX for this build key: $($cached.FullName)"; return $cached.FullName }
    Write-Host "No $App MSIX built for this build key; building it (outside the VM lock)..."
    $built = @(& "$PSScriptRoot\Build-TestPackage.ps1" -App $App) | Select-Object -Last 1
    if (-not $built -or -not (Test-Path $built)) { throw "Build-TestPackage.ps1 -App $App did not return a package path." }
    return $built
}

function Copy-ToGuest {
    param($Session, [string] $Msix)
    Invoke-Command -Session $Session -ScriptBlock {
        param($dir)
        Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Force -Path "$dir\pkg\deps", "$dir\checks", "$dir\results" | Out-Null
    } -ArgumentList $script:GuestWorkDir
    Write-Host 'Signing a copy of the package with a throw-away certificate...'
    $signDir = Join-Path $VmRoot 'sign'
    try {
        $signed = New-SignedCopy -Msix $Msix -WorkDir $signDir
        Write-Host 'Copying the package and checks into the VM...'
        Copy-Item -ToSession $Session -Path $signed.Msix -Destination "$($script:GuestWorkDir)\pkg\app.msix"
        Copy-Item -ToSession $Session -Path $signed.Cer -Destination "$($script:GuestWorkDir)\pkg\signer.cer"
    } finally { Remove-Item $signDir -Recurse -Force -ErrorAction SilentlyContinue }
    $deps = Join-Path (Split-Path $Msix) 'Dependencies\x64'
    if (Test-Path $deps) { Copy-Item -ToSession $Session -Path "$deps\*.msix" -Destination "$($script:GuestWorkDir)\pkg\deps\" }
    Copy-Item -ToSession $Session -Path "$PSScriptRoot\checks\*" -Destination "$($script:GuestWorkDir)\checks\" -Recurse
}

function Install-InGuest {
    param($Session, [string] $Name)
    return Invoke-Command -Session $Session -ArgumentList $script:GuestWorkDir, $Name -ScriptBlock {
        param($dir, $name)
        $deps = @(Get-ChildItem "$dir\pkg\deps" -Filter *.msix | ForEach-Object FullName)
        Import-Certificate -FilePath "$dir\pkg\signer.cer" -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
        $install = @{ Path = "$dir\pkg\app.msix"; ErrorAction = 'Stop' }
        if ($deps.Count) { $install.DependencyPath = $deps }
        Add-AppxPackage @install
        # Slice 179: counting off for every harness run (UsageEligibility.NoUsageVariable); a task started later reads the machine value.
        [Environment]::SetEnvironmentVariable('READTHESTUPIDTEXT_NO_USAGE', '1', 'Machine')
        $pkg = Get-AppxPackage -Name $name
        if (-not $pkg) { throw "Package $name is not installed after Add-AppxPackage." }
        return $pkg.Version
    }
}

function Copy-ResultFiles {
    # Slice 194: pulls the PNGs the checks left in the guest's results folder out to a host folder (before the reset to clean).
    param($Session, [string] $HostDir)
    New-Item -ItemType Directory -Force -Path $HostDir | Out-Null
    $files = @(Invoke-Command -Session $Session -ArgumentList $script:GuestWorkDir -ScriptBlock { param($dir) Get-ChildItem "$dir\results" -Filter *.png -ErrorAction SilentlyContinue | ForEach-Object FullName })
    foreach ($f in $files) { Copy-Item -FromSession $Session -Path $f -Destination $HostDir -Force; Write-Host "Copied $f to $HostDir" }
    if (-not $files.Count) { Write-Warning 'CopyOut: the checks left no PNG in the guest results folder.' }
}

function Assert-GuestPreflight {
    # Slice 207: collects what the guest lacks among the needs the selected checks declare (see the header of their scripts); each probe runs only when some check declares it.
    # -Only limits it to some needs: the network is tested before the install, the voice pack after it (its folder exists only once the package is installed).
    # The missing items of an earlier call are kept, and -Final throws one NO-GO line listing all of them.
    param($Session, [string[]] $Only = $script:KnownNeeds, [switch] $Final)
    $needs = @($AppProfile.Checks | ForEach-Object { Get-CheckNeeds "$PSScriptRoot\checks\$_.ps1" } | Select-Object -Unique | Where-Object { $Only -contains $_ })
    if ($SkipPreflight -or $needs.Count -eq 0) { Write-Host "Preflight ($($Only -join ', ')): $(if ($SkipPreflight) { 'skipped (-SkipPreflight)' } else { 'no check declares a need' })." }
    else { $script:PreflightMissing += @(Get-PreflightMissing -Session $Session -Needs $needs -PackageName $AppProfile.Name -PackId $script:VoicePackIds[$App]) }
    if (-not $Final) { return }
    if ($script:PreflightMissing.Count -gt 0) { throw "$($script:PreflightNoGo): $($script:PreflightMissing -join ', ')" }
    Write-Host 'Preflight: ok.'
}

function Write-Results {
    param([object[]] $Results)
    foreach ($r in $Results) {
        $mark = if ($r.pass) { 'PASS' } else { 'FAIL' }
        Write-Host ("{0}  {1,-20} {2}" -f $mark, $r.check, $r.message)
    }
}

# ---- main ----
$msix = Find-Package
$credential = Get-VmCredential
$results = @()
$noGo = $null
$script:PreflightMissing = @()
$session = $null
# Register (and wait for a quiet-machine timing run) before taking the VM lock, so a long wait never holds every other VM run up.
$activity = Start-Activity "vm-$App"
try { Enter-VmLock } catch { Stop-Activity $activity; throw }
try {
    Write-Host "Restoring '$($script:CheckpointName)' and starting the VM..."
    Restore-CleanCheckpoint -Start -Quiet
    $session = New-VmSession -Credential $credential
    Assert-GuestPreflight -Session $session -Only @('network')
    Copy-ToGuest -Session $session -Msix $msix
    Write-Host 'Installing the package in the VM...'
    Write-LicenseNote -License (Get-GuestLicense -Session $session)
    $version = Install-InGuest -Session $session -Name $AppProfile.Name
    Write-Host "Installed $($AppProfile.Name) $version in the VM from $msix"
    Assert-GuestPreflight -Session $session -Only @('voice-pack') -Final
    foreach ($check in $AppProfile.Checks) {
        if ($Sabotage -eq 'KillApp' -and $check -ne $AppProfile.Checks[0]) {
            Invoke-Command -Session $session -ArgumentList $AppProfile.Exe -ScriptBlock { param($exe) Stop-Process -Name $exe -Force -ErrorAction SilentlyContinue }
        }
        Write-Host "Running check $check..."
        $r = Invoke-InteractiveScript -Session $session -GuestScript "$($script:GuestWorkDir)\checks\$check.ps1" -ScriptArgs @($AppProfile.Name)
        $message = if ($r.noResult) { "$($r.message); screen: $(Get-ScreenText (Save-VmScreenshot -Reason "check-$check"))" } else { $r.message }
        $results += [pscustomobject]@{ check = $check; pass = [bool]$r.pass; message = $message }
    }
    if ($CopyOut) { Copy-ResultFiles -Session $session -HostDir $CopyOut }
} catch {
    $failure = $_.Exception.Message
    if ($failure.StartsWith($script:PreflightNoGo)) { $noGo = $failure; Write-Host $noGo }
    else {
        $hasShot = $failure -match 'screen: \S'
        $shot = if ($hasShot) { $null } else { Save-VmScreenshot -Reason 'harness' }
        $results += [pscustomobject]@{ check = 'harness'; pass = $false; message = $(if ($hasShot) { $failure } else { "$failure; screen: $(Get-ScreenText $shot)" }) }
    }
} finally {
    if ($session) { Remove-PSSession $session -ErrorAction SilentlyContinue }
    if (-not $KeepRunning) {
        Write-Host "Restoring '$($script:CheckpointName)'..."
        try { Restore-CleanCheckpoint -Quiet } catch { Write-Warning "Reset to clean failed: $($_.Exception.Message)" }
    }
    Stop-Activity $activity
    Exit-VmLock
}
if ($noGo) { exit $script:PreflightExitCode }
Write-Results -Results $results
if ($results.Where({ -not $_.pass }).Count -gt 0) { exit 1 }
exit 0
