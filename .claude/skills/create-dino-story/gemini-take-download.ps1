# Saves the picture Gemini just downloaded (create-dino-story step 6), with no clipboard, so it also works while the PC is locked:
#   pwsh gemini-take-download.ps1 <file> [-Bytes <n>]    waits for the newest Downloads\Gemini_Generated_Image_* written in the last
#                                            two minutes, moves it out of Downloads and saves it as JPEG quality 95 at <file>
# Moving it means the next call can never pick up the same picture again. Pass -Bytes with the size `__grab` printed
# ("image/jpeg <n>"): only a download of exactly that size is taken, so another session's Gemini download can't be.
param(
  [Parameter(Mandatory = $true)][string]$Path,
  [int]$WaitSeconds = 30,
  [long]$Bytes = 0
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$saveQuality = 95L
$freshSeconds = 120
$downloads = Join-Path $env:USERPROFILE 'Downloads'
$namePattern = 'Gemini_Generated_Image_*'

function Find-FreshDownload {
  $cutoff = (Get-Date).AddSeconds(-$freshSeconds)
  Get-ChildItem -LiteralPath $downloads -Filter $namePattern -File |
    Where-Object { $_.Extension -in '.jpg', '.jpeg', '.png' -and $_.LastWriteTime -gt $cutoff -and ($Bytes -eq 0 -or $_.Length -eq $Bytes) } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

function Wait-ForDownload {
  for ($i = 0; $i -lt $WaitSeconds; $i++) {
    $file = Find-FreshDownload
    if ($file) {
      Start-Sleep -Milliseconds 700
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
    $picture.Save([IO.Path]::GetFullPath($file), $encoder, $parameters)
    Write-Host "Saved $file ($($picture.Width)x$($picture.Height)) from $($source.Name)"
    if ([math]::Abs($picture.Width * 4 - $picture.Height * 3) -gt $picture.Height * 0.03) { Write-Host "WARNING: $file is not 3:4, so it is probably the wrong picture." }
  }
  finally { $picture.Dispose() }
}

$download = Wait-ForDownload
Save-AsJpeg $download $Path
Remove-Item -LiteralPath $download.FullName
