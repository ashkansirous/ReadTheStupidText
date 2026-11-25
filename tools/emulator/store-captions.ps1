<#
.SYNOPSIS
  Slice 433 item 1: the store frame of a Dino screenshot. The app screen fills the picture edge to edge at the exact store size, with a caption band across the top in the app's own colour and font. The raw PNGs are never touched.
.DESCRIPTION
  Frame (the note docs/plan/notes/2026-11-23-dino-store-screenshots-and-video.md, Constraints and Decisions): no device frame, no status bar, no letterbox, no stretch and no colour outside the app's palette.
  Palette and font are read from the Dino app's resources (read only): the band is Brand #5B57E8 (src/Dino/DinoReader.App/App.xaml, key Brand), the text is white, the font is Fredoka Medium
  (Resources/Fonts/Fredoka-Medium.ttf, which has no Cyrillic: Russian uses Andika-Regular.ttf, the app's reading font; Farsi and Arabic use Vazirmatn.ttf, which the app uses for them; every caption is checked glyph by glyph against its font and the run fails on a missing one, never a silent fallback; Japanese and Chinese use the Windows UI fonts Yu Gothic UI Semibold and Microsoft YaHei UI, because Fredoka has no such glyphs).
  Layout: a solid band across the top (Play phone 15% of the height, under the 20% Play allows; the App Store sets 10% and 7%) holds the caption, centred, one or two lines. The raw shot is scaled to the output WIDTH (one scale for both axes, so never stretched) and cropped
  at the top by the status bar and at the bottom by the navigation bar plus a small remainder; it fills exactly the area under the band. A shot whose ratio does not fit (the app would be cropped by more than the form allows, or would have to be letterboxed) FAILS the run (Get-ShotCrop): nothing is stretched, letterboxed or cut beyond the allowance.
  The forms (Get-FrameSpec): phone = Play phone 1080 x 1920 (the emulator runs with `wm size` set by store-screenshots.ps1 so the shot fits exactly); tablet = Play tablet 1440 x 2560, NO caption (large-screen guidance), written to tablet-framed/;
  iphone-6.9 = App Store iPhone 1320 x 2868; ipad-13 = App Store iPad 13" landscape 2752 x 2064.
  The status bar and navigation bar heights of the raw shots are read from <raw folder>/frame-info.json ({ "statusBarPx": n, "navBarPx": n }, written by store-screenshots.ps1 from the device); without it the form's default is used.
  Caption lines live in docs/store-listings/screenshots/captions.json: { "<app>": { "<n>-<screen>": { "<lang>": "line" } } }. A shot with no line for the language fails the run (never a blank band).
  Raw shots: docs/store-listings/screenshots/<app>/<form>/store-<app>-<form>-<n>-<screen>.png. Output: <app>/<form>-captioned/<lang>/ with the same file names (tablet: <app>/tablet-framed/).
  Run: pwsh -NoProfile -File tools/emulator/store-captions.ps1 -App dino -Form phone -Lang en
  Rules for the lines (stores.md, Captions): no price, ranking, "best", "free" or call to action; at most 45 characters.
#>
param(
    [ValidateSet('rtst', 'dino')][string]$App,
    [ValidateSet('phone', 'tablet', 'iphone-6.9', 'ipad-13')][string]$Form,
    [ValidatePattern('^[a-z]{2,3}(-[A-Za-z0-9]+)?$')][string]$Lang = 'en',
    [string]$ShotsRoot,
    [string]$CaptionsFile,
    [string]$ContactSheet   # optional: also write a small contact sheet PNG of the framed shots here
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $ShotsRoot) { $ShotsRoot = Join-Path $repoRoot 'docs\store-listings\screenshots' }
if (-not $CaptionsFile) { $CaptionsFile = Join-Path $ShotsRoot 'captions.json' }
Add-Type -AssemblyName System.Drawing

$MaxCaptionLength = 45
# The app's palette (src/Dino/DinoReader.App/App.xaml: Brand). The only colours the caption band may hold are this and white (and the blends of the two the text edge makes).
$BandColor = [System.Drawing.ColorTranslator]::FromHtml('#5B57E8')
$TextColor = [System.Drawing.Color]::White
$FontFolder = Join-Path $repoRoot 'src\Dino\DinoReader.App\Resources\Fonts'
$MaxLineHeights = 3.0          # a font fits when its text is at most two lines tall (a line is about 1.33 x the size, three lines about 4 x)
$RightToLeftLangs = @('fa', 'ar')
# Currency signs by code point (pound U+00A3, euro U+20AC) so the file stays ASCII and reads the same in Windows PowerShell 5.1 and pwsh.
$PricePattern = '[$' + [char]0x00A3 + [char]0x20AC + ']|\d\s*(usd|eur|gbp)\b'
# Apple 2.3.7 and Play policy: no price, ranking or call to action. ("new" is allowed: the note's own caption is "New books on the shelf".)
$ForbiddenWords = @('best', 'top', 'free', 'sale', 'discount', 'download', 'try now', '#1', 'million')

# The frame sizes (Get-FrameSpec, Get-BandHeight) live in store-frames.ps1, which store-screenshots.ps1 shares.
. (Join-Path $PSScriptRoot 'store-frames.ps1')

# Pure: the caption problems for one line (empty = fine). Prices, rankings and calls to action are out (Apple 2.3.7, Play policy).
function Test-CaptionLine([string]$Line) {
    if ([string]::IsNullOrWhiteSpace($Line)) { return 'empty' }
    if ($Line.Length -gt $MaxCaptionLength) { return "longer than $MaxCaptionLength characters" }
    if ($Line -match $PricePattern) { return 'looks like a price' }
    foreach ($w in $ForbiddenWords) { if ($Line -match ('(?i)(^|\W)' + [regex]::Escape($w) + '(\W|$)')) { return "contains '$w'" } }
    return ''
}

# Pure: which part of a raw shot goes under the band. The raw shot is scaled to the output width (one scale for both axes), the status bar is cut off the top, the navigation bar and a small remainder off the bottom, and what is left must fill the area under
# the band exactly. Returns @(srcY, srcHeight) in raw pixels. Throws when the shot cannot fill the area without a stretch, a letterbox or a cut beyond $MaxBottomFraction of its height, or when the status bar would show.
function Get-ShotCrop([int]$SrcWidth, [int]$SrcHeight, [int]$OutWidth, [int]$OutHeight, [int]$BandHeight, [int]$StatusPx, [int]$NavPx, [double]$MaxBottomFraction) {
    $scale = [double]$OutWidth / $SrcWidth
    $areaSrcHeight = ($OutHeight - $BandHeight) / $scale
    $crop = $SrcHeight - $areaSrcHeight
    $tolerance = 1.5 / $scale
    if ($crop -lt -$tolerance) { throw "The $SrcWidth x $SrcHeight shot is too short to fill $OutWidth x $($OutHeight - $BandHeight) at its own ratio: it would have to be stretched or letterboxed. Make the shot at a taller size (store-screenshots.ps1 sets it)." }
    if ($crop -lt $StatusPx + $NavPx - $tolerance) { throw "The $SrcWidth x $SrcHeight shot leaves no room to cut the status bar ($StatusPx px) and navigation bar ($NavPx px) off: the status bar would show." }
    $extra = $crop - $StatusPx - $NavPx
    if ($extra -gt $MaxBottomFraction * $SrcHeight + $tolerance) { throw "Filling $OutWidth x $($OutHeight - $BandHeight) from the $SrcWidth x $SrcHeight shot would cut $([int][math]::Round($extra)) px of the app off the bottom (more than $([math]::Round($MaxBottomFraction * 100, 1))% of its height). Not cropped, not stretched, not letterboxed: take the shot at another size." }
    $top = [math]::Min([double]$StatusPx, $crop)
    return @([int][math]::Round($top), [int][math]::Round($areaSrcHeight))
}

# Pure: the caption for a shot key and language from the parsed data file; throws when missing.
function Get-CaptionLine($Captions, [string]$App, [string]$Key, [string]$Lang) {
    $line = $Captions.$App.$Key.$Lang
    if (-not $line) { throw "No $Lang caption for $App $Key in captions.json." }
    $problem = Test-CaptionLine $line
    if ($problem) { throw "Caption for $App $Key ($Lang) rejected: $problem." }
    return [string]$line
}

# Pure: what raw shot files are ordered by (Slice 433 review pass 2, finding 5): the name without .png, so store-dino-phone-4-language (Spanish) comes before store-dino-phone-4-language-ja (a name sort with the extension puts '-' before '.').
function Get-ShotOrderKey([string]$FileName) { return [IO.Path]::GetFileNameWithoutExtension($FileName).ToLowerInvariant() }

# Pure: the shot key (<n>-<screen>) from a raw file name store-<app>-<form>-<n>-<screen>.png.
function Get-ShotKey([string]$FileName, [string]$App, [string]$Form) {
    $prefix = "store-$App-$Form-"
    if (-not $FileName.StartsWith($prefix) -or -not $FileName.EndsWith('.png')) { return $null }
    return $FileName.Substring($prefix.Length, $FileName.Length - $prefix.Length - 4)
}

# Pure: the caption key of a shot key: an extra file of the same screen in another language (4-language-ja) uses the caption of its screen (4-language).
function Get-CaptionKey([string]$ShotKey) { return ($ShotKey -replace '^(\d+-[a-z]+)-[a-z]{2}$', '$1') }

# Pure: the status bar and navigation bar heights of a form's raw shots, from <raw folder>/frame-info.json when it exists, else the form's defaults.
function Get-FrameBars([string]$RawDir, [string]$Form) {
    $spec = Get-FrameSpec $Form
    $bars = @{ StatusPx = $spec.StatusPx; NavPx = $spec.NavPx }
    $file = Join-Path $RawDir 'frame-info.json'
    if (Test-Path $file) {
        $info = Get-Content $file -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($null -ne $info.statusBarPx) { $bars.StatusPx = [int]$info.statusBarPx }
        if ($null -ne $info.navBarPx) { $bars.NavPx = [int]$info.navBarPx }
    }
    return $bars
}

$script:PrivateFonts = $null
$CyrillicLangs = @('ru')
$SystemFonts = @{ 'ja' = 'Yu Gothic UI Semibold'; 'zh' = 'Microsoft YaHei UI' }

# Pure: the app font file (in Resources/Fonts) a language's captions use. Fredoka has no Cyrillic, so Russian uses Andika, the app's reading font.
function Get-CaptionFontFile([string]$Lang) {
    if ($RightToLeftLangs -contains $Lang) { return 'Vazirmatn.ttf' }
    if ($CyrillicLangs -contains $Lang) { return 'Andika-Regular.ttf' }
    return 'Fredoka-Medium.ttf'
}

function Read-FontU16([byte[]]$B, [int]$O) { return ([int]$B[$O] -shl 8) -bor [int]$B[$O + 1] }
function Read-FontU32([byte[]]$B, [int]$O) { return ([long]$B[$O] -shl 24) -bor ([long]$B[$O + 1] -shl 16) -bor ([long]$B[$O + 2] -shl 8) -bor [long]$B[$O + 3] }

# Pure: true when a cmap subtable (format 4 or 12) at $Sub maps the code point to a real glyph.
function Test-CmapSubtable([byte[]]$B, [int]$Sub, [int]$Cp) {
    $format = Read-FontU16 $B $Sub
    if ($format -eq 12) {
        $groups = Read-FontU32 $B ($Sub + 12)
        for ($i = 0; $i -lt $groups; $i++) {
            $g = $Sub + 16 + 12 * $i
            if ($Cp -ge (Read-FontU32 $B $g) -and $Cp -le (Read-FontU32 $B ($g + 4))) { return $true }
        }
        return $false
    }
    if ($format -ne 4 -or $Cp -gt 0xFFFF) { return $false }
    $segX2 = Read-FontU16 $B ($Sub + 6)
    $endOff = $Sub + 14; $startOff = $endOff + $segX2 + 2; $deltaOff = $startOff + $segX2; $rangeOff = $deltaOff + $segX2
    for ($i = 0; $i -lt ($segX2 / 2); $i++) {
        if ($Cp -gt (Read-FontU16 $B ($endOff + 2 * $i))) { continue }
        if ($Cp -lt (Read-FontU16 $B ($startOff + 2 * $i))) { return $false }
        $delta = Read-FontU16 $B ($deltaOff + 2 * $i)
        $ro = Read-FontU16 $B ($rangeOff + 2 * $i)
        if ($ro -eq 0) { return ((($Cp + $delta) -band 0xFFFF) -ne 0) }
        $glyph = Read-FontU16 $B ($rangeOff + 2 * $i + $ro + 2 * ($Cp - (Read-FontU16 $B ($startOff + 2 * $i))))
        return ($glyph -ne 0)
    }
    return $false
}

# Pure: the characters of $Text that a font file (TrueType, read by its cmap table) has no glyph for, as one string ('' = all covered). Spaces and punctuation the caption rules allow are checked like the rest.
function Get-MissingGlyphsInFile([string]$FontPath, [string]$Text) {
    $b = [IO.File]::ReadAllBytes($FontPath)
    $tables = Read-FontU16 $b 4
    $cmap = -1
    for ($i = 0; $i -lt $tables; $i++) { if ([Text.Encoding]::ASCII.GetString($b, 12 + 16 * $i, 4) -eq 'cmap') { $cmap = [int](Read-FontU32 $b (12 + 16 * $i + 8)) } }
    if ($cmap -lt 0) { throw "Font file $FontPath has no cmap table." }
    $subs = @(0..((Read-FontU16 $b ($cmap + 2)) - 1) | ForEach-Object { $cmap + [int](Read-FontU32 $b ($cmap + 4 + 8 * $_ + 4)) })
    $missing = New-Object System.Text.StringBuilder
    foreach ($ch in $Text.ToCharArray()) {
        $found = $false
        foreach ($s in $subs) { if (Test-CmapSubtable $b $s ([int]$ch)) { $found = $true; break } }
        if (-not $found -and $missing.ToString().IndexOf($ch) -lt 0) { $null = $missing.Append($ch) }
    }
    return $missing.ToString()
}

# The characters of $Text an installed Windows font has no glyph for (GDI GetGlyphIndices; Windows would otherwise draw them in a fallback font).
function Get-MissingGlyphsInSystemFont([string]$FamilyName, [string]$Text) {
    if (-not ('CaptionGlyphProbe' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class CaptionGlyphProbe {
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr w);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr w, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr h);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] static extern uint GetGlyphIndicesW(IntPtr dc, string text, int count, ushort[] idx, uint flags);
    public static void Free(IntPtr hfont) { DeleteObject(hfont); }
    public static string Missing(IntPtr hfont, string text) {
        IntPtr dc = GetDC(IntPtr.Zero);
        IntPtr old = SelectObject(dc, hfont);
        try {
            ushort[] idx = new ushort[text.Length];
            if (GetGlyphIndicesW(dc, text, text.Length, idx, 1) == 0xFFFFFFFF) throw new InvalidOperationException("GetGlyphIndicesW failed");
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < idx.Length; i++) if (idx[i] == 0xFFFF && sb.ToString().IndexOf(text[i]) < 0) sb.Append(text[i]);
            return sb.ToString();
        } finally { SelectObject(dc, old); ReleaseDC(IntPtr.Zero, dc); }
    }
}
'@
    }
    $font = New-Object System.Drawing.Font($FamilyName, 20, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $hfont = $font.ToHfont()
    try { return [CaptionGlyphProbe]::Missing($hfont, $Text) }
    finally { [CaptionGlyphProbe]::Free($hfont); $font.Dispose() }
}

