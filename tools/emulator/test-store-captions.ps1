<#
.SYNOPSIS
  Self-test of store-captions.ps1 and store-size-check.ps1 (Slice 291 item 9, Slice 433 item 1): the pure rules, the caption data, the crop arithmetic, and one real render per form of a synthetic PNG with the exact output sizes and the palette asserted. No device. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'store-captions.ps1')
. (Join-Path $PSScriptRoot 'store-size-check.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { Write-Host "FAIL $Name : expected '$Expected', got '$Actual'"; $script:failed++ }
}
function Assert-Throws([string]$Name, [scriptblock]$Block) {
    try { & $Block; Write-Host "FAIL $Name : no error"; $script:failed++ } catch { }
}

function Get-RawHash([string]$Path) { return [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([IO.File]::ReadAllBytes($Path))) }

Assert-Equal 'a plain line is fine' '' (Test-CaptionLine 'Picture books, read aloud')
Assert-Equal 'the note caption with "New" is fine' '' (Test-CaptionLine 'New books on the shelf')
Assert-Equal 'empty is rejected' 'empty' (Test-CaptionLine '  ')
Assert-Equal 'a price is rejected' 'looks like a price' (Test-CaptionLine 'Only $4.99')
Assert-Equal 'a call to action is rejected' "contains 'download'" (Test-CaptionLine 'Download now')
Assert-Equal 'best is rejected' "contains 'best'" (Test-CaptionLine 'The best stories')
Assert-Equal 'free is rejected' "contains 'free'" (Test-CaptionLine 'Free stories')
Assert-Equal 'a word that only contains a banned one is fine' '' (Test-CaptionLine 'Topsy tales')
Assert-Equal 'a long line is rejected' 'longer than 45 characters' (Test-CaptionLine ('x' * 46))

# The frames: exact store sizes, and the Play phone band stays under the 20% Play allows.
Assert-Equal 'phone is the Play phone size' '1080x1920' "$((Get-FrameSpec 'phone').Width)x$((Get-FrameSpec 'phone').Height)"
Assert-Equal 'tablet is the Play tablet size (true 9:16)' '1440x2560' "$((Get-FrameSpec 'tablet').Width)x$((Get-FrameSpec 'tablet').Height)"
Assert-Equal 'iphone-6.9 size' '1320x2868' "$((Get-FrameSpec 'iphone-6.9').Width)x$((Get-FrameSpec 'iphone-6.9').Height)"
Assert-Equal 'ipad-13 is landscape' '2752x2064' "$((Get-FrameSpec 'ipad-13').Width)x$((Get-FrameSpec 'ipad-13').Height)"
Assert-Equal 'the Play phone band is at most 20%' $true ((Get-FrameSpec 'phone').Band -le 0.20)
Assert-Equal 'the Play tablet has no caption band' 0 (Get-FrameSpec 'tablet').Band
Assert-Equal 'band is 15% of 1920' 288 (Get-BandHeight 1920 0.15)
Assert-Throws 'an unknown form has no frame' { Get-FrameSpec 'watch' }
Assert-Equal 'shot key from a file name' '1-picture' (Get-ShotKey 'store-dino-phone-1-picture.png' 'dino' 'phone')
Assert-Equal 'another form is no shot' $null (Get-ShotKey 'store-dino-tablet-1-picture.png' 'dino' 'phone')
Assert-Equal 'the Japanese extra shot uses the language caption' '4-language' (Get-CaptionKey '4-language-ja')
Assert-Equal 'a plain shot key is its own caption key' '6-ages' (Get-CaptionKey '6-ages')
$orderedShots = @('store-dino-phone-5-shelf.png', 'store-dino-phone-4-language-ja.png', 'store-dino-phone-4-language.png') | Sort-Object { Get-ShotOrderKey $_ }
Assert-Equal 'the Spanish language shot comes before the Japanese one, then the shelf' 'store-dino-phone-4-language.png,store-dino-phone-4-language-ja.png,store-dino-phone-5-shelf.png' ($orderedShots -join ',')
Assert-Equal 'an iOS form name with a dot is a shot key prefix' '3-record' (Get-ShotKey 'store-dino-iphone-6.9-3-record.png' 'dino' 'iphone-6.9')

