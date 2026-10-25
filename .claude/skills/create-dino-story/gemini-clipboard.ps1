# Moves a picture between a file and the Windows clipboard for the Gemini loop (create-dino-story steps 3 and 6). Run with -STA:
#   pwsh -STA gemini-clipboard.ps1 Put <file>    puts the picture (e.g. the line-up) on the clipboard, to paste into Gemini with ctrl+v
#   pwsh -STA gemini-clipboard.ps1 Save <file>   saves the clipboard picture (from gemini-grab.js) as JPEG quality 95, then clears it
param(
  [Parameter(Mandatory = $true)][ValidateSet('Put', 'Save')][string]$Action,
  [Parameter(Mandatory = $true)][string]$Path
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing

$saveQuality = 95L

function Set-ClipboardPicture([string]$file) {
  if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Picture not found: $file" }
  $picture = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $file).Path)
  try { [System.Windows.Forms.Clipboard]::SetImage($picture) }
  finally { $picture.Dispose() }
  Write-Host "On the clipboard: $file"
}

function Save-ClipboardPicture([string]$file) {
  $picture = [System.Windows.Forms.Clipboard]::GetImage()
  if (-not $picture) { throw 'No picture on the clipboard: check window.__clipResult after the real click.' }
  try {
    $encoder = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
    $parameters = [System.Drawing.Imaging.EncoderParameters]::new(1)
    $parameters.Param[0] = [System.Drawing.Imaging.EncoderParameter]::new([System.Drawing.Imaging.Encoder]::Quality, $saveQuality)
    $picture.Save([IO.Path]::GetFullPath($file), $encoder, $parameters)
    Write-Host "Saved $file ($($picture.Width)x$($picture.Height))"
    if ([math]::Abs($picture.Width * 4 - $picture.Height * 3) -gt $picture.Height * 0.03) { Write-Host "WARNING: $file is not 3:4, so it is probably the wrong picture." }
  }
  finally { $picture.Dispose() }
  [System.Windows.Forms.Clipboard]::Clear()
}

if ($Action -eq 'Put') { Set-ClipboardPicture $Path } else { Save-ClipboardPicture $Path }
