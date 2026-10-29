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
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('rtst', 'dino', 'auto')] [string] $App,
    [string] $Package,
    [string] $VmRoot = 'D:\ReadTheStupidText-worktrees\_desktop-vm',
    [ValidateSet('None', 'KillApp')] [string] $Sabotage = 'None',
    [switch] $KeepRunning
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"
$script:ActiveVmRoot = $VmRoot
. "$PSScriptRoot\..\emulator\build-cache.ps1"

# -App auto: one run per touched app, each through this same script, so a failing app does not hide the next one.
if ($App -eq 'auto') {
    if ($Package) { throw '-Package names one MSIX; it cannot be combined with -App auto.' }
    $touched = @(& "$PSScriptRoot\..\test-targets.ps1" -Platform windows -Raw)
    if ($touched.Count -eq 0) { Write-Host 'NO WINDOWS APP IS TOUCHED BY THIS CHANGE: no desktop test ran. That is a skip, not a test pass.'; exit 0 }
    $worst = 0
    foreach ($name in $touched) {
        & $PSCommandPath -App $name -VmRoot $VmRoot -Sabotage $Sabotage -KeepRunning:$KeepRunning
        if ($LASTEXITCODE -ne 0) { $worst = 1 }
    }
    exit $worst
}

# One row per product: the package identity name, where a fresh MSIX is usually found and the ordered checks.
$Apps = @{
    rtst = @{ Name = 'AshkanSirous.ReadTheStupidText'; Exe = 'ReadTheStupidText.App'; Checks = @('rtst-launch', 'rtst-tray-icon', 'rtst-hotkey-read', 'rtst-selection-hotkey', 'rtst-panel-speed', 'rtst-startup-task') }
    dino = @{ Name = 'AshkanSirous.DinoReader'; Exe = 'DinoReader.App'; Checks = @('dino-launch', 'dino-open-book', 'dino-page-turn') }
}
$AppProfile = $Apps[$App]

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
$session = $null
# Register (and wait for a quiet-machine timing run) before taking the VM lock, so a long wait never holds every other VM run up.
$activity = Start-Activity "vm-$App"
try { Enter-VmLock } catch { Stop-Activity $activity; throw }
try {
    Write-Host "Restoring '$($script:CheckpointName)' and starting the VM..."
    Restore-CleanCheckpoint -Start -Quiet
    $session = New-VmSession -Credential $credential
    Copy-ToGuest -Session $session -Msix $msix
    Write-Host 'Installing the package in the VM...'
    Write-LicenseNote -License (Get-GuestLicense -Session $session)
    $version = Install-InGuest -Session $session -Name $AppProfile.Name
    Write-Host "Installed $($AppProfile.Name) $version in the VM from $msix"
    foreach ($check in $AppProfile.Checks) {
        if ($Sabotage -eq 'KillApp' -and $check -ne $AppProfile.Checks[0]) {
            Invoke-Command -Session $session -ArgumentList $AppProfile.Exe -ScriptBlock { param($exe) Stop-Process -Name $exe -Force -ErrorAction SilentlyContinue }
        }
        Write-Host "Running check $check..."
        $r = Invoke-InteractiveScript -Session $session -GuestScript "$($script:GuestWorkDir)\checks\$check.ps1" -ScriptArgs @($AppProfile.Name)
        $message = if ($r.noResult) { "$($r.message); screen: $(Get-ScreenText (Save-VmScreenshot -Reason "check-$check"))" } else { $r.message }
        $results += [pscustomobject]@{ check = $check; pass = [bool]$r.pass; message = $message }
    }
} catch {
    $failure = $_.Exception.Message
    $hasShot = $failure -match 'screen: \S'
    $shot = if ($hasShot) { $null } else { Save-VmScreenshot -Reason 'harness' }
    $results += [pscustomobject]@{ check = 'harness'; pass = $false; message = $(if ($hasShot) { $failure } else { "$failure; screen: $(Get-ScreenText $shot)" }) }
} finally {
    if ($session) { Remove-PSSession $session -ErrorAction SilentlyContinue }
    if (-not $KeepRunning) {
        Write-Host "Restoring '$($script:CheckpointName)'..."
        try { Restore-CleanCheckpoint -Quiet } catch { Write-Warning "Reset to clean failed: $($_.Exception.Message)" }
    }
    Stop-Activity $activity
    Exit-VmLock
}
Write-Results -Results $results
if ($results.Where({ -not $_.pass }).Count -gt 0) { exit 1 }
exit 0