# Get-ShotCrop: width fill, status bar off the top, navigation bar and a small remainder off the bottom; never stretch or letterbox, never cut more than the form allows.
Assert-Equal 'an exact Android fit: only the status bar is cut' '63 1632' ((Get-ShotCrop 1080 1695 1080 1920 288 63 0 0.01) -join ' ')
Assert-Equal 'an exact fit with a navigation bar' '63 1632' ((Get-ShotCrop 1080 1759 1080 1920 288 63 64 0.01) -join ' ')
Assert-Equal 'a tablet without a band' '63 2560' ((Get-ShotCrop 1440 2623 1440 2560 0 63 0 0.01) -join ' ')
Assert-Equal 'an iPhone shot: status bar off, the rest from the bottom' '186 2581' ((Get-ShotCrop 1320 2868 1320 2868 287 186 0 0.06) -join ' ')
Assert-Equal 'an iPad shot: a scale of 1 keeps the width' '48 1920' ((Get-ShotCrop 2752 2064 2752 2064 144 48 0 0.06) -join ' ')
Assert-Equal 'a wider shot is scaled by the width, not stretched' '31 1632' ((Get-ShotCrop 2160 1710 1080 960 144 31 0 0.06) -join ' ')
Assert-Throws 'a shot too short would be stretched or letterboxed' { Get-ShotCrop 1080 1500 1080 1920 288 63 0 0.01 }
Assert-Throws 'a shot that leaves no room for the status bar' { Get-ShotCrop 1080 1640 1080 1920 288 63 0 0.01 }
Assert-Throws 'a tall phone shot would lose too much of the app' { Get-ShotCrop 1080 2400 1080 1920 288 63 0 0.01 }
Assert-Equal 'the same tall shot is allowed where the form allows it' '63 1632' ((Get-ShotCrop 1080 1800 1080 1920 288 63 0 0.06) -join ' ')

# frame-info.json overrides the defaults.
$work = Join-Path ([IO.Path]::GetTempPath()) ("rtst-caps-" + [guid]::NewGuid().ToString('N'))
try {
    $null = New-Item -ItemType Directory -Force $work
    Assert-Equal 'no frame-info: the form default status bar' 63 (Get-FrameBars $work 'phone').StatusPx
    [IO.File]::WriteAllText((Join-Path $work 'frame-info.json'), '{ "statusBarPx": 80, "navBarPx": 48 }')
    $bars = Get-FrameBars $work 'phone'
    Assert-Equal 'frame-info: status bar' 80 $bars.StatusPx
    Assert-Equal 'frame-info: navigation bar' 48 $bars.NavPx
} finally { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }

# The data file: every Dino shot has a caption in every listing language that passes the rules; English is the note's text.
$captions = Get-Content (Join-Path $PSScriptRoot '..\..\docs\store-listings\screenshots\captions.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$expected = [ordered]@{ '1-picture' = 'Picture books, read aloud'; '2-voices' = 'A different voice for every character'; '3-record' = 'Record your voice. Be the wolf.'; '4-language' = 'Stories in 15 languages'; '5-shelf' = 'New books on the shelf'; '6-ages' = 'For ages 3 to 9. No ads.' }
foreach ($k in $expected.Keys) { Assert-Equal "caption $k" $expected[$k] (Get-CaptionLine $captions 'dino' $k 'en') }
$listingLangs = 'en', 'ar', 'da', 'de', 'es', 'fa', 'fi', 'fr', 'ja', 'nb', 'nl', 'pt', 'ru', 'sv', 'zh'
foreach ($lang in $listingLangs) { foreach ($k in $expected.Keys) { Assert-Equal "caption $k in $lang passes the rules" '' (Test-CaptionLine ([string]$captions.dino.$k.$lang)) } }
Assert-Throws 'a missing language fails' { Get-CaptionLine $captions 'dino' '1-picture' 'xx' }
foreach ($lang in $listingLangs) { $family = Get-CaptionFamily $lang; Assert-Equal "a caption font exists for $lang" $true ($null -ne $family) }
# Every caption has a glyph in its language's font (a missing one makes GDI+ draw a fallback font); Fredoka has no Cyrillic, so the check must catch it.
foreach ($lang in $listingLangs) { foreach ($k in $expected.Keys) { Assert-CaptionGlyphs $lang ([string]$captions.dino.$k.$lang) } }
Assert-Equal 'Fredoka has no Cyrillic (the reason Russian uses Andika)' ([string]$captions.dino.'1-picture'.ru -replace ' |,', '' | ForEach-Object { ($_.ToCharArray() | Select-Object -Unique) -join '' }) (Get-MissingGlyphsInFile (Join-Path $FontFolder 'Fredoka-Medium.ttf') ([string]$captions.dino.'1-picture'.ru -replace ' |,', ''))
Assert-Equal 'Russian uses Andika' 'Andika-Regular.ttf' (Get-CaptionFontFile 'ru')
Assert-Throws 'a missing glyph fails' { Assert-CaptionGlyphs 'en' ([string][char]0x044F) }
Assert-Throws 'a missing glyph in a system font fails' { Assert-CaptionGlyphs 'zh' ([string][char]0xE000) }

# Font fit: a caption too long for one line wraps to two lines at the same font size as a short one (never shrinks while two lines fit).
$probe = New-Object System.Drawing.Bitmap(10, 10)
$pg = [System.Drawing.Graphics]::FromImage($probe)
$pf = New-Object System.Drawing.StringFormat
try {
    $family = Get-CaptionFamily 'en'
    $shortFont = Get-FittingFont $pg $family 'Stories in 15 languages' 1016 288 0.28 $pf
    $longFont = Get-FittingFont $pg $family 'A different voice for every character' 1016 288 0.28 $pf
    Assert-Equal 'a long caption keeps the short caption font size' $shortFont.Size $longFont.Size
    $oneLine = $pg.MeasureString('Stories in 15 languages', $shortFont, 1016, $pf).Height
    $twoLines = $pg.MeasureString('A different voice for every character', $longFont, 1016, $pf).Height
    Assert-Equal 'the long caption wraps to a second line' $true ($twoLines -gt $oneLine * 1.5)
    $shortFont.Dispose(); $longFont.Dispose()
} finally { $pf.Dispose(); $pg.Dispose(); $probe.Dispose() }

# One real render per form from a synthetic raw shot: exact output size, 24-bit (no alpha), the raw file untouched.
function New-SolidPng([string]$Path, [int]$W, [int]$H, [System.Drawing.Color]$Color) {
    $bmp = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g =[System.Drawing.Graphics]::FromImage($bmp); $g.Clear($Color); $g.Dispose()
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}
# True when the pixel is the band colour, white, or a blend of the two on the line between them (what an anti-aliased text edge makes): the band holds no other colour.
function Test-PaletteBlend([System.Drawing.Color]$C) {
    $span = 255 - $BandColor.R
    $t = ($C.R - $BandColor.R) / $span
    if ($t -lt -0.02 -or $t -gt 1.02) { return $false }
    return ([math]::Abs($C.G - ($BandColor.G + $t * (255 - $BandColor.G))) -le 6) -and ([math]::Abs($C.B - ($BandColor.B + $t * (255 - $BandColor.B))) -le 6)
}
$red = [System.Drawing.Color]::FromArgb(255, 200, 30, 30)
$cases = @(
    @{ Form = 'phone'; W = 1080; H = 1695; Text = 'A different voice for every character'; Band = 288 },
    @{ Form = 'tablet'; W = 1440; H = 2623; Text = ''; Band = 0 },
    @{ Form = 'iphone-6.9'; W = 1320; H = 2868; Text = 'Stories in 15 languages'; Band = 287 },
    @{ Form = 'ipad-13'; W = 2752; H = 2064; Text = 'Stories in 15 languages'; Band = 103 }
)
$work = Join-Path ([IO.Path]::GetTempPath()) ("rtst-caps-" + [guid]::NewGuid().ToString('N'))
try {
    $null = New-Item -ItemType Directory -Force $work
    foreach ($case in $cases) {
        $spec = Get-FrameSpec $case.Form
        $raw = Join-Path $work "store-dino-$($case.Form)-1-picture.png"
        New-SolidPng $raw $case.W $case.H $red
        $before = Get-RawHash $raw
        $out = Join-Path $work "out\$($case.Form).png"
        New-FramedShot $raw $out $case.Form $case.Text 'en' (@{ StatusPx = $spec.StatusPx; NavPx = $spec.NavPx })
        $img = [System.Drawing.Bitmap]::FromFile($out)
        try {
            Assert-Equal "$($case.Form): exact output size" "$($spec.Width)x$($spec.Height)" "$($img.Width)x$($img.Height)"
            Assert-Equal "$($case.Form): no alpha channel" 'Format24bppRgb' $img.PixelFormat
            $stray = 0; $screenWrong = 0
            for ($y = 0; $y -lt $img.Height; $y += 3) {
                for ($x = 0; $x -lt $img.Width; $x += 3) {
                    $c = $img.GetPixel($x, $y)
                    if ($y -lt $case.Band) { if (-not (Test-PaletteBlend $c)) { $stray++ } }
                    elseif ([math]::Abs($c.R - 200) -gt 2 -or [math]::Abs($c.G - 30) -gt 2 -or [math]::Abs($c.B - 30) -gt 2) { $screenWrong++ }
                }
            }
            Assert-Equal "$($case.Form): no band pixel outside the palette" 0 $stray
            Assert-Equal "$($case.Form): the screen fills everything under the band, edge to edge" 0 $screenWrong
            if ($case.Text) {
                Assert-Equal "$($case.Form): band corner is the app's Brand colour" ($BandColor.ToArgb()) ($img.GetPixel(2, 2).ToArgb())
                $lit = 0; for ($x = 20; $x -lt ($img.Width - 20); $x += 2) { for ($y = 10; $y -lt ($case.Band - 10); $y += 2) { if ($img.GetPixel($x, $y).G -gt 200) { $lit++ } } }
                Assert-Equal "$($case.Form): caption text is drawn in the band" $true ($lit -gt 20)
            }
        } finally { $img.Dispose() }
        Assert-Equal "$($case.Form): the raw file is untouched" $before (Get-RawHash $raw)
        Assert-Equal "$($case.Form): store-size-check passes the file" 0 @(Get-ImageProblems (Get-ImageFacts $out) (Get-SizeRule $spec.Target)).Count
    }
    # A shot that does not fit fails; nothing is stretched or letterboxed.
    $bad = Join-Path $work 'store-dino-phone-2-voices.png'
    New-SolidPng $bad 1080 1500 $red
    Assert-Throws 'a too-short phone shot fails the render' { New-FramedShot $bad (Join-Path $work 'out\bad.png') 'phone' 'x' 'en' @{ StatusPx = 63; NavPx = 0 } }
    Assert-Equal 'a failed render leaves no file' $false (Test-Path (Join-Path $work 'out\bad.png'))

    # store-size-check: size, ratio and alpha.
    $rgba = Join-Path $work 'rgba.png'
    $bmp = New-Object System.Drawing.Bitmap(1080, 1920, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb); $bmp.Save($rgba, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    $problems = @(Get-ImageProblems (Get-ImageFacts $rgba) (Get-SizeRule 'play-phone'))
    Assert-Equal 'an RGBA PNG fails on alpha' 1 $problems.Count
    Assert-Equal 'the alpha problem is named' $true ($problems[0] -like '*alpha*')
    $wrong = Join-Path $work 'wrong.png'
    New-SolidPng $wrong 1080 2400 $red
    $problems = @(Get-ImageProblems (Get-ImageFacts $wrong) (Get-SizeRule 'play-phone'))
    Assert-Equal 'a 1080x2400 file fails on size and ratio' 2 $problems.Count
    Assert-Equal 'a 1600x2560 tablet shot (the old live size) fails' 2 @(Get-ImageProblems ([pscustomobject]@{ Width = 1600; Height = 2560; Alpha = $false }) (Get-SizeRule 'play-tablet')).Count
    Assert-Equal 'a non-image fails' 1 @(Get-ImageProblems $null (Get-SizeRule 'play-phone')).Count
    Assert-Equal 'the feature graphic size' '1024x500' "$((Get-SizeRule 'play-feature').Width)x$((Get-SizeRule 'play-feature').Height)"
    Assert-Equal 'the Duo sizes' '1398x2034,2007x2853' ((@((Get-SizeRule 'appstore-duo-outer'), (Get-SizeRule 'appstore-duo-inner')) | ForEach-Object { "$($_.Width)x$($_.Height)" }) -join ',')
    Assert-Throws 'an unknown target is an error' { Get-SizeRule 'play-watch' }
    # A JPEG header is read too (feature graphic).
    $jpg = Join-Path $work 'feature.jpg'
    $bmp = New-Object System.Drawing.Bitmap(1024, 500, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb); $bmp.Save($jpg, [System.Drawing.Imaging.ImageFormat]::Jpeg); $bmp.Dispose()
    Assert-Equal 'a 1024x500 JPEG passes the feature graphic check' 0 @(Get-ImageProblems (Get-ImageFacts $jpg) (Get-SizeRule 'play-feature')).Count

    # Preview videos: the encoding limits on a parsed ffprobe answer.
    $good = '{"streams":[{"codec_type":"video","codec_name":"h264","profile":"High","width":886,"height":1920,"r_frame_rate":"30/1","bit_rate":"11000000"},{"codec_type":"audio","codec_name":"aac","channels":2,"sample_rate":"48000","bit_rate":"256000"}],"format":{"duration":"20.0"}}' | ConvertFrom-Json
    Assert-Equal 'a good iPhone preview passes' 0 @(Get-VideoProblems $good (Get-SizeRule 'preview-iphone') 27500000).Count
    $slow = '{"streams":[{"codec_type":"video","codec_name":"h264","profile":"Main","width":1080,"height":1920,"r_frame_rate":"60/1"},{"codec_type":"audio","codec_name":"mp3","channels":1,"sample_rate":"22050","bit_rate":"128000"}],"format":{"duration":"40.0"}}' | ConvertFrom-Json
    Assert-Equal 'a wrong size, profile, 60 fps, 40 s, low bit rate and bad audio each fail' 9 @(Get-VideoProblems $slow (Get-SizeRule 'preview-iphone') 1000000).Count
    $lowAudio = '{"streams":[{"codec_type":"video","codec_name":"h264","profile":"High","width":886,"height":1920,"r_frame_rate":"30/1","bit_rate":"11000000"},{"codec_type":"audio","codec_name":"aac","channels":2,"sample_rate":"48000","bit_rate":"128000"}],"format":{"duration":"20.0"}}' | ConvertFrom-Json
    Assert-Equal 'a 128 kbps audio track fails on its own' 1 @(Get-VideoProblems $lowAudio (Get-SizeRule 'preview-iphone') 27500000).Count
    Assert-Equal 'a rational frame rate' 29.97 ([math]::Round((ConvertTo-Fps '30000/1001'), 2))
} finally { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-store-captions: all passed.'
exit 0
