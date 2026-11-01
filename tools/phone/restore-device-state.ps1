<#
.SYNOPSIS
  Clears what a dead check left on the real phone, without the user (Slice 225 item 1).
.DESCRIPTION
  Detects and clears: a leftover `device_config activity_manager data_sync_fgs_timeout_duration`, the FGS_INTRODUCE_TIME_LIMITS compat flag on the two .dbg dev packages (always reset:
  the override cannot be read back reliably, and the reset is idempotent), and any `restrict-background-blacklist` entry of a dev package's uid (another app's entry is the user's own data-saver
  choice and is never touched). It also cancels a pending device-side restore. Then it reads the settings again and says what is still set.
  When something was found (or with -Reset) it runs `reset-dev-app.ps1 -Level auto` for each dev app that is installed, because a check that died mid-run can have left the app broken too.
  Wi-Fi and mobile data are never touched (a wireless adb link runs over them). Exit 0 clean or cleared, 1 something is still set or an app did not come back, 2 no phone, 4 the phone is leased by someone else, 5 the block list could not be read back (unknown, not a failure).
  Self-test with a fake adb: test-phone-leftovers.ps1.
#>
param(
    [string]$Serial,
    [switch]$Reset,
    [ValidateSet('rtst', 'dino', 'both')][string]$App = 'both'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$script:UnknownExitCode = 5

if (-not $Serial) { $Serial = Find-WirelessPhone '' }
if (-not $Serial) { Write-PairingAsk; exit 2 }
Assert-PhoneLease $Serial

$apps = if ($App -eq 'both') { @('rtst', 'dino') } else { @($App) }
$packages = @($apps | ForEach-Object { Get-DevPackage $_ })
$result = Clear-DeviceLeftovers $Serial $packages
foreach ($line in $result.Leftovers) { Write-Host "  cleared: $line" }
if ($result.Leftovers.Count -eq 0) { Write-Host '  no dataSync timeout and no dev-app network block was left (the compat flag was reset anyway).' }
if ($result.Remaining.Count -gt 0) { Write-Host "RESTORE FAILED: still set: $($result.Remaining -join '; ')"; exit 1 }

$exit = 0
if ($result.Leftovers.Count -gt 0 -or $Reset) {
    foreach ($name in $apps) {
        try { $null = Get-PackageUid $Serial (Get-DevPackage $name) } catch { Write-Host "  $name dev app is not installed: no reset"; continue }
        Write-Host "  reset-dev-app.ps1 -App $name -Level auto"
        & (Join-Path $PSScriptRoot 'reset-dev-app.ps1') -App $name -Level auto -Serial $Serial | Out-Host
        if ($LASTEXITCODE -ne 0) { Write-Host "  $name did not come back alive (reset-dev-app exit $LASTEXITCODE)"; $exit = 1 }
    }
}
if ($exit -ne 0) { Write-Host 'RESTORE INCOMPLETE'; exit $exit }
# A block list that could not be read is neither clean nor "still set": its own exit code, and never "RESTORE FAILED" on that alone.
if ($result.Unknown.Count -gt 0) { Write-Host "RESTORE UNKNOWN: could not read back: $($result.Unknown -join '; ')"; exit $script:UnknownExitCode }
Write-Host 'RESTORE OK'
exit 0
