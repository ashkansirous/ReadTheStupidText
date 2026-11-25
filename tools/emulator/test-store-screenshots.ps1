<#
.SYNOPSIS
  Self-test of the pure functions of store-screenshots.ps1 (Slice 291 item 5): dot-sources it, no device, no Maestro.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'store-screenshots.ps1')
. (Join-Path $PSScriptRoot 'store-captions.ps1')   # Get-CaptionKey, for the caption data checks below (store-screenshots.ps1 shares only store-frames.ps1 with it)
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

$flow = "- takeScreenshot: store-dino-phone-1-reading`n- tapOn: x`n- takeScreenshot: store-dino-phone-2-record`n- takeScreenshot: store-dino-tablet-1-reading`n"
Assert-Equal 'shot names of the phone form, in order' 'store-dino-phone-1-reading,store-dino-phone-2-record' ((Get-StoreShotNames $flow 'dino' 'phone') -join ',')
Assert-Equal 'no shot for another app' 0 @(Get-StoreShotNames $flow 'rtst' 'phone').Count

# Slice 433: the real Dino flows take the note's six shots in order (picture, voices, record, language, shelf, ages), with the Japanese language shot as an extra file after the Spanish one; every shot but the extra has a caption line.
$dinoShots = '1-picture', '2-voices', '3-record', '4-language', '4-language-ja', '5-shelf', '6-ages'
$captionData = Get-Content (Join-Path $PSScriptRoot '..\..\docs\store-listings\screenshots\captions.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($form in 'phone', 'tablet') {
    $flowFile = Join-Path $PSScriptRoot "..\..\.maestro\dino\needs-file\store-shot-$form.yaml"
    $expected = ($dinoShots | ForEach-Object { "store-dino-$form-$_" }) -join ','
    Assert-Equal "the Dino $form flow's shots" $expected ((Get-StoreShotNames (Get-Content -Raw $flowFile) 'dino' $form) -join ',')
}
foreach ($shot in $dinoShots) { Assert-Equal "shot $shot has an English caption line" $true ($null -ne $captionData.dino.(Get-CaptionKey $shot).en) }

# Slice 433 item 1: the emulator is sized to the store frame; the bars come from the device, else the form default.
Assert-Equal 'Play phone: 1080 wide, the area under the 288 px band plus the status bar' '1080 1695' ((Get-StoreShotWmSize 'phone' 63 0) -join ' ')
Assert-Equal 'Play phone with a navigation bar' '1080 1759' ((Get-StoreShotWmSize 'phone' 63 64) -join ' ')
Assert-Equal 'Play tablet: no band, 1440 wide' '1440 2623' ((Get-StoreShotWmSize 'tablet' 63 0) -join ' ')
Assert-Equal 'wm size is set to WxH' 'shell wm size 1080x1695' ((Get-WmSizeEnterCommands @(1080, 1695))[0] -join ' ')
Assert-Equal 'wm size is reset after the run' 'shell wm size reset' ((Get-WmSizeExitCommands)[0] -join ' ')
$wmSent = New-Object System.Collections.Generic.List[string]
$null = Invoke-StatusBarDemo (Get-WmSizeExitCommands) { param($a) $wmSent.Add(($a -join ' ')) }
Assert-Equal 'the one wm size command is sent whole' 'shell wm size reset' ($wmSent -join '|')
$dump = "InsetsSource id=1 type=statusBars frame=[0,0][1080,63] visible=true`r`nInsetsSource id=2 type=navigationBars frame=[0,2337][1080,2400] visible=true`r`n"
Assert-Equal 'status bar height from the window dump' 63 (Get-InsetBarPx $dump 'statusBars')
Assert-Equal 'navigation bar height from the window dump' 63 (Get-InsetBarPx $dump 'navigationBars')
Assert-Equal 'no such bar in the dump' $null (Get-InsetBarPx 'nothing here' 'statusBars')
Assert-Equal 'the override density wins' 420 (Get-DensityDpi "Physical density: 440`r`nOverride density: 420")
Assert-Equal 'the physical density' 440 (Get-DensityDpi 'Physical density: 440')
$adb = { param($a) if ($a -contains 'density') { 'Physical density: 420' } else { 'nothing about bars' } }
Assert-Equal 'no bar in the dump: 24 dp at 420 dpi' '63 0' ((Get-DeviceBars -1 -1 $adb | ForEach-Object { "$($_.StatusPx) $($_.NavPx)" }))
$adbDump = { param($a) if ($a -contains 'dumpsys') { $dump } else { '' } }
Assert-Equal 'both bars read from the dump' '63 63' ((Get-DeviceBars -1 -1 $adbDump | ForEach-Object { "$($_.StatusPx) $($_.NavPx)" }))
Assert-Equal 'an explicit value wins over the dump' '80 0' ((Get-DeviceBars 80 0 $adbDump | ForEach-Object { "$($_.StatusPx) $($_.NavPx)" }))
$sizeDir = Join-Path ([IO.Path]::GetTempPath()) "store-size-test-$PID"
try {
    $null = New-Item -ItemType Directory -Force $sizeDir
    $png = [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A, 0,0,0,13, 0x49,0x48,0x44,0x52, 0,0,4,56, 0,0,6,159, 8,2,0,0,0)   # 1080 x 1695
    [IO.File]::WriteAllBytes((Join-Path $sizeDir 'store-dino-phone-1-picture.png'), $png)
    Assert-Equal 'a raw shot at the set size has no problem' 0 @(Get-RawShotSizeProblems @('store-dino-phone-1-picture') $sizeDir @(1080, 1695)).Count
    Assert-Equal 'a raw shot of another size is named' 1 @(Get-RawShotSizeProblems @('store-dino-phone-1-picture') $sizeDir @(1080, 2400)).Count
    Assert-Equal 'a missing raw shot is named' 1 @(Get-RawShotSizeProblems @('store-dino-phone-2-voices') $sizeDir @(1080, 1695)).Count
} finally { Remove-Item $sizeDir -Recurse -Force -ErrorAction SilentlyContinue }

