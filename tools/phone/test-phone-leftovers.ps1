<#
.SYNOPSIS
  Self-test for the Slice 216 helpers in common.ps1: corrupt lease handling, the build path-length check, the shared build command, the lease note, the cold-read cache command.
  Run it in both `powershell` (5.1) and `pwsh`. Needs no device; the lease files go to a temp folder.
#>
$ErrorActionPreference = 'Stop'
if (-not $env:ANDROID_HOME -and -not $env:ANDROID_SDK_ROOT) { $env:ANDROID_HOME = $env:TEMP }
. (Join-Path $PSScriptRoot 'common.ps1')
$script:LeaseDir = Join-Path ([IO.Path]::GetTempPath()) ('rtst-leftovers-test-' + [guid]::NewGuid().ToString('N'))

$failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

try {
    # ---- item 7: a corrupt lease is named and removable; a valid one never is
    $key = 'leftovers-test'
    $null = New-Item -ItemType Directory -Force $script:LeaseDir
    Assert-Equal 'no file: not corrupt' $false (Test-LeaseFileCorrupt $key)
    Assert-Equal 'no file: nothing to clear' 'NotCorrupt' (Clear-CorruptLease $key)
    Assert-Equal 'valid json is not corrupt' $false (Test-LeaseTextCorrupt '{ "owner": "a", "taken": 1, "expires": 2 }')
    Assert-Equal 'broken json is corrupt' $true (Test-LeaseTextCorrupt '{ not json')
    Assert-Equal 'take a valid lease' $true (Request-Lease $key 'agent-a' 60)
    Assert-Equal 'valid lease: not corrupt' $false (Test-LeaseFileCorrupt $key)
    Assert-Equal 'valid lease: -Clear -Corrupt leaves it' 'NotCorrupt' (Clear-CorruptLease $key)
    Assert-Equal 'valid lease still held' 'agent-a' (Read-Lease $key).Owner
    Assert-Equal 'release' $true (Remove-Lease $key 'agent-a')
    $leasePath = Get-LeasePath $key
    Set-Content $leasePath '{ "owner": "old", "taken": 1, "expires": 2 }'
    Assert-Equal 'expired valid lease: -Clear -Corrupt leaves it' 'NotCorrupt' (Clear-CorruptLease $key)
    Assert-Equal 'expired valid lease file still there' $true (Test-Path $leasePath)
    Set-Content $leasePath '{ not json'
    Assert-Equal 'corrupt file is detected by key' $true (Test-LeaseFileCorrupt $key)
    $now = [DateTimeOffset]::new((Get-Item $leasePath).LastWriteTimeUtc).ToUnixTimeSeconds() + 3 * 3600 + 10 * 60
    $held = Read-Lease $key $now
    $text = Format-LeaseHolder $held $now
    Assert-Equal 'corrupt holder says corrupt' $true ($text -like 'a corrupt lease file*')
    Assert-Equal 'corrupt holder says its age' $true ($text -like '*written 3 h 10 min ago*')
    Assert-Equal 'corrupt holder says how to clear it' $true ($text -like '*lease.ps1 -Clear -Corrupt*')
    Assert-Equal 'emulator corrupt holder does not point at lease.ps1' $false ((Format-LeaseHolder $held $now 'emulator') -like '*lease.ps1*')
    Assert-Equal 'a normal holder keeps the old text' $true ((Format-LeaseHolder ([pscustomobject]@{ Owner = 'agent-b'; Expires = $now + 60 }) $now) -like "'agent-b' (until *)")
    Assert-Equal 'corrupt lease cleared' 'Cleared' (Clear-CorruptLease $key)
    Assert-Equal 'cleared: file gone' $false (Test-Path $leasePath)
    Assert-Equal 'cleared: phone is free' $null (Read-Lease $key $now)
    Assert-Equal 'span under an hour' '12 min' (Format-LeaseSpan 725)
    Assert-Equal 'span over an hour' '1 h 5 min' (Format-LeaseSpan 3900)
} finally {
    Remove-Item $script:LeaseDir -Recurse -Force -ErrorAction SilentlyContinue
}

# ---- item 8: a screen change prints the changed texts only, cut to 120 characters
$sigs = @('Hello world||id1|c=False|s=False', '|a description|id2|c=True|s=False', '||only.id|c=False|s=False')
Assert-Equal 'label: text first' 'Hello world' (Get-SignatureLabel $sigs[0])
Assert-Equal 'label: description when no text, checked state shown' 'a description [checked]' (Get-SignatureLabel $sigs[1])
Assert-Equal 'label: id when nothing else' 'only.id' (Get-SignatureLabel $sigs[2])
Assert-Equal 'short change line' 'Added: Hello world ; a description [checked] ; only.id' (Format-ScreenChangeLine 'Added' $sigs)
$wide = @(1..40 | ForEach-Object { "word$_||id$_|c=False|s=False" })
$cut = Format-ScreenChangeLine 'Added' $wide
Assert-Equal 'long change line is cut to 120' 120 $cut.Length
Assert-Equal 'cut line ends in an ellipsis' $true ($cut.EndsWith('...'))
Assert-Equal 'ids and flags are not printed' $false ($cut -match 'id\d|c=False')
$tapSource = Get-Content (Join-Path $PSScriptRoot 'tap.ps1') -Raw
Assert-Equal 'tap.ps1 prints the short line and logs the full one' $true (($tapSource -match 'Format-ScreenChangeLine') -and ($tapSource -match 'Write-ScreenChangeLog \$diff'))

