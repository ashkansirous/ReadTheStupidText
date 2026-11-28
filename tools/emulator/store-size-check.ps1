<#
.SYNOPSIS
  Slice 433 item 1: fails any store image or preview video whose pixel size, ratio, alpha or encoding does not match the exact row of the note's Sizes table. Every upload step runs it first (store-captions.ps1 runs it on its own output).
.DESCRIPTION
  Targets (docs/plan/notes/2026-11-23-dino-store-screenshots-and-video.md, Sizes; checked on each store's own help pages 2026-11-23):
    play-phone 1080x1920 (no alpha) | play-tablet 1440x2560 (no alpha) | play-feature 1024x500 (no alpha; JPEG or 24-bit PNG)
    appstore-iphone-6.9 1320x2868 | appstore-ipad-13 2752x2064 (landscape) | appstore-duo-outer 1398x2034 | appstore-duo-inner 2007x2853 (all App Store images: no alpha)
    preview-iphone 886x1920 and preview-duo 886x1920 | preview-ipad 1600x1200 (video: H.264, 15-30 s, at most 30 fps, about 10-12 Mbps, stereo AAC 44.1 or 48 kHz, at most 500 MB; needs ffprobe)
    Microsoft Store (Slice 445): windows-screenshot (PNG, long side 1366-3840 and short side 768-2160 in either orientation, at most 50 MB each, 10 files per listing language (more under the path only warns), alpha allowed) |
    windows-thumbnail 1920x1080 PNG (alpha allowed) | windows-trailer 1920x1080 MP4 (H.264 High, AAC-LC 48 kHz, under 2 GB; over 60 s only prints a warning; needs ffprobe)
  The ratio is the size's own ratio, so a file that has the right pixel count in the wrong shape fails twice, and the message names what the file is.
  Usage: pwsh -NoProfile -File tools/emulator/store-size-check.ps1 -Target play-phone -Path <file or folder>   (exit 0 = every file matches, 1 = a file does not, 2 = no file or unknown target)
#>
param(
    [string]$Target,
    [string]$Path
)
$ErrorActionPreference = 'Stop'

# Target -> exact size and kind. Image rows forbid alpha; video rows carry the encoding limits.
$script:SizeRules = @{
    'play-phone'          = @{ Width = 1080; Height = 1920; Kind = 'image' }
    'play-tablet'         = @{ Width = 1440; Height = 2560; Kind = 'image' }
    'play-feature'        = @{ Width = 1024; Height = 500;  Kind = 'image' }
    'appstore-iphone-6.9' = @{ Width = 1320; Height = 2868; Kind = 'image' }
    'appstore-ipad-13'    = @{ Width = 2752; Height = 2064; Kind = 'image' }
    'appstore-duo-outer'  = @{ Width = 1398; Height = 2034; Kind = 'image' }
    'appstore-duo-inner'  = @{ Width = 2007; Height = 2853; Kind = 'image' }
    'preview-iphone'      = @{ Width = 886;  Height = 1920; Kind = 'video' }
    'preview-duo'         = @{ Width = 886;  Height = 1920; Kind = 'video' }
    'preview-ipad'        = @{ Width = 1600; Height = 1200; Kind = 'video' }
    # Slice 445: Microsoft Store (learn.microsoft.com/windows/apps/publish/publish-your-app/msix/screenshots-and-images). Screenshots are a range, either orientation, alpha allowed.
    'windows-screenshot'  = @{ Kind = 'range-image'; MinLong = 1366; MinShort = 768; MaxLong = 3840; MaxShort = 2160; MaxBytes = 50MB; MaxFiles = 10; Formats = @('png'); AllowAlpha = $true }
    'windows-thumbnail'   = @{ Width = 1920; Height = 1080; Kind = 'image'; Formats = @('png'); AllowAlpha = $true }
    'windows-trailer'     = @{ Width = 1920; Height = 1080; Kind = 'trailer' }
}
$script:TrailerLimits = @{ MaxSeconds = 60; MaxBytes = 2GB; SampleRate = 48000 }
$script:PreviewLimits = @{ MinSeconds = 15; MaxSeconds = 30; MaxFps = 30; MinMbps = 9.5; MaxMbps = 12.5; MaxBytes = 500MB }

# Pure: the rule of a target (throws on an unknown one).
function Get-SizeRule([string]$Target) {
    if (-not $script:SizeRules.ContainsKey($Target)) { throw "Unknown target '$Target' (known: $(($script:SizeRules.Keys | Sort-Object) -join ', '))." }
    return $script:SizeRules[$Target]
}

# Pure: width, height and alpha of a PNG or JPEG file from its header bytes ($null when it is neither). Alpha = colour type 4 or 6, or a tRNS chunk.
function Get-ImageFacts([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 33 -and $bytes[0] -eq 0x89 -and $bytes[1] -eq 0x50 -and $bytes[2] -eq 0x4E -and $bytes[3] -eq 0x47) { return Get-PngFacts $bytes }
    if ($bytes.Length -ge 4 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xD8) { return Get-JpegFacts $bytes }
    return $null
}

function Read-UInt32BigEndian([byte[]]$Bytes, [int]$Offset) {
    return ([int64]$Bytes[$Offset] -shl 24) -bor ([int64]$Bytes[$Offset + 1] -shl 16) -bor ([int64]$Bytes[$Offset + 2] -shl 8) -bor [int64]$Bytes[$Offset + 3]
}

function Get-PngFacts([byte[]]$Bytes) {
    $colorType = [int]$Bytes[25]
    $alpha = ($colorType -eq 4 -or $colorType -eq 6)
    $offset = 8
    while ($offset + 8 -le $Bytes.Length) {
        $length = Read-UInt32BigEndian $Bytes $offset
        $name = [Text.Encoding]::ASCII.GetString($Bytes, $offset + 4, 4)
        if ($name -eq 'tRNS') { $alpha = $true }
        if ($name -eq 'IDAT') { break }
        $offset += 12 + $length
    }
    return [pscustomobject]@{ Format = 'png'; Width = [int](Read-UInt32BigEndian $Bytes 16); Height = [int](Read-UInt32BigEndian $Bytes 20); Alpha = $alpha }
}

function Get-JpegFacts([byte[]]$Bytes) {
    $offset = 2
    while ($offset + 9 -lt $Bytes.Length) {
        if ($Bytes[$offset] -ne 0xFF) { $offset++; continue }
        $marker = $Bytes[$offset + 1]
        if ($marker -ge 0xC0 -and $marker -le 0xC2) {
            $height = ([int]$Bytes[$offset + 5] -shl 8) -bor [int]$Bytes[$offset + 6]
            $width = ([int]$Bytes[$offset + 7] -shl 8) -bor [int]$Bytes[$offset + 8]
            return [pscustomobject]@{ Format = 'jpeg'; Width = $width; Height = $height; Alpha = $false }
        }
        $offset += 2 + (([int]$Bytes[$offset + 2] -shl 8) -bor [int]$Bytes[$offset + 3])
    }
    return $null
}

# Pure: the problems of one image's facts against a rule (empty = fine).
function Get-ImageProblems($Facts, $Rule) {
    if (-not $Facts) { return @('not a PNG or JPEG file') }
    $problems = @()
    if ($Rule.Formats -and $Rule.Formats -notcontains $Facts.Format) { $problems += "format is $($Facts.Format), the store takes $($Rule.Formats -join ' or ')" }
    if ($Rule.Kind -eq 'range-image') { return $problems + @(Get-RangeProblems $Facts $Rule) }
    if ($Facts.Width -ne $Rule.Width -or $Facts.Height -ne $Rule.Height) { $problems += "size is $($Facts.Width) x $($Facts.Height), the store takes exactly $($Rule.Width) x $($Rule.Height)" }
    if (($Facts.Width * $Rule.Height) -ne ($Facts.Height * $Rule.Width)) { $problems += "ratio is $([math]::Round($Facts.Width / $Facts.Height, 4)), it must be $([math]::Round($Rule.Width / $Rule.Height, 4))" }
    if ($Facts.Alpha -and -not $Rule.AllowAlpha) { $problems += 'has an alpha channel (the stores take none)' }
    return $problems
}

# Pure: a range image's problems, either orientation (the longer side against MinLong/MaxLong, the shorter against MinShort/MaxShort; alpha is the rule's own AllowAlpha).
function Get-RangeProblems($Facts, $Rule) {
    $long = [math]::Max($Facts.Width, $Facts.Height)
    $short = [math]::Min($Facts.Width, $Facts.Height)
    $problems = @()
    if ($long -lt $Rule.MinLong -or $short -lt $Rule.MinShort) { $problems += "size is $($Facts.Width) x $($Facts.Height), the store needs at least $($Rule.MinLong) x $($Rule.MinShort) (either orientation)" }
    if ($long -gt $Rule.MaxLong -or $short -gt $Rule.MaxShort) { $problems += "size is $($Facts.Width) x $($Facts.Height), the store takes at most $($Rule.MaxLong) x $($Rule.MaxShort) (either orientation)" }
    if ($Facts.Alpha -and -not $Rule.AllowAlpha) { $problems += 'has an alpha channel' }
    return $problems
}

# Pure: the problems of one trailer's ffprobe data (a parsed -of json object) against the trailer rule (empty = fine): exact size, H.264 High, AAC-LC 48 kHz, under 2 GB (length is Get-TrailerWarnings).
function Get-TrailerProblems($Probe, $Rule, [int64]$Bytes) {
    $video = @($Probe.streams | Where-Object { $_.codec_type -eq 'video' }) | Select-Object -First 1
    $audio = @($Probe.streams | Where-Object { $_.codec_type -eq 'audio' }) | Select-Object -First 1
    if (-not $video) { return @('no video stream') }
    $limits = $script:TrailerLimits
    $problems = @()
    if ([int]$video.width -ne $Rule.Width -or [int]$video.height -ne $Rule.Height) { $problems += "size is $($video.width) x $($video.height), the trailer takes exactly $($Rule.Width) x $($Rule.Height)" }
    if ($video.codec_name -ne 'h264') { $problems += "codec is $($video.codec_name), it must be h264" }
    if ($video.profile -ne 'High') { $problems += "H.264 profile is '$($video.profile)', it must be High" }
    if ($Bytes -ge $limits.MaxBytes) { $problems += 'not under 2 GB' }
    if (-not $audio) { return $problems + @('no audio stream (the trailer takes AAC-LC 48 kHz)') }
    if ($audio.codec_name -ne 'aac') { $problems += "audio is $($audio.codec_name), it must be aac" }
    elseif ($audio.profile -and $audio.profile -ne 'LC') { $problems += "AAC profile is '$($audio.profile)', it must be LC" }
    if ([int]$audio.sample_rate -ne $limits.SampleRate) { $problems += "audio sample rate is $($audio.sample_rate), it must be $($limits.SampleRate)" }
    return $problems
}

# Pure: the warnings of one trailer (never a failure): Microsoft recommends 60 s or less, it does not forbid longer.
function Get-TrailerWarnings($Probe) {
    $seconds = [double]$Probe.format.duration
    if ($seconds -gt $script:TrailerLimits.MaxSeconds) { return @("length is $([math]::Round($seconds, 1)) s, Microsoft recommends $($script:TrailerLimits.MaxSeconds) s or less") }
    return @()
}

# Pure: the problems of one preview video's ffprobe data (a parsed -of json object) against a rule and the preview limits (empty = fine).
function Get-VideoProblems($Probe, $Rule, [int64]$Bytes) {
    $video = @($Probe.streams | Where-Object { $_.codec_type -eq 'video' }) | Select-Object -First 1
    $audio = @($Probe.streams | Where-Object { $_.codec_type -eq 'audio' }) | Select-Object -First 1
    if (-not $video) { return @('no video stream') }
    $limits = $script:PreviewLimits
    $problems = @()
    if ([int]$video.width -ne $Rule.Width -or [int]$video.height -ne $Rule.Height) { $problems += "size is $($video.width) x $($video.height), the preview takes exactly $($Rule.Width) x $($Rule.Height)" }
    if ($video.codec_name -ne 'h264') { $problems += "codec is $($video.codec_name), it must be h264" }
    $fps = ConvertTo-Fps ([string]$video.r_frame_rate)
    if ($fps -gt $limits.MaxFps + 0.01) { $problems += "frame rate is $([math]::Round($fps, 2)), at most $($limits.MaxFps)" }
    $seconds = [double]$Probe.format.duration
    if ($seconds -lt $limits.MinSeconds -or $seconds -gt $limits.MaxSeconds) { $problems += "length is $([math]::Round($seconds, 1)) s, it must be $($limits.MinSeconds) to $($limits.MaxSeconds) s" }
    if ($video.profile -ne 'High') { $problems += "H.264 profile is '$($video.profile)', it must be High" }
    # The video stream's own bit rate when ffprobe gives it (the file size mixes in the audio track and the container); the whole file's rate otherwise.
    $mbps = if ($video.bit_rate) { [double]$video.bit_rate / 1000000 } elseif ($seconds -gt 0) { $Bytes * 8 / $seconds / 1000000 } else { $null }
    if ($null -ne $mbps -and ($mbps -lt $limits.MinMbps -or $mbps -gt $limits.MaxMbps)) { $problems += "video bit rate is $([math]::Round($mbps, 1)) Mbps, it must be about 10 to 12" }
    if ($Bytes -gt $limits.MaxBytes) { $problems += 'larger than 500 MB' }
    if (-not $audio) { $problems += 'no audio stream (Apple wants stereo AAC 256 kbps, silent track included)' }
    else {
        if ($audio.codec_name -ne 'aac') { $problems += "audio is $($audio.codec_name), it must be aac" }
        if ([int]$audio.channels -ne 2) { $problems += "audio has $($audio.channels) channel(s), it must be stereo" }
        if (-not $audio.bit_rate) { $problems += 'audio bit rate cannot be read (Apple wants AAC 256 kbps)' }
        elseif ([int64]$audio.bit_rate -lt 250000) { $problems += "audio bit rate is $([math]::Round([int64]$audio.bit_rate / 1000)) kbps, it must be 256 kbps" }
        if (@(44100, 48000) -notcontains [int]$audio.sample_rate) { $problems += "audio sample rate is $($audio.sample_rate), it must be 44100 or 48000" }
    }
    return $problems
}

# Pure: frames per second from ffprobe's "30/1" or "30000/1001" text.
function ConvertTo-Fps([string]$Text) {
    $parts = $Text -split '/'
    if ($parts.Count -eq 2 -and [double]$parts[1] -ne 0) { return [double]$parts[0] / [double]$parts[1] }
    return [double]$parts[0]
}

function Get-VideoProbe([string]$Path) {
    $ffprobe = Get-Command ffprobe -ErrorAction SilentlyContinue
    if (-not $ffprobe) { throw 'ffprobe is not on the PATH (needed to check a video).' }
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'   # Windows PowerShell 5.1 turns ffprobe's stderr text into an error under 'Stop'
    $json = & $ffprobe.Source -v error -show_entries 'stream=codec_type,codec_name,profile,width,height,r_frame_rate,bit_rate,channels,sample_rate:format=duration' -of json $Path 2>$null
    $ErrorActionPreference = $previous
    try { return ($json -join "`n") | ConvertFrom-Json } catch { return $null }   # no-fallback-needed: a file ffprobe cannot read is reported per file as a problem
}

# The files of a target under a path: the path itself, or the PNG/JPEG (images) or MP4/MOV (videos) files of a folder.
function Get-CheckFiles([string]$Path, [string]$Kind) {
    if (Test-Path $Path -PathType Leaf) { return @(Get-Item $Path) }
    $patterns = if ($Kind -eq 'video' -or $Kind -eq 'trailer') { @('*.mp4', '*.mov') } else { @('*.png', '*.jpg', '*.jpeg') }
    return @(Get-ChildItem $Path -File -Include $patterns -Recurse | Sort-Object FullName)
}

# The problems of one file against its target's rule (dispatch on the rule's kind; a range image also has a file-size cap).
function Get-FileProblems($File, $Rule) {
    if ($Rule.Kind -eq 'video' -or $Rule.Kind -eq 'trailer') {
        $probe = Get-VideoProbe $File.FullName
        if (-not $probe) { return @('ffprobe cannot read this file (corrupt or not a video)') }
        if ($Rule.Kind -eq 'video') { return @(Get-VideoProblems $probe $Rule $File.Length) }
        return @(Get-TrailerProblems $probe $Rule $File.Length)
    }
    $problems = @(Get-ImageProblems (Get-ImageFacts $File.FullName) $Rule)
    if ($Rule.MaxBytes -and $File.Length -gt $Rule.MaxBytes) { $problems += "larger than $($Rule.MaxBytes / 1MB) MB" }
    return $problems
}

if ($MyInvocation.InvocationName -eq '.') { return }   # dot-sourced by a self-test: functions only

if (-not $Target -or -not $Path) { Write-Host 'STORE SIZE CHECK: -Target and -Path are required.'; exit 2 }
$rule = Get-SizeRule $Target
$files = @(Get-CheckFiles $Path $rule.Kind)
if ($files.Count -eq 0) { Write-Host "STORE SIZE CHECK: no file for $Target under $Path."; exit 2 }
$failed = 0
# A warning, not a failure: the store's cap is per listing language and a folder can hold several languages' sets.
if ($rule.MaxFiles -and $files.Count -gt $rule.MaxFiles) { Write-Host "STORE SIZE CHECK WARNING ($Target): $($files.Count) files under the path, the store takes at most $($rule.MaxFiles) per listing language." }
foreach ($file in $files) {
    $problems = @(Get-FileProblems $file $rule)
    $label = if ($rule.Width) { "$($rule.Width)x$($rule.Height)" } else { 'range' }
    if ($rule.Kind -eq 'trailer' -and $problems.Count -eq 0) { foreach ($warning in @(Get-TrailerWarnings (Get-VideoProbe $file.FullName))) { Write-Host "STORE SIZE CHECK WARNING ($Target): $($file.Name): $warning" } }
    if ($problems.Count -eq 0) { Write-Host "STORE SIZE CHECK ok ($Target $label): $($file.Name)" }
    else { $failed++; Write-Host "STORE SIZE CHECK FAILED ($Target): $($file.Name): $($problems -join '; ')" }
}
if ($failed -gt 0) { Write-Host "STORE SIZE CHECK: $failed of $($files.Count) file(s) do not match; nothing is uploaded."; exit 1 }
exit 0