# Slice 393 item 14: status bar demo mode (fixed clock, full battery, no notification icons), on before the flow and off after it.
$enter = @(Get-StatusBarDemoEnterCommands '1000')
Assert-Equal 'demo mode is allowed first' 'shell settings put global sysui_demo_allowed 1' ($enter[0] -join ' ')
Assert-Equal 'demo mode is entered second' 'shell am broadcast -a com.android.systemui.demo -e command enter' ($enter[1] -join ' ')
Assert-Equal 'the clock is fixed' 'shell am broadcast -a com.android.systemui.demo -e command clock -e hhmm 1000' ($enter[2] -join ' ')
Assert-Equal 'notification icons are hidden' 'shell am broadcast -a com.android.systemui.demo -e command notifications -e visible false' ($enter[-1] -join ' ')
Assert-Equal 'seven enter commands' 7 $enter.Count
$exit = @(Get-StatusBarDemoExitCommands)
Assert-Equal 'demo mode is left first' 'shell am broadcast -a com.android.systemui.demo -e command exit' ($exit[0] -join ' ')
Assert-Equal 'demo mode is disallowed again' 'shell settings put global sysui_demo_allowed 0' ($exit[1] -join ' ')
$sent = New-Object System.Collections.Generic.List[string]
Invoke-StatusBarDemo $exit { param($a) $sent.Add(($a -join ' ')) }
Assert-Equal 'every exit command is sent' 2 $sent.Count
Assert-Equal 'no failure when every call goes through' 0 @(Invoke-StatusBarDemo $exit { param($a) }).Count
$threw = $false
$reported = @()   # not $failed: that name is the script-scope check counter
try { $reported = @(Invoke-StatusBarDemo $exit { param($a) throw 'adb is gone' }) } catch { $threw = $true }
Assert-Equal 'a failing adb call does not throw' $false $threw
Assert-Equal 'every failing call is reported' 2 $reported.Count
Assert-Equal 'the failure names the command and the cause' $true ($reported[0] -like "*command exit*adb is gone*")
$firstOnly = @(Invoke-StatusBarDemo $exit { param($a) if ($a -contains 'broadcast') { throw 'no broadcast' } })
Assert-Equal 'one failing call among several is reported alone' 1 $firstOnly.Count

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

