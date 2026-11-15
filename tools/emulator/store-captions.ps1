<#
.SYNOPSIS
  Slice 291 item 9: put a one-line benefit caption on each store screenshot. The raw PNGs are never touched; the captioned copy has the same pixel size.
.DESCRIPTION
  Layout: a solid band across the top (15% of the height, under the 20% Google Play allows for text) holds the caption, centred, one or two lines. Every caption starts from the same start size (a fraction of the band height); a line that is too long wraps to a second line at that size, and only a line that does not fit in two lines gets a smaller font, so a long or translated line can end up smaller than its siblings;
  the raw shot is scaled to fit the rest and centred on the same background colour. System.Drawing only (no extra package).
  Caption lines live in docs/store-listings/screenshots/captions.json: { "<app>": { "<n>-<screen>": { "<lang>": "line" } } }. A shot with no line for the language fails the run (never a blank band).
  Raw shots: docs/store-listings/screenshots/<app>/<form>/store-<app>-<form>-<n>-<screen>.png. Output: <app>/<form>-captioned/<lang>/ with the same file names.
  Run: pwsh -NoProfile -File tools/emulator/store-captions.ps1 -App dino -Form phone -Lang en
  Rules for the lines (stores.md, Captions): no price, ranking, "best", "new" or call to action; under ~40 characters.
#>
param(
    [ValidateSet('rtst', 'dino')][string]$App,
    [ValidateSet('phone', 'tablet')][string]$Form,
    [ValidatePattern('^[a-z]{2,3}(-[A-Za-z0-9]+)?$')][string]$Lang = 'en',
    [string]$ShotsRoot,
    [string]$CaptionsFile,
    [string]$ContactSheet   # optional: also write a small contact sheet PNG of the captioned shots here
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $ShotsRoot) { $ShotsRoot = Join-Path $repoRoot 'docs\store-listings\screenshots' }
if (-not $CaptionsFile) { $CaptionsFile = Join-Path $ShotsRoot 'captions.json' }
Add-Type -AssemblyName System.Drawing

$BandFraction = 0.15            # of the image height; Play allows text on at most 20% of the image
$MaxCaptionLength = 45          # the skill says under ~40; the accepted longest line is 41
$BandColor = [System.Drawing.ColorTranslator]::FromHtml('#2F5D50')
$TextColor = [System.Drawing.Color]::White
$FontName = 'Segoe UI Semibold'
$StartSizeFraction = 0.2        # the font size (of the band height) every caption starts from, and shrinks from only when two lines do not fit; small enough that a 45-character line fits on two lines
$MaxLineHeights = 3.0          # a font fits when its text is at most two lines tall (a line is about 1.33 x the size, three lines about 4 x)
$RightToLeftLangs = @('fa', 'ar')
# Currency signs by code point (pound U+00A3, euro U+20AC) so the file stays ASCII and reads the same in Windows PowerShell 5.1 and pwsh.
$PricePattern = '[$' + [char]0x00A3 + [char]0x20AC + ']|\d\s*(usd|eur|gbp)\b'
$ForbiddenWords = @('best', 'new', 'top', 'free', 'sale', 'discount', 'download', 'try now', '#1', 'million')

# Pure: the caption problems for one line (empty = fine). Prices, rankings and calls to action are out (Apple 2.3.7, Play policy).
function Test-CaptionLine([string]$Line) {
    if ([string]::IsNullOrWhiteSpace($Line)) { return 'empty' }
    if ($Line.Length -gt $MaxCaptionLength) { return "longer than $MaxCaptionLength characters" }
    if ($Line -match $PricePattern) { return 'looks like a price' }
    foreach ($w in $ForbiddenWords) { if ($Line -match ('(?i)(^|\W)' + [regex]::Escape($w) + '(\W|$)')) { return "contains '$w'" } }
    return ''
}

# Pure: the band height in pixels for an image height.
function Get-BandHeight([int]$ImageHeight) { return [int][math]::Round($ImageHeight * $BandFraction) }

