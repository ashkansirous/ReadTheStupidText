# Makes the small review copy of a Dino picture (Slice 375): `pwsh tools/dino/picture-review-copy.ps1 <original>` writes
# <name>.small.jpg beside the original: centre-cropped to 3:4, shrunk to 896x1200 (never enlarged), JPEG quality 85.
# Agents and Claude look only at this copy, never at the full-size original. Prints the path written.
# The resize code lives here, in Save-Prepared; prepare-story-pictures.ps1 dot-sources this file for it (one copy of the code).
# -CopyDir <folder> writes the copy there instead (a reviewer changes nothing in the repo: it passes a scratch folder).
# The parameter names are unique on purpose: dot-sourcing binds them in the caller's scope, so a shared name (OutDir) would be wiped.
param([string]$Original, [string]$CopyDir)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$targetWidth = 896
$targetHeight = 1200
$jpegQuality = 85L

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

function Get-SmallCopyPath([string]$originalPath, [string]$outDir = '') {
  $folder = if ($outDir) { $outDir } else { [IO.Path]::GetDirectoryName($originalPath) }
  Join-Path $folder ([IO.Path]::GetFileNameWithoutExtension($originalPath) + '.small.jpg')
}

function New-ReviewCopy([string]$originalPath, [string]$outDir = '') {
  $full = (Resolve-Path -LiteralPath $originalPath).Path
  if ($full -like '*.small.jpg') { return $full }   # already a small copy: re-use it, never write .small.small.jpg
  if ($outDir) { $outDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($outDir); New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
  $small = Get-SmallCopyPath $full $outDir
  Save-Prepared $full $small
  $small
}

if ($Original -and $MyInvocation.InvocationName -ne '.') { New-ReviewCopy $Original $CopyDir }
