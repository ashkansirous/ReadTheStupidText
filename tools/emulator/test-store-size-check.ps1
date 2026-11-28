<#
.SYNOPSIS
  Self-test of the pure functions of store-size-check.ps1 (Slice 445): dot-sources it, no ffprobe, no device. Covers the Microsoft Store targets (windows-screenshot range, windows-thumbnail, windows-trailer) and that the older exact rows still behave.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'store-size-check.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ($Expected -ne $Actual) { Write-Host "FAIL $Name : expected '$Expected', got '$Actual'"; $script:failed++ }
}
function Assert-Throws([string]$Name, [scriptblock]$Block) {
    try { & $Block; Write-Host "FAIL $Name : no error"; $script:failed++ } catch { }
}

# A PNG header (signature + IHDR, no pixel data) is all Get-ImageFacts reads.
function New-PngFile([string]$Path, [int]$Width, [int]$Height, [int]$ColorType) {
    $bytes = New-Object System.Collections.Generic.List[byte]
    $bytes.AddRange([byte[]](0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52))
    foreach ($value in $Width, $Height) { $bytes.AddRange([byte[]]@((($value -shr 24) -band 255), (($value -shr 16) -band 255), (($value -shr 8) -band 255), ($value -band 255))) }
    $bytes.AddRange([byte[]](8, $ColorType, 0, 0, 0, 0, 0, 0, 0))
    [IO.File]::WriteAllBytes($Path, $bytes.ToArray())
}
function Get-PngProblems([int]$Width, [int]$Height, [int]$ColorType, [string]$Target) {
    $file = Join-Path $script:tmp "t-$Width-$Height-$ColorType.png"
    New-PngFile $file $Width $Height $ColorType
    return @(Get-ImageProblems (Get-ImageFacts $file) (Get-SizeRule $Target)).Count
}
function New-Probe([int]$Width, [int]$Height, [string]$Profile, [string]$Audio, [int]$Rate, [string]$AudioProfile, [double]$Seconds) {
    $streams = @([pscustomobject]@{ codec_type = 'video'; codec_name = 'h264'; profile = $Profile; width = $Width; height = $Height })
    if ($Audio) { $streams += [pscustomobject]@{ codec_type = 'audio'; codec_name = $Audio; profile = $AudioProfile; sample_rate = "$Rate"; channels = 2 } }
    return [pscustomobject]@{ streams = $streams; format = [pscustomobject]@{ duration = "$Seconds" } }
}
function Get-TrailerProblemCount($Probe, [int64]$Bytes = 5MB) { return @(Get-TrailerProblems $Probe (Get-SizeRule 'windows-trailer') $Bytes).Count }