# Throws when the font a language's captions use has no glyph for a character of the line (GDI+ would silently draw it in another font).
function Assert-CaptionGlyphs([string]$Lang, [string]$Text) {
    if ($SystemFonts.ContainsKey($Lang)) { $missing = Get-MissingGlyphsInSystemFont $SystemFonts[$Lang] $Text; $fontName = $SystemFonts[$Lang] }
    else { $file = Get-CaptionFontFile $Lang; $missing = Get-MissingGlyphsInFile (Join-Path $FontFolder $file) $Text; $fontName = $file }
    if ($missing) { throw "The caption font for '$Lang' ($fontName) has no glyph for: $missing in '$Text'. Pick a font that covers it; a substitute font is not allowed on a store shot." }
}

# The font family of a language's captions: the app's own font files for Latin, Cyrillic, Farsi and Arabic; the Windows UI font for Japanese and Chinese. Throws when it is not there (Windows would substitute another).
function Get-CaptionFamily([string]$Lang) {
    if ($SystemFonts.ContainsKey($Lang)) {
        $family = New-Object System.Drawing.FontFamily($SystemFonts[$Lang])
        if ($family.Name -ne $SystemFonts[$Lang]) { throw "Font '$($SystemFonts[$Lang])' is not installed (Windows substituted '$($family.Name)'); captions in '$Lang' need it." }
        return $family
    }
    $file = Get-CaptionFontFile $Lang
    $wanted = if ($RightToLeftLangs -contains $Lang) { 'Vazirmatn Medium' } elseif ($CyrillicLangs -contains $Lang) { 'Andika*' } else { 'Fredoka*' }
    if (-not $script:PrivateFonts) { $script:PrivateFonts = @{} }
    if (-not $script:PrivateFonts.ContainsKey($file)) {
        $collection = New-Object System.Drawing.Text.PrivateFontCollection
        $collection.AddFontFile((Join-Path $FontFolder $file))
        $script:PrivateFonts[$file] = $collection
    }
    $match = @($script:PrivateFonts[$file].Families | Where-Object { $_.Name -like $wanted })
    if ($match.Count -eq 0) { throw "Font file $file has no family '$wanted' (it has: $(($script:PrivateFonts[$file].Families | ForEach-Object Name) -join ', '))." }
    return $match[0]
}

