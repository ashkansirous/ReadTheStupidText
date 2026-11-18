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
$content = Join-Path ([IO.Path]::GetTempPath()) "missing-stems-test-$PID"
try {
    foreach ($b in @(@('built', 'id1', 'Store'), @('unbuilt', 'id2', 'Store'), @('starter', 'id3', 'Starter'))) {
        $null = New-Item -ItemType Directory -Force (Join-Path $content "content\$($b[0])")
        Set-Content (Join-Path $content "content\$($b[0])\book.json") "{ `"id`": `"$($b[1])`", `"kind`": `"$($b[2])`" }"
    }
    $null = New-Item -ItemType File -Force (Join-Path $content 'store\id1\story-v3.dino')
    Assert-Equal 'only an unbuilt store book is missing a package' 'unbuilt' ((Get-MissingPackageStems (Join-Path $content 'content') (Join-Path $content 'store')) -join ',')
} finally { Remove-Item $content -Recurse -Force -ErrorAction SilentlyContinue }
$pulls = @(Get-LfsPullCommands @('aaa', 'bbb'))
Assert-Equal 'one LFS pull per stem' 2 $pulls.Count
Assert-Equal 'the pull is for exactly the stem high-res folder, with an empty exclude' 'lfs pull --include content/dino/aaa/high-res/** --exclude ' ($pulls[0] -join ' ')
Assert-Equal 'no pull failure when git succeeds' '' (Invoke-LfsPulls @('aaa', 'bbb') { param($a) 0 })
Assert-Equal 'a failed pull names the stem' $true ((Invoke-LfsPulls @('aaa', 'bbb') { param($a) if ($a -contains 'content/dino/bbb/high-res/**') { 1 } else { 0 } }) -like '*bbb*exited 1*')
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