$script:tmp = Join-Path ([IO.Path]::GetTempPath()) ("store-size-check-test-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $script:tmp | Out-Null
try {
    # windows-screenshot: a range, either orientation, alpha allowed (RGBA = colour type 6).
    Assert-Equal 'the shipped 1920x1080 RGBA shot passes' 0 (Get-PngProblems 1920 1080 6 'windows-screenshot')
    Assert-Equal 'the minimum 1366x768 passes' 0 (Get-PngProblems 1366 768 2 'windows-screenshot')
    Assert-Equal 'the maximum 3840x2160 passes' 0 (Get-PngProblems 3840 2160 2 'windows-screenshot')
    Assert-Equal 'portrait 768x1366 passes' 0 (Get-PngProblems 768 1366 2 'windows-screenshot')
    Assert-Equal 'one pixel under the minimum width fails' 1 (Get-PngProblems 1365 768 2 'windows-screenshot')
    Assert-Equal 'one pixel under the minimum height fails' 1 (Get-PngProblems 1366 767 2 'windows-screenshot')
    Assert-Equal 'over the maximum fails' 1 (Get-PngProblems 3841 2160 2 'windows-screenshot')
    Assert-Equal 'a portrait phone shot is a valid desktop range' 0 (Get-PngProblems 1080 1920 2 'windows-screenshot')
    Assert-Equal 'a small portrait shot fails' 1 (Get-PngProblems 700 1200 2 'windows-screenshot')
    Assert-Equal 'the screenshot rule takes png only' 'png' ((Get-SizeRule 'windows-screenshot').Formats -join ',')

    # windows-thumbnail: exactly 1920x1080 PNG, alpha allowed.
    Assert-Equal 'thumbnail 1920x1080 RGBA passes' 0 (Get-PngProblems 1920 1080 6 'windows-thumbnail')
    Assert-Equal 'thumbnail 1920x1081 fails (size and ratio)' 2 (Get-PngProblems 1920 1081 2 'windows-thumbnail')
    Assert-Equal 'thumbnail 1080x1920 fails twice (size and ratio)' 2 (Get-PngProblems 1080 1920 2 'windows-thumbnail')

    # The older rows still forbid alpha.
    Assert-Equal 'play-phone still refuses alpha' 1 (Get-PngProblems 1080 1920 6 'play-phone')
    Assert-Equal 'play-phone exact RGB passes' 0 (Get-PngProblems 1080 1920 2 'play-phone')

    # windows-trailer: 1920x1080, H.264 High, AAC-LC 48 kHz, 60 s or less, under 2 GB.
    Assert-Equal 'a good trailer passes' 0 (Get-TrailerProblemCount (New-Probe 1920 1080 'High' 'aac' 48000 'LC' 20))
    Assert-Equal 'exactly 60 s passes' 0 (Get-TrailerProblemCount (New-Probe 1920 1080 'High' 'aac' 48000 'LC' 60))
    Assert-Equal '75 s is no failure (Microsoft only recommends 60 s)' 0 (Get-TrailerProblemCount (New-Probe 1920 1080 'High' 'aac' 48000 'LC' 75))
    Assert-Equal '75 s prints one warning' 1 @(Get-TrailerWarnings (New-Probe 1920 1080 'High' 'aac' 48000 'LC' 75)).Count
    Assert-Equal '60 s prints no warning' 0 @(Get-TrailerWarnings (New-Probe 1920 1080 'High' 'aac' 48000 'LC' 60)).Count
    Assert-Equal 'wrong size fails' 1 (Get-TrailerProblemCount (New-Probe 1280 720 'High' 'aac' 48000 'LC' 20))
    Assert-Equal 'Main profile fails' 1 (Get-TrailerProblemCount (New-Probe 1920 1080 'Main' 'aac' 48000 'LC' 20))
    Assert-Equal '44.1 kHz audio fails' 1 (Get-TrailerProblemCount (New-Probe 1920 1080 'High' 'aac' 44100 'LC' 20))
    Assert-Equal 'mp3 audio fails' 1 (Get-TrailerProblemCount (New-Probe 1920 1080 'High' 'mp3' 48000 '' 20))
    Assert-Equal 'HE-AAC fails' 1 (Get-TrailerProblemCount (New-Probe 1920 1080 'High' 'aac' 48000 'HE-AAC' 20))
    Assert-Equal 'no audio stream fails' 1 (Get-TrailerProblemCount (New-Probe 1920 1080 'High' '' 0 '' 20))
    Assert-Equal '2 GB or more fails' 1 (Get-TrailerProblemCount (New-Probe 1920 1080 'High' 'aac' 48000 'LC' 20) 2GB)
    Assert-Equal 'no video stream fails' 1 (@(Get-TrailerProblems ([pscustomobject]@{ streams = @(); format = [pscustomobject]@{ duration = '5' } }) (Get-SizeRule 'windows-trailer') 1MB)).Count

    # Dispatch and file picking.
    New-Item -ItemType Directory -Path (Join-Path $script:tmp 'folder') | Out-Null
    New-PngFile (Join-Path $script:tmp 'folder\a.png') 1920 1080 6
    [IO.File]::WriteAllText((Join-Path $script:tmp 'folder\b.mp4'), 'x')
    Assert-Equal 'a trailer folder holds the mp4 only' 'b.mp4' ((Get-CheckFiles (Join-Path $script:tmp 'folder') 'trailer').Name -join ',')
    Assert-Equal 'a screenshot folder holds the png only' 'a.png' ((Get-CheckFiles (Join-Path $script:tmp 'folder') 'range-image').Name -join ',')
    Assert-Equal 'the screenshot rule allows 10 files' 10 (Get-SizeRule 'windows-screenshot').MaxFiles
    Assert-Throws 'an unknown target throws' { Get-SizeRule 'windows-nonsense' }

    # Get-FileProblems and the main loop (run as the CI runs it: a child process on the script, reading its exit code and output).
    $jpeg = Join-Path $script:tmp 'shot.jpg'
    [IO.File]::WriteAllBytes($jpeg, [byte[]](0xFF, 0xD8, 0xFF, 0xC0, 0, 17, 8, 4, 56, 7, 128, 3, 1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1))
    Assert-Equal 'a JPEG is refused by windows-screenshot (png only)' 1 @(Get-FileProblems (Get-Item $jpeg) (Get-SizeRule 'windows-screenshot')).Count
    $big = Join-Path $script:tmp 'big.png'
    New-PngFile $big 1920 1080 2
    $sized = [pscustomobject]@{ FullName = $big; Name = 'big.png'; Length = 51MB }   # only the length matters, so no 51 MB file is written
    Assert-Equal 'a PNG over 50 MB fails' 1 @(Get-FileProblems $sized (Get-SizeRule 'windows-screenshot')).Count
    $many = Join-Path $script:tmp 'many'
    New-Item -ItemType Directory -Path $many | Out-Null
    1..11 | ForEach-Object { New-PngFile (Join-Path $many "s$_.png") 1920 1080 6 }
    $shell = (Get-Process -Id $PID).Path
    $output = & $shell -NoProfile -File (Join-Path $PSScriptRoot 'store-size-check.ps1') -Target windows-screenshot -Path $many
    Assert-Equal '11 PNGs in a folder still exit 0' 0 $LASTEXITCODE
    Assert-Equal '11 PNGs print the per-language warning' 1 @($output | Where-Object { $_ -like '*WARNING*per listing language*' }).Count
    Assert-Equal 'every shot of the folder is labelled range' 11 @($output | Where-Object { $_ -like '*ok (windows-screenshot range)*' }).Count
    $output = & $shell -NoProfile -File (Join-Path $PSScriptRoot 'store-size-check.ps1') -Target windows-screenshot -Path $jpeg
    Assert-Equal 'a JPEG file exits 1' 1 $LASTEXITCODE
    if (Get-Command ffprobe -ErrorAction SilentlyContinue) {
        $truncated = Join-Path $script:tmp 'truncated.mp4'
        [IO.File]::WriteAllText($truncated, 'not a video')
        $output = & $shell -NoProfile -File (Join-Path $PSScriptRoot 'store-size-check.ps1') -Target windows-trailer -Path $truncated
        Assert-Equal 'an unreadable trailer exits 1 with a per-file line' 1 $LASTEXITCODE
        Assert-Equal 'an unreadable trailer names the file' 1 @($output | Where-Object { $_ -like '*FAILED*truncated.mp4*' }).Count
    }
}
finally { Remove-Item -Recurse -Force $script:tmp -ErrorAction SilentlyContinue }

if ($script:failed -gt 0) { Write-Host "STORE SIZE CHECK SELF-TEST: $script:failed failure(s)."; exit 1 }
Write-Host 'STORE SIZE CHECK SELF-TEST: all passed.'
exit 0
