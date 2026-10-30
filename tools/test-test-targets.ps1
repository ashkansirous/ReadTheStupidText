<#
.SYNOPSIS
  Self-test for test-targets.ps1 -Checks (Slice 207): which dotnet build and dotnet test lines a diff names. Needs node (ship-targets.mjs) and no device.
  Run it in both `powershell` (5.1) and `pwsh`.
#>
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'test-targets.ps1'
$failed = 0

function Get-CheckLines([string[]]$Files) {
    return @(& $script -Checks -Files $Files 6>$null | ForEach-Object { "$_" })
}

function Assert-Lines([string]$Name, [string[]]$Files, [string[]]$Expected) {
    $actual = Get-CheckLines $Files
    if (($actual -join "`n") -ne ($Expected -join "`n")) {
        $script:failed++
        Write-Host "FAIL: $Name`n  expected: $($Expected -join ' | ')`n  got:      $($actual -join ' | ')"
    }
}

$rtstAndroid = 'dotnet build src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj -f net10.0-android'
$dinoAndroid = 'dotnet build src/DinoReader.App/DinoReader.App.csproj -f net10.0-android'
$rtstWindows = 'dotnet build src/ReadTheStupidText.App/ReadTheStupidText.App.csproj -p:Platform=x64'
$dinoWindows = 'dotnet build src/DinoReader.App/DinoReader.App.csproj -f net10.0-windows10.0.19041.0 -p:IncludeAndroid=false -p:Platform=x64'
$rtstTests = 'dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj'
$dinoTests = 'dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj'

# Mobile.Shared always names both Android apps and both test projects, even for iOS-only code (which CI maps to iOS alone).
$shared = Get-CheckLines @('src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/X.cs')
foreach ($line in $rtstAndroid, $dinoAndroid, $rtstTests, $dinoTests) {
    if ($shared -notcontains $line) { $failed++; Write-Host "FAIL: Mobile.Shared iOS file is missing [$line]; got: $($shared -join ' | ')" }
}
Assert-Lines 'Dino.Core reaches the Dino tests and the Dino builds only' @('src/DinoReader.Core/A.cs') @($dinoAndroid, $dinoWindows, $dinoTests)
Assert-Lines 'Windows tray code reaches the RTST tests and the RTST Windows build only' @('src/ReadTheStupidText.Windows.Tray/A.cs') @($rtstWindows, $rtstTests)
Assert-Lines 'a Dino test file runs the Dino tests with no build' @('tests/DinoReader.Tests/A.cs') @($dinoTests)
Assert-Lines 'an RTST test file runs the RTST tests with no build' @('tests/ReadTheStupidText.Tests/A.cs') @($rtstTests)
Assert-Lines 'test tooling alone names no build or test line' @('tools/lease-common.ps1', '.maestro/rtst/speed.yaml') @()
Assert-Lines 'a shared build input names every build and both test projects' @('Directory.Build.props') @($rtstAndroid, $dinoAndroid, $rtstWindows, $dinoWindows, $rtstTests, $dinoTests)
Assert-Lines 'an unreadable change names everything' @('*') @($rtstAndroid, $dinoAndroid, $rtstWindows, $dinoWindows, $rtstTests, $dinoTests)

$infra = Get-CheckLines @('src/ReadTheStupidText.Infrastructure/A.cs')
if ($infra -notcontains $dinoTests) { $failed++; Write-Host "FAIL: Infrastructure (used by Dino's Windows head) is missing [$dinoTests]; got: $($infra -join ' | ')" }

$domain = Get-CheckLines @('src/ReadTheStupidText.Domain/A.cs')
foreach ($line in $rtstTests, $dinoTests) {
    if ($domain -notcontains $line) { $failed++; Write-Host "FAIL: Domain is missing [$line]; got: $($domain -join ' | ')" }
}

if ($failed) { Write-Host "$failed check(s) failed."; exit 1 }
Write-Host "All test-targets -Checks checks pass (PowerShell $($PSVersionTable.PSVersion))."
exit 0
