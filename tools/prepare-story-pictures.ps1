# Prepares a story's generated pictures for the repo (Slice 119): `pwsh tools/prepare-story-pictures.ps1 <sourceFolder> <targetFolder> <pageCount>`.
# The source holds cover.jpg and 1.jpg..N.jpg (the originals, kept outside the repo). Each is centre-cropped to 3:4, shrunk to 896x1200
# (never enlarged: a smaller source keeps its cropped size) and saved as JPEG quality 85. Writes <targetFolder>/cover.jpg and
# <targetFolder>/pictures/1.jpg..N.jpg, e.g. tests/DinoReader.Tests/Starters/three-little-pigs or tests/DinoReader.Tests/Store/<stem>.
# Also writes <targetFolder>/cover-thumb.jpg (288x384, quality 80): the small cover the store list fetches (the full cover stays in the package).
# Fails, writing nothing, when a picture is missing or the folder holds more pages than <pageCount>.
param(
  [Parameter(Mandatory = $true)][string]$SourceFolder,
  [Parameter(Mandatory = $true)][string]$TargetFolder,
  [Parameter(Mandatory = $true)][int]$PageCount
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$targetWidth = 896
$targetHeight = 1200
$jpegQuality = 85L
$thumbWidth = 288
$thumbHeight = 384
$thumbQuality = 80L

if (-not (Test-Path -LiteralPath $SourceFolder -PathType Container)) { throw "Source folder not found: $SourceFolder" }
if ($PageCount -lt 1) { throw "Page count must be at least 1, got $PageCount." }

function Get-SourceFiles {
  $expected = @('cover.jpg') + (1..$PageCount | ForEach-Object { "$_.jpg" })
  $missing = $expected | Where-Object { -not (Test-Path -LiteralPath (Join-Path $SourceFolder $_)) }
  if ($missing) { throw "Missing in ${SourceFolder}: $($missing -join ', ')" }
  $pagesFound = Get-ChildItem -LiteralPath $SourceFolder -Filter '*.jpg' | Where-Object { $_.BaseName -match '^\d+$' }
  if ($pagesFound.Count -ne $PageCount) { throw "Expected $PageCount page pictures in $SourceFolder but found $($pagesFound.Count)." }
  $expected
}

function Get-CropRectangle([int]$width, [int]$height) {
  if ($width -eq $targetWidth -and $height -eq $targetHeight) { return [System.Drawing.Rectangle]::new(0, 0, $width, $height) }
  $cropWidth = [math]::Min($width, [int][math]::Floor($height * 3 / 4))
  $cropHeight = [math]::Min($height, [int][math]::Floor($width * 4 / 3))
  [System.Drawing.Rectangle]::new([int][math]::Floor(($width - $cropWidth) / 2), [int][math]::Floor(($height - $cropHeight) / 2), $cropWidth, $cropHeight)
}

function Save-Prepared([string]$sourcePath, [string]$targetPath, [int]$maxWidth = $targetWidth, [int]$maxHeight = $targetHeight, [long]$quality = $jpegQuality) {
  $source = [System.Drawing.Image]::FromFile($sourcePath)
  try {
    $crop = Get-CropRectangle $source.Width $source.Height
    $shrinks = $crop.Width -gt $maxWidth -or $crop.Height -gt $maxHeight
    $outWidth = if ($shrinks) { $maxWidth } else { $crop.Width }
    $outHeight = if ($shrinks) { $maxHeight } else { $crop.Height }
    $bitmap = [System.Drawing.Bitmap]::new($outWidth, $outHeight)
    try {
      $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
      try {
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, $outWidth, $outHeight), $crop, [System.Drawing.GraphicsUnit]::Pixel)
      }
      finally { $graphics.Dispose() }
      $encoder = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
      $parameters = [System.Drawing.Imaging.EncoderParameters]::new(1)
      $parameters.Param[0] = [System.Drawing.Imaging.EncoderParameter]::new([System.Drawing.Imaging.Encoder]::Quality, $quality)
      $bitmap.Save($targetPath, $encoder, $parameters)
    }
    finally { $bitmap.Dispose() }
  }
  finally { $source.Dispose() }
}

$files = Get-SourceFiles
$picturesFolder = Join-Path $TargetFolder 'pictures'
New-Item -ItemType Directory -Force -Path $picturesFolder | Out-Null
foreach ($name in $files) {
  $target = if ($name -eq 'cover.jpg') { Join-Path $TargetFolder $name } else { Join-Path $picturesFolder $name }
  Save-Prepared (Resolve-Path -LiteralPath (Join-Path $SourceFolder $name)).Path ([IO.Path]::GetFullPath($target))
}
$coverSource = (Resolve-Path -LiteralPath (Join-Path $SourceFolder 'cover.jpg')).Path
Save-Prepared $coverSource ([IO.Path]::GetFullPath((Join-Path $TargetFolder 'cover-thumb.jpg'))) $thumbWidth $thumbHeight $thumbQuality
Write-Host "Prepared cover, cover thumbnail and $PageCount pictures in $TargetFolder"