# Pure: the rectangle (x, y, w, h) the raw shot is scaled into: fit the area under the band, keep the ratio, centre horizontally.
function Get-ShotRect([int]$Width, [int]$Height, [int]$SrcWidth, [int]$SrcHeight) {
    $band = Get-BandHeight $Height
    $areaH = $Height - $band
    $scale = [math]::Min([double]$Width / $SrcWidth, [double]$areaH / $SrcHeight)
    $w = [int][math]::Round($SrcWidth * $scale); $h = [int][math]::Round($SrcHeight * $scale)
    return @([int][math]::Round(($Width - $w) / 2), $band, $w, $h)
}

# Pure: the caption for a shot key and language from the parsed data file; throws when missing.
function Get-CaptionLine($Captions, [string]$App, [string]$Key, [string]$Lang) {
    $line = $Captions.$App.$Key.$Lang
    if (-not $line) { throw "No $Lang caption for $App $Key in captions.json." }
    $problem = Test-CaptionLine $line
    if ($problem) { throw "Caption for $App $Key ($Lang) rejected: $problem." }
    return [string]$line
}

# Pure: the shot key (<n>-<screen>) from a raw file name store-<app>-<form>-<n>-<screen>.png.
function Get-ShotKey([string]$FileName, [string]$App, [string]$Form) {
    $prefix = "store-$App-$Form-"
    if (-not $FileName.StartsWith($prefix) -or -not $FileName.EndsWith('.png')) { return $null }
    return $FileName.Substring($prefix.Length, $FileName.Length - $prefix.Length - 4)
}

