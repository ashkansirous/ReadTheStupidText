# Run once, by hand, in Windows PowerShell 5.1 (Windows.Media.Ocr is not reachable from pwsh 7): `powershell -File tools/ocr-starter-pages.ps1 <folder>`.
# It OCRs pages 1.jpg-10.jpg of a picture starter and prints lines with 0..1 boxes. The output is proofread by hand into
# content/dino/good-enough/lines.json (Slice 94; the pictures are in its pictures/ folder); the build never runs OCR.
param([string]$Folder)
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Foundation, ContentType = WindowsRuntime]
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($op, [Type]$t) { $task = $asTask.MakeGenericMethod($t).Invoke($null, @($op)); $task.Wait() | Out-Null; $task.Result }
$null = [Windows.Globalization.Language, Windows.Globalization, ContentType = WindowsRuntime]
$lang = [Windows.Globalization.Language]::new('en-US')
$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($lang)
$out = @()
foreach ($i in 1..10) {
  $path = Join-Path $Folder "$i.jpg"
  $file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync($path)) ([Windows.Storage.StorageFile])
  $stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
  $dec = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
  $bmp = Await ($dec.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
  $res = Await ($engine.RecognizeAsync($bmp)) ([Windows.Media.Ocr.OcrResult])
  $w = $bmp.PixelWidth; $h = $bmp.PixelHeight
  $lines = foreach ($l in $res.Lines) {
    $x1 = ($l.Words | ForEach-Object { $_.BoundingRect.X } | Measure-Object -Minimum).Minimum
    $y1 = ($l.Words | ForEach-Object { $_.BoundingRect.Y } | Measure-Object -Minimum).Minimum
    $x2 = ($l.Words | ForEach-Object { $_.BoundingRect.X + $_.BoundingRect.Width } | Measure-Object -Maximum).Maximum
    $y2 = ($l.Words | ForEach-Object { $_.BoundingRect.Y + $_.BoundingRect.Height } | Measure-Object -Maximum).Maximum
    [pscustomobject]@{ text = $l.Text; x = [math]::Round($x1/$w,4); y = [math]::Round($y1/$h,4); w = [math]::Round(($x2-$x1)/$w,4); h = [math]::Round(($y2-$y1)/$h,4) }
  }
  $out += [pscustomobject]@{ page = $i; width = $w; height = $h; lines = @($lines) }
}
$out | ConvertTo-Json -Depth 5
