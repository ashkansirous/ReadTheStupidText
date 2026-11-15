<#
.SYNOPSIS
  Self-test of store-captions.ps1 (Slice 291 item 9): the pure rules, the accepted English lines, and one real render of a synthetic PNG. No device. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'store-captions.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { Write-Host "FAIL $Name : expected '$Expected', got '$Actual'"; $script:failed++ }
}
function Assert-Throws([string]$Name, [scriptblock]$Block) {
    try { & $Block; Write-Host "FAIL $Name : no error"; $script:failed++ } catch { }
}

function Get-RawHash([string]$Path) { return [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([IO.File]::ReadAllBytes($Path))) }

Assert-Equal 'a plain line is fine' '' (Test-CaptionLine 'A voice for every character')
Assert-Equal 'empty is rejected' 'empty' (Test-CaptionLine '  ')
Assert-Equal 'a price is rejected' 'looks like a price' (Test-CaptionLine 'Only $4.99')
Assert-Equal 'a call to action is rejected' "contains 'download'" (Test-CaptionLine 'Download now')
Assert-Equal 'best is rejected' "contains 'best'" (Test-CaptionLine 'The best stories')
Assert-Equal 'a word that only contains a banned one is fine' '' (Test-CaptionLine 'Newspaper tales')
Assert-Equal 'a long line is rejected' 'longer than 45 characters' (Test-CaptionLine ('x' * 46))

Assert-Equal 'band is 15% of 2400' 360 (Get-BandHeight 2400)
Assert-Equal 'shot rect: 1080x2400 into the area under the band' '81 360 918 2040' ((Get-ShotRect 1080 2400 1080 2400) -join ' ')
Assert-Equal 'shot key from a file name' '1-reading' (Get-ShotKey 'store-dino-phone-1-reading.png' 'dino' 'phone')
Assert-Equal 'another form is no shot' $null (Get-ShotKey 'store-dino-tablet-1-shelf.png' 'dino' 'phone')

# The data file: every Dino shot of the skill's table has an English line that passes the rules.
$captions = Get-Content (Join-Path $PSScriptRoot '..\..\docs\store-listings\screenshots\captions.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$expected = [ordered]@{ '1-reading' = 'A voice for every character'; '2-record' = 'Record your voice as any character'; '3-language' = '15 languages'; '4-grownups' = 'Grown-ups choose the books, behind a gate'; '5-shelf' = 'Classic tales, retold gently' }
foreach ($k in $expected.Keys) { Assert-Equal "caption $k" $expected[$k] (Get-CaptionLine $captions 'dino' $k 'en') }
Assert-Throws 'a missing language fails' { Get-CaptionLine $captions 'dino' '1-reading' 'de' }

# Font fit: a caption too long for one line wraps to two lines at the same font size as a short one (never shrinks while two lines fit).
$probe = New-Object System.Drawing.Bitmap(10, 10)
$pg = [System.Drawing.Graphics]::FromImage($probe)
$pf = New-Object System.Drawing.StringFormat
try {
    $shortFont = Get-FittingFont $pg '15 languages' 1016 360 $pf
    $longFont = Get-FittingFont $pg 'Grown-ups choose the books, behind a gate' 1016 360 $pf
    Assert-Equal 'a long caption keeps the short caption font size' $shortFont.Size $longFont.Size
    $oneLine = $pg.MeasureString('15 languages', $shortFont, 1016, $pf).Height
    $twoLines = $pg.MeasureString('Grown-ups choose the books, behind a gate', $longFont, 1016, $pf).Height
    Assert-Equal 'the long caption wraps to a second line' $true ($twoLines -gt $oneLine * 1.5)
    $shortFont.Dispose(); $longFont.Dispose()
} finally { $pf.Dispose(); $pg.Dispose(); $probe.Dispose() }

# One real render: same pixel size, the raw file untouched, the band coloured, the shot below it.
$work = Join-Path ([IO.Path]::GetTempPath()) ("rtst-caps-" + [guid]::NewGuid().ToString('N'))
try {
    $null = New-Item -ItemType Directory -Force $work
    $raw = Join-Path $work 'store-dino-phone-1-reading.png'
    $bmp = New-Object System.Drawing.Bitmap(300, 600)
    $g = [System.Drawing.Graphics]::FromImage($bmp); $g.Clear([System.Drawing.Color]::FromArgb(255, 200, 30, 30)); $g.Dispose()
    $bmp.Save($raw, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    $before = Get-RawHash $raw
    $out = Join-Path $work 'out\store-dino-phone-1-reading.png'
    New-CaptionedShot $raw $out 'A voice for every character' $false
    $img = [System.Drawing.Bitmap]::FromFile($out)
    try {
        Assert-Equal 'output has the raw pixel size' '300x600' "$($img.Width)x$($img.Height)"
        Assert-Equal 'band corner is the band colour' ($BandColor.ToArgb()) ($img.GetPixel(2, 2).ToArgb())
        $mid = $img.GetPixel(150, 400)
        Assert-Equal 'the shot shows under the band' '200,30,30' "$($mid.R),$($mid.G),$($mid.B)"
        $lit = 0; for ($x = 20; $x -lt 280; $x += 2) { for ($y = 10; $y -lt 80; $y += 2) { if ($img.GetPixel($x, $y).R -gt 200 -and $img.GetPixel($x, $y).G -gt 200) { $lit++ } } }
        Assert-Equal 'caption text is drawn in the band' $true ($lit -gt 20)
    } finally { $img.Dispose() }
    Assert-Equal 'the raw file is untouched' $before (Get-RawHash $raw)
} finally { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }

if ($script:failed -gt 0) { Write-Host "$($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-store-captions: all passed.'
exit 0
