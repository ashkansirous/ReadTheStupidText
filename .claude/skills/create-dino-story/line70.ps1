<#
.SYNOPSIS
  Saves a check copy of a picture with a red line at 70% of its height and the bottom 30% tinted,
  so the full-size check can see whether any part of a character (feet, tail, a held berry) sits
  in the strip the app's words cover (Slice 350, 2026-11-13). It only draws the guide; the checker
  reads the copy and judges where each character ends.
.PARAMETER OutDir
  Folder for the copy. Default: next to the picture. Pass a scratch folder when the picture sits in
  the repo (the reviewer changes nothing in the repo).
.EXAMPLE
  pwsh line70.ps1 cand-5.jpg                       # writes cand-5.jpg.line70.png next to it
  pwsh line70.ps1 high-res/pictures/5.png -OutDir $env:TEMP\line70
#>
param(
    [Parameter(Mandatory)][string]$Picture,
    [ValidateRange(0.05, 0.95)][double]$Fraction = 0.70,
    [string]$OutDir
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Get-OutPath([string]$picturePath, [string]$outDir) {
    $dir = if ($outDir) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($outDir) } else { Split-Path -Parent $picturePath }
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    Join-Path $dir ((Split-Path -Leaf $picturePath) + '.line70.png')
}

function Save-WithLine([string]$picturePath, [double]$fraction, [string]$out) {
    $src = $bmp = $g = $tint = $pen = $null
    try {
        $src = [Drawing.Image]::FromFile($picturePath)
        $bmp = New-Object Drawing.Bitmap $src.Width, $src.Height
        $g = [Drawing.Graphics]::FromImage($bmp)
        $g.DrawImage($src, 0, 0, $src.Width, $src.Height)
        $y = [int]($src.Height * $fraction)
        $tint = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(70, 0, 90, 255))
        $g.FillRectangle($tint, 0, $y, $src.Width, $src.Height - $y)
        $pen = New-Object Drawing.Pen ([Drawing.Color]::Red), ([Math]::Max(4, [int]($src.Height / 300)))
        $g.DrawLine($pen, 0, $y, $src.Width, $y)
        $g.Dispose(); $g = $null
        $bmp.Save($out, [Drawing.Imaging.ImageFormat]::Png)
    } catch {
        throw "line70: could not write '$out' from '$picturePath': $($_.Exception.Message)"
    } finally {
        foreach ($d in @($pen, $tint, $g, $bmp, $src)) { if ($d) { $d.Dispose() } }
    }
}

$full = (Resolve-Path -LiteralPath $Picture).ProviderPath
$out = Get-OutPath $full $OutDir
Save-WithLine $full $Fraction $out
Write-Output $out
