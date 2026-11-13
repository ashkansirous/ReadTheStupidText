<#
.SYNOPSIS
  Saves a check copy of a picture with a red line at 70% of its height and the bottom 30% tinted,
  so the full-size check can see whether any part of a character (feet, tail, a held berry) sits
  in the strip the app's words cover (Slice 350, 2026-11-13). It only draws the guide; the checker
  reads the copy and judges where each character ends. Since Slice 375 it draws on the picture's
  small copy (<name>.small.jpg), so the output is 896x1200, not full size.
  Where files go (no git call; the path alone decides):
  - The picture's full path has a \content\dino\ segment (a book folder in any checkout): the small copy and
    the overlay both go to %TEMP%\line70\<first 12 hex of SHA-256 of the full path>\, so two books' 5.jpg never
    share a file and nothing lands in the repo.
  - Any other path (candidates in a scratch folder): the small copy beside it is used and kept (rebuilt only
    when missing or older than the picture), and the overlay is written beside it too
    (<name>.small.jpg.line70.png).
  - -OutDir overrides both: the small copy and the overlay go to that folder.
  The small copy is written to a temp name and renamed into place, so a parallel reader never sees a half file.
  The written overlay path is printed.
.PARAMETER OutDir
  Folder for the small copy and the overlay; overrides both rules above.
.EXAMPLE
  pwsh line70.ps1 cand-5.jpg                       # scratch candidate: writes cand-5.small.jpg.line70.png beside it
  pwsh line70.ps1 content/dino/book/high-res/5.jpg # book folder: small copy and overlay go to %TEMP%\line70\<hash>\
#>
param(
    [Parameter(Mandatory)][string]$Picture,
    [ValidateRange(0.05, 0.95)][double]$Fraction = 0.70,
    [string]$OutDir
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
. (Join-Path $PSScriptRoot '..\..\..\tools\picture-review-copy.ps1')   # Slice 375: the guide is drawn on the 896x1200 small copy, never the full-size original

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

function Test-InBookFolder([string]$picturePath) {
    return $picturePath.Replace('/', '\').IndexOf('\content\dino\', [StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Get-ScratchDir([string]$picturePath) {
    $parent = Join-Path ([System.IO.Path]::GetTempPath()) 'line70'
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $bytes = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($picturePath)) } finally { $sha.Dispose() }
    $key = ([BitConverter]::ToString($bytes) -replace '-', '').Substring(0, 12).ToLowerInvariant()
    $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Join-Path $parent $key))
}

function Get-SmallPicture([string]$picturePath, [string]$copyDir) {
    if ($picturePath -like '*.small.jpg') { return $picturePath }
    $small = Get-SmallCopyPath $picturePath $copyDir
    if ((Test-Path -LiteralPath $small) -and (Get-Item -LiteralPath $small).LastWriteTime -ge (Get-Item -LiteralPath $picturePath).LastWriteTime) { return $small }   # a missing or older copy is rebuilt, so the guide never sits on a stale picture
    Write-SmallCopy $picturePath $small
    $small
}

function Write-SmallCopy([string]$picturePath, [string]$small) {
    $folder = Split-Path -Parent $small
    if (-not (Test-Path -LiteralPath $folder)) { New-Item -ItemType Directory -Force -Path $folder | Out-Null }
    $temp = "$small.$([Guid]::NewGuid().ToString('N')).tmp"
    try {
        Save-Prepared $picturePath $temp
        Move-Item -LiteralPath $temp -Destination $small -Force   # atomic rename: a parallel reader never opens a half-written JPEG
    } finally {
        if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force }
    }
}

function Get-CopyDir([string]$originalPath, [string]$outDir) {
    if ($outDir) { return $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($outDir) }
    if (Test-InBookFolder $originalPath) { return Get-ScratchDir $originalPath }   # a book folder: keyed by the full path, nothing in the repo
    return ''                                                                      # scratch candidate: beside the picture
}

$original = (Resolve-Path -LiteralPath $Picture).ProviderPath
$copyDir = Get-CopyDir $original $OutDir
$small = Get-SmallPicture $original $copyDir
$out = Get-OutPath $small $copyDir
Save-WithLine $small $Fraction $out
Write-Output $out
