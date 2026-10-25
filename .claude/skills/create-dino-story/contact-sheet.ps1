# Draws a whole book's pictures onto one sheet, so the caller can look over every page in a single picture read (create-dino-story
# step 6, made at its end and read by the coordinator per "Who does what" item 4): `pwsh contact-sheet.ps1 <folder> <pageCount> <outFile>`. The folder holds cover.jpg and 1.jpg..N.jpg. Tiles are 224x300
# in rows of four, each with its label (cover, 1, 2...) above it. Fails, writing nothing, when a picture is missing.
param(
  [Parameter(Mandatory = $true)][string]$Folder,
  [Parameter(Mandatory = $true)][int]$PageCount,
  [Parameter(Mandatory = $true)][string]$OutFile
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$tileWidth = 224
$tileHeight = 300
$labelHeight = 24
$columns = 4
$sheetQuality = 85L

function Get-Tiles {
  $tiles = @([pscustomobject]@{ Label = 'cover'; File = Join-Path $Folder 'cover.jpg' })
  $tiles += 1..$PageCount | ForEach-Object { [pscustomobject]@{ Label = "$_"; File = Join-Path $Folder "$_.jpg" } }
  $missing = $tiles | Where-Object { -not (Test-Path -LiteralPath $_.File -PathType Leaf) }
  if ($missing) { throw "Missing in ${Folder}: $(($missing | ForEach-Object { Split-Path $_.File -Leaf }) -join ', ')" }
  $tiles
}

function Add-Tile([System.Drawing.Graphics]$graphics, $tile, [int]$index, [System.Drawing.Font]$font) {
  $x = ($index % $columns) * $tileWidth
  $y = [math]::Floor($index / $columns) * ($tileHeight + $labelHeight)
  $graphics.DrawString($tile.Label, $font, [System.Drawing.Brushes]::Black, $x + 4, $y + 3)
  $picture = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $tile.File).Path)
  try { $graphics.DrawImage($picture, [System.Drawing.Rectangle]::new($x, $y + $labelHeight, $tileWidth, $tileHeight)) }
  finally { $picture.Dispose() }
}

function Save-Sheet([System.Drawing.Bitmap]$sheet) {
  $encoder = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
  $parameters = [System.Drawing.Imaging.EncoderParameters]::new(1)
  $parameters.Param[0] = [System.Drawing.Imaging.EncoderParameter]::new([System.Drawing.Imaging.Encoder]::Quality, $sheetQuality)
  $sheet.Save([IO.Path]::GetFullPath($OutFile), $encoder, $parameters)
}

if ($PageCount -lt 1) { throw "Page count must be at least 1, got $PageCount." }
$tiles = @(Get-Tiles)
$rows = [math]::Ceiling($tiles.Count / $columns)
$sheet = [System.Drawing.Bitmap]::new($columns * $tileWidth, $rows * ($tileHeight + $labelHeight))
try {
  $graphics = [System.Drawing.Graphics]::FromImage($sheet)
  $font = [System.Drawing.Font]::new('Segoe UI', 11)
  try {
    $graphics.Clear([System.Drawing.Color]::White)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    for ($i = 0; $i -lt $tiles.Count; $i++) { Add-Tile $graphics $tiles[$i] $i $font }
  }
  finally { $font.Dispose(); $graphics.Dispose() }
  Save-Sheet $sheet
}
finally { $sheet.Dispose() }
Write-Host "Contact sheet with $($tiles.Count) pictures: $OutFile"
