<#
.SYNOPSIS
  Slice 433 item 7: turns the first PNG frame of the feature graphic render (RGBA, from `npm run render:feature`) into the 24-bit, no-alpha 1024 x 500 PNG Google Play takes, and checks it with tools/emulator/store-size-check.ps1 -Target play-feature.
.EXAMPLE
  pwsh -NoProfile -File tools/dino/promo/store/flatten-still.ps1 -Frames tools/dino/promo/store/renders/feature-frames -Out docs/store-listings/screenshots/dino/feature-graphic/feature-graphic.png
#>
param(
    [Parameter(Mandatory)][string]$Frames,   # the png-sequence folder
    [Parameter(Mandatory)][string]$Out
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$frame = Get-ChildItem $Frames -Filter 'frame_*.png' -File | Sort-Object Name | Select-Object -First 1
if (-not $frame) { Write-Host "FLATTEN STILL: no frame_*.png in $Frames."; exit 1 }
$src = [System.Drawing.Image]::FromFile($frame.FullName)
try {
    $bmp = New-Object System.Drawing.Bitmap($src.Width, $src.Height, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#F3EFE6'))   # the app's Sand: the composition is opaque, so nothing shows through
        $g.DrawImage($src, 0, 0, $src.Width, $src.Height)
    } finally { $g.Dispose() }
    $null = New-Item -ItemType Directory -Force (Split-Path $Out -Parent)
    $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
} finally { $src.Dispose() }
Write-Host "FLATTEN STILL: $($frame.Name) -> $Out"
& (Join-Path $PSScriptRoot '..\..\..\emulator\store-size-check.ps1') -Target 'play-feature' -Path $Out
exit $LASTEXITCODE
