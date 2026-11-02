<#
.SYNOPSIS
  Creates the second test AVD, RTST_Test2, as a twin of RTST_Test (Slice 180), or with -Tablet the tablet AVD RTST_Tablet (Slice 242).
.DESCRIPTION
  Copies only config.ini (same system image, device, RAM and screen) and writes the AVD's .ini pointer. No system image is
  downloaded: it is the one RTST_Test already uses. The emulator creates the disk files on the first boot, so the new AVD
  starts small on C: and grows as the first one did (RTST_Test's userdata is about 12 GB after heavy use).
  Does nothing when the AVD exists (-Force recreates it, deleting its disk files).
  -Tablet (Slice 242 item 5): RTST_Tablet is the same twin with a tablet screen, 1600 x 2560 px at 320 dpi = 800 x 1280 dp (short side 800 dp, above the 600 dp
  tablet line). Run flows on it with `run-flows.ps1 -Instance tablet` (instance 3, port 5558); start it with `start-warm.ps1 -Instance tablet`.
#>
param(
    [switch]$Force,
    [switch]$Tablet
)
$ErrorActionPreference = 'Stop'

$avdHome = if ($env:ANDROID_AVD_HOME) { $env:ANDROID_AVD_HOME } else { Join-Path $env:USERPROFILE '.android\avd' }
$sourceName = 'RTST_Test'
$twinName = if ($Tablet) { 'RTST_Tablet' } else { 'RTST_Test2' }
# The tablet screen: a 600 dp short side is the tablet line; 800 dp leaves room above it.
$tabletScreen = @{ 'hw.lcd.width' = '1600'; 'hw.lcd.height' = '2560'; 'hw.lcd.density' = '320' }
$sourceDir = Join-Path $avdHome "$sourceName.avd"
$twinDir = Join-Path $avdHome "$twinName.avd"
$twinIni = Join-Path $avdHome "$twinName.ini"

if (-not (Test-Path (Join-Path $sourceDir 'config.ini'))) { throw "$sourceName was not found in $avdHome; create it first." }
if ((Test-Path $twinDir) -and -not $Force) { Write-Host "$twinName already exists."; return }
if (Test-Path $twinDir) { Remove-Item $twinDir -Recurse -Force }
$null = New-Item -ItemType Directory -Force $twinDir
# The twin must not claim the first AVD's name: AvdId and the display name are rewritten, every other line is copied as it is (the tablet also swaps its screen size).
$config = Get-Content (Join-Path $sourceDir 'config.ini') | ForEach-Object {
    if ($_ -match '^AvdId\s*=') { "AvdId=$twinName" }
    elseif ($_ -match '^avd\.ini\.displayname\s*=') { "avd.ini.displayname=$twinName" }
    elseif ($Tablet -and $_ -match '^(hw\.lcd\.(width|height|density))\s*=') { "$($Matches[1]) = $($tabletScreen[$Matches[1]])" }
    else { $_ }
}
if ($Tablet) {
    # A source config without one of the three keys would leave the tablet phone-sized or mixed: append what the copy did not replace.
    foreach ($key in $tabletScreen.Keys) {
        if (-not ($config | Where-Object { $_ -match ('^' + [regex]::Escape($key) + '\s*=') })) { $config += "$key = $($tabletScreen[$key])" }
    }
    # A copied skin sets its own screen size and wins over hw.lcd.* (the "tablet" would boot phone-sized): drop the skin lines so the hw.lcd size above is used.
    $config = @($config | Where-Object { $_ -notmatch '^skin\.(name|path)\s*=' })
}
$config | Set-Content (Join-Path $twinDir 'config.ini')

# The pointer file says where the AVD lives and which Android target it uses (the target line is copied from the first AVD's own pointer).
$target = Get-Content (Join-Path $avdHome "$sourceName.ini") | Where-Object { $_ -like 'target=*' } | Select-Object -First 1
@('avd.ini.encoding=UTF-8', "path=$twinDir", "path.rel=avd\$twinName.avd", $target) | Set-Content $twinIni
Write-Host "Created $twinName at $twinDir (config only; disk files appear on the first boot)."