# Largest font size (down to 10 px) at which the text fits $MaxWidth in at most two lines of the band.
function Get-FittingFont([System.Drawing.Graphics]$G, [System.Drawing.FontFamily]$Family, [string]$Text, [int]$MaxWidth, [int]$BandHeight, [double]$StartFraction, [System.Drawing.StringFormat]$Format) {
    for ($size = [int]($BandHeight * $StartFraction); $size -ge 10; $size--) {
        $font = New-Object System.Drawing.Font($Family, $size, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
        $measured = $G.MeasureString($Text, $font, $MaxWidth, $Format)
        if ($measured.Height -le ($BandHeight - 24) -and $measured.Height -le ($size * $MaxLineHeights)) { return $font }
        $font.Dispose()
    }
    throw "Caption does not fit the band: $Text"
}

# Draws the part of $Src chosen by Get-ShotCrop into the area under the band, scaled to the output width.
function Add-ScreenArea([System.Drawing.Graphics]$G, [System.Drawing.Image]$Src, [int]$OutWidth, [int]$BandHeight, [int]$OutHeight, [int[]]$Crop) {
    $attributes = New-Object System.Drawing.Imaging.ImageAttributes
    try {
        $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
        $dest = New-Object System.Drawing.Rectangle(0, $BandHeight, $OutWidth, ($OutHeight - $BandHeight))
        $G.DrawImage($Src, $dest, 0, $Crop[0], $Src.Width, $Crop[1], [System.Drawing.GraphicsUnit]::Pixel, $attributes)
    } finally { $attributes.Dispose() }
}

# Writes the framed copy of $RawPath to $OutPath at the form's exact size; $Text empty = no band (the screen fills the whole picture below the status bar). The raw file is only read. 24-bit, so no alpha.
function New-FramedShot([string]$RawPath, [string]$OutPath, [string]$Form, [string]$Text, [string]$Lang, [hashtable]$Bars) {
    $spec = Get-FrameSpec $Form
    $w = [int]$spec.Width; $h = [int]$spec.Height
    $band = if ($Text) { Get-BandHeight $h $spec.Band } else { 0 }
    $src = [System.Drawing.Image]::FromFile($RawPath)
    try {
        $crop = Get-ShotCrop $src.Width $src.Height $w $h $band $Bars.StatusPx $Bars.NavPx $spec.MaxBottom
        $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $g = $null; $fmt = $null; $font = $null; $brush = $null
        try {
            $bmp.SetResolution(72, 72)
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $g.Clear($BandColor)
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
            Add-ScreenArea $g $src $w $band $h $crop
            if ($Text) {
                $fmt = New-Object System.Drawing.StringFormat
                $fmt.Alignment = [System.Drawing.StringAlignment]::Center
                $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
                if ($RightToLeftLangs -contains $Lang) { $fmt.FormatFlags = [System.Drawing.StringFormatFlags]::DirectionRightToLeft }
                Assert-CaptionGlyphs $Lang $Text
                $margin = [int]($w * 0.06)
                $font = Get-FittingFont $g (Get-CaptionFamily $Lang) $Text ($w - 2 * $margin) $band $spec.Start $fmt
                $rect = New-Object System.Drawing.RectangleF($margin, 0, ($w - 2 * $margin), $band)
                $brush = New-Object System.Drawing.SolidBrush($TextColor)
                $g.DrawString($Text, $font, $brush, $rect, $fmt)
            }
            $g.Dispose(); $g = $null
            $null = New-Item -ItemType Directory -Force (Split-Path $OutPath -Parent)
            $bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
        } finally {
            foreach ($o in @($brush, $font, $fmt, $g, $bmp)) { if ($o) { $o.Dispose() } }
        }
    } finally { $src.Dispose() }
}

# A small contact sheet: the framed shots side by side, each scaled to $CellHeight tall.
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
$spec = Get-FrameSpec $Form
$captioned = $spec.Band -gt 0
$rawDir = Join-Path $ShotsRoot "$App\$Form"
$outDir = if ($captioned) { Join-Path $ShotsRoot "$App\$Form-captioned\$Lang" } else { Join-Path $ShotsRoot "$App\$Form-framed" }
$captions = if ($captioned) { Get-Content $CaptionsFile -Raw -Encoding UTF8 | ConvertFrom-Json } else { $null }
$raws = @(Get-ChildItem $rawDir -Filter 'store-*.png' -File | Sort-Object { Get-ShotOrderKey $_.Name })
if ($raws.Count -eq 0) { Write-Host "STORE CAPTIONS: no raw shots in $rawDir."; exit 1 }
$bars = Get-FrameBars $rawDir $Form

# Validate every shot first (a missing or rejected line stops the run before anything is written), then render into a temp folder and swap it in only when all succeeded.
$jobs = @()
foreach ($raw in $raws) {
    $key = Get-ShotKey $raw.Name $App $Form
    if (-not $key) { throw "Raw shot $($raw.Name) does not match store-$App-$Form-<n>-<screen>.png; rename or remove it." }
    $text = if ($captioned) { Get-CaptionLine $captions $App (Get-CaptionKey $key) $Lang } else { '' }
    if ($text) { Assert-CaptionGlyphs $Lang $text }
    $jobs += [pscustomobject]@{ Raw = $raw; Text = $text }
}
$tempDir = Join-Path (Split-Path $outDir -Parent) (".captions-tmp-" + [guid]::NewGuid().ToString('N'))
$backupDir = $null
try {
    foreach ($job in $jobs) { New-FramedShot $job.Raw.FullName (Join-Path $tempDir $job.Raw.Name) $Form $job.Text $Lang $bars }
    # Swap: old set to a backup, new set in, backup removed. A failed move puts the old set back.
    if (Test-Path $outDir) { $backupDir = Join-Path (Split-Path $outDir -Parent) (".captions-old-" + [guid]::NewGuid().ToString('N')); Move-Item $outDir $backupDir }
    try { Move-Item $tempDir $outDir }
    catch { if ($backupDir -and -not (Test-Path $outDir)) { Move-Item $backupDir $outDir; $backupDir = $null }; throw }
    if ($backupDir) { Remove-Item $backupDir -Recurse -Force; $backupDir = $null }
} finally { if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force } }
$written = @($jobs | ForEach-Object { Join-Path $outDir $_.Raw.Name })
foreach ($job in $jobs) { Write-Host "STORE CAPTIONS: $($job.Raw.Name) -> $outDir ($($job.Text))" }
if ($ContactSheet) { New-CaptionSheet $written $ContactSheet; Write-Host "STORE CAPTIONS: contact sheet $ContactSheet" }
# Every file is checked against its store's size before anything uploads it (store-size-check.ps1 is the upload step's first command too).
& (Join-Path $PSScriptRoot 'store-size-check.ps1') -Target $spec.Target -Path $outDir
exit $LASTEXITCODE
