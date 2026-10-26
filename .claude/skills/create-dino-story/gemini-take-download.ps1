# Saves the picture Gemini just downloaded (create-dino-story step 6), with no clipboard, so it also works while the PC is locked:
#   pwsh gemini-take-download.ps1 <file> [-Bytes <n>]    waits for the newest Downloads\Gemini_Generated_Image_* written in the last
#                                            two minutes, moves it out of Downloads and saves it as JPEG quality 95 at <file>
# Moving it means the next call can never pick up the same picture again; if the save fails it is moved back to Downloads under its original name (or the error names where it is).
# Pass -Bytes with the size(s) `__grab` printed ("image/jpeg <n>"; several allowed: -Bytes 123,456): only a download of exactly
# one of those sizes is taken, so another session's Gemini download can't be. A relative <file> is relative to the PowerShell location.
param(
  [Parameter(Mandatory = $true)][string]$Path,
  [int]$WaitSeconds = 30,
  [long[]]$Bytes = @()
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

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
    if ([math]::Abs($picture.Width * 4 - $picture.Height * 3) -gt $picture.Height * 0.03) { Write-Host "WARNING: $file is not 3:4, so it is probably the wrong picture." }
  }
  finally { $picture.Dispose() }
}

function Move-ToFailedFolder([System.IO.FileInfo]$source) {
  New-Item -ItemType Directory -Force -Path $failedFolder | Out-Null
  $target = Join-Path $failedFolder $source.Name
  Move-Item -LiteralPath $source.FullName -Destination $target -Force
  return Get-Item -LiteralPath $target
}

$target = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
$original = Wait-ForDownload
$download = Move-ToFailedFolder $original
try { Save-AsJpeg $download $target }
catch {
  $restored = Join-Path $downloads $original.Name
  try { Move-Item -LiteralPath $download.FullName -Destination $restored -Force -ErrorAction Stop; $where = $restored }
  catch { $where = $download.FullName }
  throw "Saving $target failed ($($_.Exception.Message)); the picture is at $where"
}
Remove-Item -LiteralPath $download.FullName