# ---- item 6: the lease note says which length and why
Assert-Equal 'note: budget plus margin' '   phone lease: 25 min (a timed run needs 25 min: its budget plus the margin)' (Format-LeaseLengthNote 'a timed run' 25 25 0)
Assert-Equal 'note: own longer lease kept' $true ((Format-LeaseLengthNote 'a timed run' 90 25 90) -like '*your own lease still has 90 min, never shortened*')

# ---- item 3: the path-length check fails early, naming what to shorten
$short = 'D:\w\s1\.artifacts\rtst-aot'
Assert-Equal 'short path passes' $null (Test-BuildPathLength $short)
$long = 'D:\' + ('x' * 100) + '\.artifacts\rtst-aot'
$problem = Test-BuildPathLength $long
Assert-Equal 'estimate-only path warns' 'Warn' $problem.Level
$warned = $null; Assert-BuildPathLength $long -WarningVariable warned -WarningAction SilentlyContinue
Assert-Equal 'estimate-only path does not throw, warns' $true (@($warned).Count -eq 1)
Assert-Equal 'boundary passes' $null (Test-BuildPathLength ('x' * ($script:WindowsPathLimit - $script:DeepestMauiIntermediateChars)))
Assert-Equal 'one over warns' 'Warn' (Test-BuildPathLength ('x' * ($script:WindowsPathLimit - $script:DeepestMauiIntermediateChars + 1))).Level
$tooLong = 'D:\' + ('x' * 200) + '\.artifacts\rtst-aot'
Assert-Equal 'known-too-long path fails' 'Fail' (Test-BuildPathLength $tooLong).Level
$threw = $false; try { Assert-BuildPathLength $tooLong } catch { $threw = $_.Exception.Message -like 'path too long*' }
Assert-Equal 'Assert-BuildPathLength throws on known-too-long' $true $threw

# ---- item 5: one build command; both scripts use it
$pkg = 'uk.sirous.readthestupidtext.dbg'
$debugArgs = Get-PhoneBuildArguments 'P.csproj' 'A' $pkg 'android-arm64' $false
$aotArgs = Get-PhoneBuildArguments 'P.csproj' 'A' $pkg 'android-arm64' $true
Assert-Equal 'debug: no AOT flag' $false (($debugArgs -join ' ') -match 'RunAOTCompilation')
Assert-Equal 'aot: AOT flags added to the debug command' (($debugArgs + '-p:RunAOTCompilation=true' + '-p:AndroidEnableProfiledAot=false') -join ' ') ($aotArgs -join ' ')
Assert-Equal 'rid and package in the command' $true ((($debugArgs -join ' ') -match 'RuntimeIdentifier=android-arm64') -and (($debugArgs -join ' ') -match [regex]::Escape("ApplicationId=$pkg")))
Assert-Equal 'x64 rid for the emulator' $true ((Get-PhoneBuildArguments 'P' 'A' $pkg 'android-x64' $false) -contains '-p:RuntimeIdentifier=android-x64')
Assert-Equal 'quoting only where there are spaces' 'a "b c"' ((ConvertTo-ProcessArguments @('a', 'b c')) -join ' ')
foreach ($script in 'audio-timing.ps1', 'run-checks.ps1') {
    $source = Get-Content (Join-Path $PSScriptRoot $script) -Raw
    Assert-Equal "$script builds through Get-PhoneBuildArguments" $true ($source -match 'Get-PhoneBuildArguments')
    Assert-Equal "$script has no copy of the dotnet build flags" $false ($source -match '-p:EmbedAssembliesIntoApk')
    Assert-Equal "$script checks the path length" $true ($source -match 'Assert-BuildPathLength')
}

# ---- item 2 and 4: cold reads clear the sentence cache (dev package only); the timing log folder is the shared one
$command = New-SentenceCacheClearCommand $pkg
Assert-Equal 'clear command runs as the dev package' $true ($command -like "run-as $pkg sh -c 'rm -rf cache/sentences;*")
$refused = $false; try { New-SentenceCacheClearCommand 'uk.sirous.readthestupidtext' | Out-Null } catch { $refused = $true }
Assert-Equal 'store package is refused' $true $refused
$timing = Get-Content (Join-Path $PSScriptRoot 'audio-timing.ps1') -Raw
Assert-Equal 'audio-timing clears the cache before each cold read' $true ($timing -match 'Clear-SentenceCache \$Phone \$package')
Assert-Equal 'audio-timing logs go to Get-WorktreesRoot' $true ($timing -match "Join-Path \(Get-WorktreesRoot\) '\.phone'")


# ---- -Rates: "1,1" is two rates, not eleven; bad values are refused
Assert-Equal 'rates: "1,1" splits into two' '1 1' ((ConvertTo-RateList '1,1') -join ' ')
Assert-Equal 'rates: array of strings' '1 2' ((ConvertTo-RateList @('1', '2')) -join ' ')
Assert-Equal 'rates: array of doubles' '1.25 2' ((ConvertTo-RateList @(1.25, 2.0)) -join ' ')
Assert-Equal 'rates: single value' '1.5' ((ConvertTo-RateList '1.5') -join ' ')
foreach ($bad in '11', '0.4', '2.5', 'abc', '1,,x', '') {
    $refusedRate = $false; try { ConvertTo-RateList $bad | Out-Null } catch { $refusedRate = $true }
    Assert-Equal "rates: [$bad] refused" $true $refusedRate
}
if ($failed) { Write-Host "$failed phone-leftovers checks failed (PowerShell $($PSVersionTable.PSVersion))."; exit 1 }
Write-Host "All phone-leftovers checks pass (PowerShell $($PSVersionTable.PSVersion))."
exit 0
