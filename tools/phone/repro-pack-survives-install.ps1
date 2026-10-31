<#
.SYNOPSIS
  Slice 216 item 1: does `adb install -r` of the other build variant keep the dev app's voice packs? (Written, NOT yet run.)
.DESCRIPTION
  repro-pack-survives-install.ps1 -App rtst|dino [-Serial s]
  Needs the phone lease (the same rules as every phone script), both variants in the build cache (run-checks.ps1 -Build -Variant debug / aot), and the packs installed on the dev app
  (run-checks.ps1 -Prepare). Steps: (1) install the AOT build with `adb install -r` and list the installed packs, (2) install the plain Debug build over it with `adb install -r` and list them
  again, (3) print PACKS SURVIVED or PACKS LOST with both lists and the lastUpdateTime of each install. Nothing is cleared or uninstalled, only the .dbg package is touched, and no pack is
  deleted: if the packs are lost the aside copy (run-checks.ps1 -Prepare restores it) is the way back.
  It does not rewrite run-checks' install marker, so the next run-checks.ps1 run needs -Reinstall once (without it the install plan is Skip).
  Exit: 0 survived, 1 lost, 2 no phone, 4 leased by someone else, 6 a variant is not in the cache, 7 an install or probe failed.
  If the packs SURVIVE, the Slice 213 re-download came from the script's flags (audio-timing.ps1 -FreshInstall clears the dev app's data), not from the install: fix or document the flags.
  If they are LOST, it is an app bug: find why the packs folder is cleared on an update (Android and iOS) before changing src/.
#>
param(
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [string]$Serial
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'checks\_lib.ps1')
. (Join-Path $PSScriptRoot '..\emulator\build-cache.ps1')

$package = Get-DevPackage $App
$script:ApkFilter = '*-Signed.apk'
$script:ExitSurvived = 0; $script:ExitLost = 1; $script:ExitMissingBuild = 6; $script:ExitProbeFailed = 7

function Install-Variant([string]$Phone, [string]$Variant, $Apk) {
    $result = "$(Invoke-Adb $Phone @('install', '-r', $Apk.FullName))"
    if ($result -notmatch 'Success') { throw "adb install -r of the $Variant build failed: $result" }
    $update = Get-PackageLastUpdate ((Invoke-Adb $Phone @('shell', 'dumpsys', 'package', $package)) -join "`n")
    Write-Host "   installed the $Variant build over the dev app (lastUpdateTime $update)"
    Write-Host "   NOTE: run-checks' install marker no longer matches; the next run-checks.ps1 / audio-timing run needs -Reinstall once."
}

function Get-PacksNow([string]$Phone) {
    return @(Get-InstalledPackFolders $Phone $package)
}

$phone = Find-WirelessPhone $Serial
if (-not $phone) { Write-PairingAsk; exit 2 }
Assert-PhoneLease $phone
$apks = [ordered]@{}
foreach ($variant in 'aot', 'debug') {
    # Only the build for the CURRENT inputs: a stale build could fail or downgrade on install and give a misleading verdict.
    $apk = Find-CachedBuild (Get-BuildDir $App (Get-CachePlatform $variant) (Get-BuildKey)) $script:ApkFilter
    if (-not $apk) { Write-Host "No cached $variant build of $App for the current build inputs: run run-checks.ps1 -Build -App $App -Variant $variant first."; exit $script:ExitMissingBuild }
    $apks[$variant] = $apk
}
try {
    Install-Variant $phone 'aot' $apks['aot']
    $before = Get-PacksNow $phone
    Write-Host "packs after the AOT install: $(if ($before.Count) { $before -join ', ' } else { '(none)' })"
    if ($before.Count -eq 0) { Write-Host 'The dev app has no pack to lose: run run-checks.ps1 -Prepare first.'; exit $script:ExitProbeFailed }
    Install-Variant $phone 'debug' $apks['debug']
    $after = Get-PacksNow $phone
    Write-Host "packs after the Debug install: $(if ($after.Count) { $after -join ', ' } else { '(none)' })"
} catch { Write-Host "REPRO FAILED: $($_.Exception.Message)"; exit $script:ExitProbeFailed }
$lost = @($before | Where-Object { $after -cnotcontains $_ })
if ($lost.Count -eq 0) { Write-Host 'PACKS SURVIVED: adb install -r keeps the packs; the earlier re-download came from the script flags (-FreshInstall clears the dev app data).'; exit $script:ExitSurvived }
Write-Host "PACKS LOST: $($lost -join ', ') gone after adb install -r over the other variant. App bug: find why files/packs is cleared on an update (Android and iOS). Restore with run-checks.ps1 -Prepare."
exit $script:ExitLost
