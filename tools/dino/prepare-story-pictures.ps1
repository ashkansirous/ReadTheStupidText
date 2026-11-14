# Prepares a story's generated pictures for the repo (Slice 119): `pwsh tools/dino/prepare-story-pictures.ps1 <sourceFolder> <targetFolder> <pageCount>`.
# The source holds cover.jpg and 1.jpg..N.jpg (the originals, kept outside the repo). Each is centre-cropped to 3:4, shrunk to 896x1200
# (never enlarged: a smaller source keeps its cropped size) and saved as JPEG quality 85. Writes <targetFolder>/cover.jpg and
# <targetFolder>/pictures/1.jpg..N.jpg, e.g. content/dino/<stem> (starters and store books alike, Slice 157).
# Also writes <targetFolder>/cover-thumb.jpg (288x384, quality 80): the small cover the store list fetches (the full cover stays in the package).
# -HighRes (Slice 375 B) writes the high-res pack's pictures instead: <targetFolder>/high-res/cover.jpg, 1.jpg..N.jpg and back-cover.jpg (when the source has it), each
# centre-cropped to 3:4, shrunk to 1792x2400 (never enlarged) and saved as JPEG quality 85. A source that already is <targetFolder>/high-res is re-saved in place. No thumbnail.
# Fails, writing nothing, when a picture is missing or the folder holds more pages than <pageCount>.
param(
  [Parameter(Mandatory = $true)][string]$SourceFolder,
  [Parameter(Mandatory = $true)][string]$TargetFolder,
  [Parameter(Mandatory = $true)][int]$PageCount,
  [switch]$HighRes
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

. (Join-Path $PSScriptRoot 'picture-review-copy.ps1')   # Save-Prepared and the 896x1200 / JPEG 85 constants (Slice 375: one copy of the resize code)
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

function Save-HighRes {
  $folder = [IO.Path]::GetFullPath((Join-Path $TargetFolder 'high-res'))
  New-Item -ItemType Directory -Force -Path $folder | Out-Null
  $names = @($files)
  if (Test-Path -LiteralPath (Join-Path $SourceFolder 'back-cover.jpg')) { $names += 'back-cover.jpg' }
  foreach ($name in $names) {
    $source = (Resolve-Path -LiteralPath (Join-Path $SourceFolder $name)).Path
    $temp = Join-Path $folder "$name.tmp"
    Save-Prepared $source $temp $highResWidth $highResHeight   # a temp file first: an in-place re-save cannot write over the picture it is still reading
    Move-Item -LiteralPath $temp -Destination (Join-Path $folder $name) -Force
  }
  Write-Host "Prepared $($names.Count) high-res pictures in $folder"
}

$files = Get-SourceFiles
if ($HighRes) { Save-HighRes; return }
$picturesFolder = Join-Path $TargetFolder 'pictures'
New-Item -ItemType Directory -Force -Path $picturesFolder | Out-Null
foreach ($name in $files) {
  $target = if ($name -eq 'cover.jpg') { Join-Path $TargetFolder $name } else { Join-Path $picturesFolder $name }
  Save-Prepared (Resolve-Path -LiteralPath (Join-Path $SourceFolder $name)).Path ([IO.Path]::GetFullPath($target))
}
$coverSource = (Resolve-Path -LiteralPath (Join-Path $SourceFolder 'cover.jpg')).Path
Save-Prepared $coverSource ([IO.Path]::GetFullPath((Join-Path $TargetFolder 'cover-thumb.jpg'))) $thumbWidth $thumbHeight $thumbQuality
Write-Host "Prepared cover, cover thumbnail and $PageCount pictures in $TargetFolder"
