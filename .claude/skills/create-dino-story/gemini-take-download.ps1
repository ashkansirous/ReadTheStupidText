# Saves the picture Gemini just downloaded (create-dino-story step 6), with no clipboard, so it also works while the PC is locked:
#   pwsh gemini-take-download.ps1 <file> [-Bytes <n>] [-Untouched]    waits for the newest Downloads\Gemini_Generated_Image_*
#                                            written in the last two minutes, moves it out of Downloads and saves it as JPEG
#                                            quality 95 at <file> (with -Untouched: moved byte for byte, see below)
# Moving it means the next call can never pick up the same picture again; if the save fails it is moved back to Downloads under its original name (or the error names where it is).
# Pass -Bytes with the size(s) `__grab` printed ("image/jpeg <n>"; several allowed: -Bytes 123,456): only a download of exactly
# one of those sizes is taken, so another session's Gemini download can't be. A relative <file> is relative to the PowerShell location.
# -Untouched (Slice 350 high-res pictures): move the download byte for byte instead of re-saving it; <file>'s extension is
# replaced by the download's own, and the path actually written is printed ("Saved <path> ...").
# Both modes then write <name>.small.jpg beside the picture (896x1200, JPEG 85; Slice 375): the copy agents look at.
# With -Untouched the original stays byte for byte. A move or rename of the picture moves <name>.small.jpg with it.
# Exit codes: 0 saved with its small copy; 1 nothing saved (no download, save failed; the error says where the file is);
# 2 the picture IS saved but the small copy failed ("SAVED <path>; small copy failed: <reason>"): never retry the download, run pwsh tools/picture-review-copy.ps1 <path>.
param(
  [Parameter(Mandatory = $true)][string]$Path,
  [int]$WaitSeconds = 30,
  [long[]]$Bytes = @(),
  [switch]$Untouched
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

. (Join-Path $PSScriptRoot '..\..\..\tools\picture-review-copy.ps1')   # New-ReviewCopy, the one resize function (Slice 375)

$saveQuality = 95L
$freshSeconds = 120
$downloads = Join-Path $env:USERPROFILE 'Downloads'
$namePattern = 'Gemini_Generated_Image_*'
$failedFolder = Join-Path $downloads '.gemini-failed'

function Find-FreshDownload {
  $cutoff = (Get-Date).AddSeconds(-$freshSeconds)
  Get-ChildItem -LiteralPath $downloads -Filter $namePattern -File |
    Where-Object { $_.Extension -in '.jpg', '.jpeg', '.png' -and $_.LastWriteTime -gt $cutoff -and ($Bytes.Count -eq 0 -or $Bytes -contains $_.Length) } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

function Wait-ForDownload {
  for ($i = 0; $i -lt $WaitSeconds; $i++) {
    $file = Find-FreshDownload
    if ($file) {
      Start-Sleep -Seconds 1
      $again = Get-Item -LiteralPath $file.FullName
      if ($again.Length -eq $file.Length -and $again.Length -gt 0) { return $again }
    }
    Start-Sleep -Seconds 1
  }
  throw "No Gemini download in $downloads within $WaitSeconds s: click the picture's 'Download full size image' button (window.__grab) first."
}

function Save-AsJpeg([System.IO.FileInfo]$source, [string]$file) {
  $picture = [System.Drawing.Image]::FromFile($source.FullName)
  try {
    $encoder = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
    $parameters = [System.Drawing.Imaging.EncoderParameters]::new(1)
    $parameters.Param[0] = [System.Drawing.Imaging.EncoderParameter]::new([System.Drawing.Imaging.Encoder]::Quality, $saveQuality)
    $picture.Save($file, $encoder, $parameters)
    Write-Host "Saved $file ($($picture.Width)x$($picture.Height)) from $($source.Name)"
    Write-SizeWarning $file $picture.Width $picture.Height
  }
  finally { $picture.Dispose() }
  Write-SmallCopy $file
}

# Slice 375: every saved picture gets its 896x1200 review copy beside it (<name>.small.jpg); agents look only at that copy.
# The original is never changed. A failure here must not undo the saved picture: it stays, and the script prints
# "SAVED <path>; small copy failed: <reason>" and exits 2, so the caller never retries the download and never reads a missing small copy.
function Write-SmallCopy([string]$file) {
  try { Write-Host "Small copy $(New-ReviewCopy $file) (look at this, never at the full-size original)" }
  catch {
    [Console]::Error.WriteLine("ERROR: saved $file but no small copy: $($_.Exception.Message). Run: pwsh tools/picture-review-copy.ps1 $file")
    $script:smallCopyFailed = $true
    $script:savedPath = $file
    $script:smallCopyReason = $_.Exception.Message
  }
}

function Write-SizeWarning([string]$file, [int]$width, [int]$height) {
  if ([math]::Abs($width * 4 - $height * 3) -gt $height * 0.03) { Write-Host "WARNING: $file is not 3:4, so it is probably the wrong picture." }
}

function Assert-NameFree([string]$file) {
  $stem = Join-Path ([System.IO.Path]::GetDirectoryName($file)) ([System.IO.Path]::GetFileNameWithoutExtension($file))
  foreach ($ext in '.jpg', '.jpeg', '.png') {
    if (Test-Path -LiteralPath "$stem$ext") { throw "$stem$ext already exists; pick a new name so no picture is overwritten or mixed up" }
  }
}

function Save-Untouched([System.IO.FileInfo]$source, [string]$file) {
  $final = [System.IO.Path]::ChangeExtension($file, $source.Extension.ToLowerInvariant())
  Assert-NameFree $file
  $picture = [System.Drawing.Image]::FromFile($source.FullName)
  try { $width = $picture.Width; $height = $picture.Height }
  finally { $picture.Dispose() }
  Move-Item -LiteralPath $source.FullName -Destination $final
  Write-Host "Saved $final (${width}x${height}, $($source.Length) bytes, untouched) from $($source.Name)"
  Write-SizeWarning $final $width $height
  Write-SmallCopy $final
}

function Move-ToFailedFolder([System.IO.FileInfo]$source) {
  New-Item -ItemType Directory -Force -Path $failedFolder | Out-Null
  $target = Join-Path $failedFolder $source.Name
  Move-Item -LiteralPath $source.FullName -Destination $target -Force
  return Get-Item -LiteralPath $target
}

$target = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
if ($Untouched) { Assert-NameFree $target }
$original = Wait-ForDownload
$download = Move-ToFailedFolder $original
$script:smallCopyFailed = $false
try { if ($Untouched) { Save-Untouched $download $target } else { Save-AsJpeg $download $target } }
catch {
  $restored = Join-Path $downloads $original.Name
  try { Move-Item -LiteralPath $download.FullName -Destination $restored -Force -ErrorAction Stop; $where = $restored }
  catch { $where = $download.FullName }
  throw "Saving $target failed ($($_.Exception.Message)); the picture is at $where"
}
if (-not $Untouched) { Remove-Item -LiteralPath $download.FullName }
if ($script:smallCopyFailed) { Write-Host "SAVED $script:savedPath; small copy failed: $script:smallCopyReason"; exit 2 }