# Slice 404: the full-shelf packages (newest per book) and the adb commands that put them in the app's files folder.
$shelf = Join-Path ([IO.Path]::GetTempPath()) "full-shelf-test-$PID"
try {
    foreach ($name in 'story-v2.dino', 'story-v10.dino', 'story-v9.dino', 'meta.json') { $null = New-Item -ItemType File -Force (Join-Path $shelf "aaa\$name") }
    $null = New-Item -ItemType File -Force (Join-Path $shelf 'bbb\story-v1.dino')
    $null = New-Item -ItemType Directory -Force (Join-Path $shelf 'empty')
    $found = @(Get-FullShelfPackages $shelf)
    Assert-Equal 'one package per book folder with a package' 2 $found.Count
    Assert-Equal 'the highest version number wins (10, not 9 or 2)' 'story-v10.dino' (Split-Path $found[0].Path -Leaf)
    Assert-Equal 'the id is the folder name' 'aaa,bbb' (($found | ForEach-Object Id) -join ',')
    Assert-Equal 'a missing root is no package' 0 @(Get-FullShelfPackages (Join-Path $shelf 'nope')).Count
} finally { Remove-Item $shelf -Recurse -Force -ErrorAction SilentlyContinue }
$fetchArguments = @(Get-FetchShelfPackagesArguments 'C:\repo' 'C:\repo\artifacts\full-shelf')
Assert-Equal 'the fetch runs the shared node script with the target folder, never BookBuilder' 'C:\repo\.github\scripts\fetch-shelf-packages.mjs C:\repo\artifacts\full-shelf' ($fetchArguments -join ' ')
Assert-Equal 'no failure when the fetch script exits 0' '' (Invoke-FetchShelfPackages 'C:\repo' 'C:\x' { param($a) 0 })
Assert-Equal 'a failed fetch names the script and its exit code' $true ((Invoke-FetchShelfPackages 'C:\repo' 'C:\x' { param($a) 1 }) -like '*fetch-shelf-packages.mjs exited 1*')
Assert-Equal 'the store-shot script never builds a book package' 0 @(Select-String -Path (Join-Path $PSScriptRoot 'store-screenshots.ps1') -Pattern 'BookBuilder -- build|dotnet run').Count
$push =Get-FullShelfPushCommands 'uk.example.app' @([pscustomobject]@{ Id = 'abc'; Path = 'C:\x\story-v1.dino' })
Assert-Equal 'the app data is cleared first' 'shell pm clear uk.example.app' ($push[0] -join ' ')
Assert-Equal 'the book is pushed to the temp folder' 'push C:\x\story-v1.dino /data/local/tmp/full-shelf/abc.dino' ($push[4] -join ' ')
Assert-Equal 'then copied into the app files folder by run-as' 'shell run-as uk.example.app cp /data/local/tmp/full-shelf/abc.dino files/full-shelf/abc.dino' ($push[5] -join ' ')
Assert-Equal 'the temp folder is removed last' 'shell rm -rf /data/local/tmp/full-shelf' ($push[-1] -join ' ')
Assert-Equal 'every command goes through when adb is quiet' '' (Invoke-FullShelfPush $push { param($a) 'Success' })
Assert-Equal 'a run-as refusal stops the push and is named' $true ((Invoke-FullShelfPush $push { param($a) if ($a -contains 'run-as') { 'run-as: package not debuggable' } else { '' } }) -like "*run-as*failed*")
Assert-Equal 'a capital-F Failed from pm clear stops the push' $true ((Invoke-FullShelfPush $push { param($a) 'Failed' }) -like "*pm clear*failed*")
Assert-Equal 'an installed debug app is no problem' '' (Get-AppNotInstalledProblem 'uk.example.app' { param($a) "package:uk.example.app`r`n" })
Assert-Equal 'a missing app is named before pm clear or run-as run' $true ((Get-AppNotInstalledProblem 'uk.example.app' { param($a) '' }) -like '*not installed*')
Assert-Equal 'a longer package name is not the app' $true ((Get-AppNotInstalledProblem 'uk.example.app' { param($a) 'package:uk.example.app.dbg' }) -like '*not installed*')

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-store-screenshots: all passed.'
exit 0
