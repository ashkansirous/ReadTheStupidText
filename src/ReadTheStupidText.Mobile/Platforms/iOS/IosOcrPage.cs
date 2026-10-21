using CoreGraphics;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// One scanned page for the OCR engine on iOS (Slice 55): the downscaled detection image, decoded
/// at the detector's size only, plus the path of the file it came from. A detected box is in the
/// detection image's coordinates; it is scaled up to the original, grown by a small margin,
/// cropped, and drawn at a size that keeps the line about twice the recognizer height. The sharper
/// upright picture (at most <see cref="IosPixelImageLoader.MaxRecognitionSide"/> pixels on its long
/// side) is decoded on the first crop, not up front, so a page where nothing is detected never
/// pays for it, and it is released with the page.
/// </summary>
internal sealed class IosOcrPage : IOcrPage
{
    private const float MarginInLineHeights = 0.1f;

    private readonly string _path;
    private CGImage? _original;

    public IosOcrPage(PixelImage detection, string path)
    {
        Detection = detection;
        _path = path;
    }

    public PixelImage Detection { get; }

    public PixelImage? CropLine(OcrBox box, int recognitionHeight)
    {
        try
        {
            return CropFromOriginal(box, recognitionHeight);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Original crop failed, cropping from the detection image: {ex.Message}");
            return ImageResizer.Crop(Detection, box);
        }
    }

    public void Dispose() => _original?.Dispose();

    private PixelImage? CropFromOriginal(OcrBox box, int recognitionHeight)
    {
        CGImage original = Original();
        float scaleX = (float)original.Width / Detection.Width;
        float scaleY = (float)original.Height / Detection.Height;
        var scaled = new OcrBox(box.Left * scaleX, box.Top * scaleY, box.Right * scaleX, box.Bottom * scaleY);
        float lineHeight = Math.Min(scaled.Width, scaled.Height);
        if (SourceRegion(original, scaled, lineHeight * MarginInLineHeights) is not { } region)
        {
            return null;
        }

        using CGImage? cropped = original.WithImageInRect(region);
        if (cropped is null)
        {
            return ImageResizer.Crop(Detection, box);
        }

        int sample = CropSampling.SampleSize(lineHeight, recognitionHeight);
        int width = Math.Max(1, (int)MathF.Ceiling((float)region.Width / sample));
        int height = Math.Max(1, (int)MathF.Ceiling((float)region.Height / sample));
        return CgImagePixels.ToPixelImage(cropped, width, height);
    }

    // The margin-grown box clamped to the original; null when nothing is left of it.
    private CGImage Original() => _original ??= IosPixelImageLoader.DecodeUpright(_path, IosPixelImageLoader.MaxRecognitionSide);

    private static CGRect? SourceRegion(CGImage original, OcrBox box, float margin)
    {
        int left = Math.Clamp((int)MathF.Floor(box.Left - margin), 0, (int)original.Width);
        int top = Math.Clamp((int)MathF.Floor(box.Top - margin), 0, (int)original.Height);
        int right = Math.Clamp((int)MathF.Ceiling(box.Right + margin), 0, (int)original.Width);
        int bottom = Math.Clamp((int)MathF.Ceiling(box.Bottom + margin), 0, (int)original.Height);
        return right > left && bottom > top ? new CGRect(left, top, right - left, bottom - top) : null;
    }
}
