<#
.SYNOPSIS
  Self-test of the pure functions of store-screenshots.ps1 (Slice 291 item 5): dot-sources it, no device, no Maestro.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'store-screenshots.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { Write-Host "FAIL $Name : expected '$Expected', got '$Actual'"; $script:failed++ }
}
function Assert-Throws([string]$Name, [scriptblock]$Block) {
    try { & $Block; Write-Host "FAIL $Name : no error"; $script:failed++ } catch { }
}

Assert-Equal 'phone default is instance 1' '1' (Get-StoreShotInstance '' 'phone')
Assert-Equal 'tablet default is instance 3' '3' (Get-StoreShotInstance '' 'tablet')
Assert-Equal 'phone on emulator-5554' '1' (Get-StoreShotInstance 'emulator-5554' 'phone')
Assert-Equal 'phone on emulator-5556' '2' (Get-StoreShotInstance 'emulator-5556' 'phone')
Assert-Equal 'tablet on emulator-5558' '3' (Get-StoreShotInstance 'emulator-5558' 'tablet')
Assert-Throws 'tablet form on a phone AVD' { Get-StoreShotInstance 'emulator-5554' 'tablet' }
Assert-Throws 'phone form on the tablet AVD' { Get-StoreShotInstance 'emulator-5558' 'phone' }
Assert-Throws 'a real phone serial' { Get-StoreShotInstance 'R5CT1234' 'phone' }

$flow = "- takeScreenshot: store-dino-phone-1-shelf`n- tapOn: x`n- takeScreenshot: store-dino-phone-2-reading`n- takeScreenshot: store-dino-tablet-1-shelf`n"
Assert-Equal 'shot names of the phone form, in order' 'store-dino-phone-1-shelf,store-dino-phone-2-reading' ((Get-StoreShotNames $flow 'dino' 'phone') -join ',')
Assert-Equal 'no shot for another app' 0 @(Get-StoreShotNames $flow 'rtst' 'phone').Count

# Slice 291 item 8: the real Dino flows take the five shots of the skill's table, in order (reading, record, language, grown-ups, shelf; no store shot).
$dinoShots = 'reading', 'record', 'language', 'grownups', 'shelf'
foreach ($form in 'phone', 'tablet') {
    $flowFile = Join-Path $PSScriptRoot "..\..\.maestro\dino\needs-file\store-shot-$form.yaml"
    $expected = (1..5 | ForEach-Object { "store-dino-$form-$_-$($dinoShots[$_ - 1])" }) -join ','
    Assert-Equal "the Dino $form flow's five shots" $expected ((Get-StoreShotNames (Get-Content -Raw $flowFile) 'dino' $form) -join ',')
}

$work = Join-Path ([IO.Path]::GetTempPath()) "store-shots-test-$PID"
try {
    $null = New-Item -ItemType Directory -Force (Join-Path $work 'i1'), (Join-Path $work 'i2')
    # A 1x1 PNG header (signature + IHDR width/height) is all Get-PngSize reads.
    $png = [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A, 0,0,0,13, 0x49,0x48,0x44,0x52, 0,0,0,1, 0,0,0,1, 8,6,0,0,0)
    $zero = [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A, 0,0,0,13, 0x49,0x48,0x44,0x52, 0,0,0,0, 0,0,0,0, 8,6,0,0,0)
    [IO.File]::WriteAllBytes((Join-Path $work 'i1\store-dino-phone-1-shelf.png'), $png)
    [IO.File]::WriteAllBytes((Join-Path $work 'i2\store-dino-phone-2-reading.png'), $png)
    [IO.File]::WriteAllBytes((Join-Path $work 'i1\x-store-dino-phone-3-near.png'), $png)   # similar name: never a match
    [IO.File]::WriteAllBytes((Join-Path $work 'i1\store-dino-phone-4-empty.png'), $zero)    # zero pixel size: never copied
    [IO.File]::WriteAllBytes((Join-Path $work 'i1\store-dino-phone-5-text.png'), [byte[]](97))   # not a PNG: never copied
    $missing = @(Copy-StoreShots @('store-dino-phone-1-shelf', 'store-dino-phone-2-reading') @((Join-Path $work 'i1')) (Join-Path $work 'out'))
    Assert-Equal 'only this instance folder is searched: the other one is missing' 'store-dino-phone-2-reading' ($missing -join ',')
    Assert-Equal 'the found shot is copied' $true (Test-Path (Join-Path $work 'out\store-dino-phone-1-shelf.png'))
    $bad = @(Copy-StoreShots @('store-dino-phone-3-near', 'store-dino-phone-4-empty', 'store-dino-phone-5-text') @((Join-Path $work 'i1')) (Join-Path $work 'out2'))
    Assert-Equal 'a similar name, a zero-size PNG and a non-PNG are all missing' 3 $bad.Count
    Remove-OldStoreShots @('store-dino-phone-1-shelf') @((Join-Path $work 'i1'))
    Assert-Equal 'an old shot is deleted before the run' $false (Test-Path (Join-Path $work 'i1\store-dino-phone-1-shelf.png'))
    Assert-Equal 'a similarly named file is left alone' $true (Test-Path (Join-Path $work 'i1\x-store-dino-phone-3-near.png'))
} finally { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-store-screenshots: all passed.'
exit 0