# Largest font size (down to 10 px) at which the text fits $MaxWidth in at most two lines of the band.
function Get-FittingFont([System.Drawing.Graphics]$G, [string]$Text, [int]$MaxWidth, [int]$BandHeight, [System.Drawing.StringFormat]$Format) {
    for ($size = [int]($BandHeight * $StartSizeFraction); $size -ge 10; $size--) {
        $font = New-Object System.Drawing.Font($FontName, $size, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
        if ($font.Name -ne $FontName) { $installed = $font.Name; $font.Dispose(); throw "Font '$FontName' is not installed (Windows substituted '$installed'); install it before making captions." }
        $measured = $G.MeasureString($Text, $font, $MaxWidth, $Format)
        if ($measured.Height -le ($BandHeight - 24) -and $measured.Height -le ($size * $MaxLineHeights)) { return $font }
        $font.Dispose()
    }
    throw "Caption does not fit the band: $Text"
}

# Writes the captioned copy of $RawPath to $OutPath (same pixel size). The raw file is only read.
function New-CaptionedShot([string]$RawPath, [string]$OutPath, [string]$Text, [bool]$Rtl) {
    $src = [System.Drawing.Image]::FromFile($RawPath)
    try {
        $w = $src.Width; $h = $src.Height
        $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $g = $null; $fmt = $null; $font = $null; $brush = $null
        try {
            $bmp.SetResolution($src.HorizontalResolution, $src.VerticalResolution)
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $g.Clear($BandColor)
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
            $r = Get-ShotRect $w $h $src.Width $src.Height
            $g.DrawImage($src, $r[0], $r[1], $r[2], $r[3])
            $fmt = New-Object System.Drawing.StringFormat
            $fmt.Alignment = [System.Drawing.StringAlignment]::Center
            $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
            if ($Rtl) { $fmt.FormatFlags = [System.Drawing.StringFormatFlags]::DirectionRightToLeft }
            $band = Get-BandHeight $h
            $margin = [int]($w * 0.06)
            $font = Get-FittingFont $g $Text ($w - 2 * $margin) $band $fmt
            $rect = New-Object System.Drawing.RectangleF($margin, 0, ($w - 2 * $margin), $band)
            $brush = New-Object System.Drawing.SolidBrush($TextColor)
            $g.DrawString($Text, $font, $brush, $rect, $fmt)
            $g.Dispose(); $g = $null
            $null = New-Item -ItemType Directory -Force (Split-Path $OutPath -Parent)
            $bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
        } finally {
            foreach ($o in @($brush, $font, $fmt, $g, $bmp)) { if ($o) { $o.Dispose() } }
        }
    } finally { $src.Dispose() }
}

# A small contact sheet: the captioned shots side by side, each scaled to $CellHeight tall.
function New-CaptionSheet([string[]]$Paths, [string]$OutPath, [int]$CellHeight = 640) {
    $images = @($Paths | ForEach-Object { [System.Drawing.Image]::FromFile($_) })
    try {
        $widths = @($images | ForEach-Object { [int]($_.Width * $CellHeight / $_.Height) })
        $total = [int](($widths | Measure-Object -Sum).Sum + 10 * ($images.Count + 1))
        $bmp = New-Object System.Drawing.Bitmap($total, ($CellHeight + 20))
        $g = $null
        try {
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $g.Clear([System.Drawing.Color]::White)
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $x = 10
            for ($i = 0; $i -lt $images.Count; $i++) { $g.DrawImage($images[$i], $x, 10, $widths[$i], $CellHeight); $x += $widths[$i] + 10 }
            $null = New-Item -ItemType Directory -Force (Split-Path $OutPath -Parent)
            $bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
        } finally { if ($g) { $g.Dispose() }; $bmp.Dispose() }
    } finally { $images | ForEach-Object { $_.Dispose() } }
}

if ($MyInvocation.InvocationName -eq '.') { return }   # dot-sourced by a self-test: functions only

if (-not $App -or -not $Form) { Write-Host 'STORE CAPTIONS: -App and -Form are required.'; exit 1 }
$rawDir = Join-Path $ShotsRoot "$App\$Form"
$outDir = Join-Path $ShotsRoot "$App\$Form-captioned\$Lang"
$captions = Get-Content $CaptionsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$raws = @(Get-ChildItem $rawDir -Filter 'store-*.png' -File | Sort-Object Name)
if ($raws.Count -eq 0) { Write-Host "STORE CAPTIONS: no raw shots in $rawDir."; exit 1 }

# Validate every shot first (a missing or rejected line stops the run before anything is written), then render into a temp folder and swap it in only when all succeeded.
$jobs = @()
foreach ($raw in $raws) {
    $key = Get-ShotKey $raw.Name $App $Form
    if (-not $key) { throw "Raw shot $($raw.Name) does not match store-$App-$Form-<n>-<screen>.png; rename or remove it." }
    $jobs += [pscustomobject]@{ Raw = $raw; Text = (Get-CaptionLine $captions $App $key $Lang) }
}
if ($jobs.Count -eq 0) { Write-Host "STORE CAPTIONS: no shot named store-$App-$Form-<n>-<screen>.png in $rawDir."; exit 1 }
$tempDir = Join-Path (Split-Path $outDir -Parent) (".captions-tmp-" + [guid]::NewGuid().ToString('N'))
$backupDir = $null
try {
    foreach ($job in $jobs) { New-CaptionedShot $job.Raw.FullName (Join-Path $tempDir $job.Raw.Name) $job.Text ($RightToLeftLangs -contains $Lang) }
    # Swap: old set to a backup, new set in, backup removed. A failed move puts the old set back.
    if (Test-Path $outDir) { $backupDir = Join-Path (Split-Path $outDir -Parent) (".captions-old-" + [guid]::NewGuid().ToString('N')); Move-Item $outDir $backupDir }
    try { Move-Item $tempDir $outDir }
    catch { if ($backupDir -and -not (Test-Path $outDir)) { Move-Item $backupDir $outDir; $backupDir = $null }; throw }
    if ($backupDir) { Remove-Item $backupDir -Recurse -Force; $backupDir = $null }
} finally { if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force } }
$written = @($jobs | ForEach-Object { Join-Path $outDir $_.Raw.Name })
foreach ($job in $jobs) { Write-Host "STORE CAPTIONS: $($job.Raw.Name) -> $outDir ($($job.Text))" }
if ($ContactSheet) { New-CaptionSheet $written $ContactSheet; Write-Host "STORE CAPTIONS: contact sheet $ContactSheet" }
exit 0
