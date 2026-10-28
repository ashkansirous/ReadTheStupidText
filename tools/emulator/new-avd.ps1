<#
.SYNOPSIS
  Creates the second test AVD, RTST_Test2, as a twin of RTST_Test (Slice 180).
.DESCRIPTION
  Copies only config.ini (same system image, device, RAM and screen) and writes the AVD's .ini pointer. No system image is
  downloaded: it is the one RTST_Test already uses. The emulator creates the disk files on the first boot, so the new AVD
  starts small on C: and grows as the first one did (RTST_Test's userdata is about 12 GB after heavy use).
  Does nothing when RTST_Test2 exists (-Force recreates it, deleting its disk files).
#>
param(
    [switch]$Force
)
$ErrorActionPreference = 'Stop'

$avdHome = if ($env:ANDROID_AVD_HOME) { $env:ANDROID_AVD_HOME } else { Join-Path $env:USERPROFILE '.android\avd' }
$sourceName = 'RTST_Test'
$twinName = 'RTST_Test2'
$sourceDir = Join-Path $avdHome "$sourceName.avd"
$twinDir = Join-Path $avdHome "$twinName.avd"
$twinIni = Join-Path $avdHome "$twinName.ini"

if (-not (Test-Path (Join-Path $sourceDir 'config.ini'))) { throw "$sourceName was not found in $avdHome; create it first." }
if ((Test-Path $twinDir) -and -not $Force) { Write-Host "$twinName already exists."; return }
if (Test-Path $twinDir) { Remove-Item $twinDir -Recurse -Force }
$null = New-Item -ItemType Directory -Force $twinDir
# The twin must not claim the first AVD's name: AvdId and the display name are rewritten, every other line is copied as it is.
$config = Get-Content (Join-Path $sourceDir 'config.ini') | ForEach-Object {
    if ($_ -match '^AvdId\s*=') { "AvdId=$twinName" }
    elseif ($_ -match '^avd\.ini\.displayname\s*=') { "avd.ini.displayname=$twinName" }
    else { $_ }
}
$config | Set-Content (Join-Path $twinDir 'config.ini')

# The pointer file says where the AVD lives and which Android target it uses (the target line is copied from the first AVD's own pointer).
$target = Get-Content (Join-Path $avdHome "$sourceName.ini") | Where-Object { $_ -like 'target=*' } | Select-Object -First 1
@('avd.ini.encoding=UTF-8', "path=$twinDir", "path.rel=avd\$twinName.avd", $target) | Set-Content $twinIni
Write-Host "Created $twinName at $twinDir (config only; disk files appear on the first boot)."
