using CoreGraphics;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// One scanned page for the OCR engine on iOS (Slice 55): the downscaled detection image plus the
/// upright full-resolution <see cref="CGImage"/> it was made from. A detected box is in the
/// detection image's coordinates; it is scaled up to the original, grown by a small margin,
/// cropped, and drawn at a size that keeps the line about twice the recognizer height. ImageIO
/// decodes the original lazily, so a crop only touches the pixels it needs. The image lives for
/// this page only.
/// </summary>
internal sealed class IosOcrPage : IOcrPage
{
    private const float MarginInLineHeights = 0.1f;

    private readonly CGImage _original;
    private readonly float _scaleX;
    private readonly float _scaleY;

    public IosOcrPage(PixelImage detection, CGImage original)
    {
        Detection = detection;
        _original = original;
        _scaleX = (float)original.Width / detection.Width;
        _scaleY = (float)original.Height / detection.Height;
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

    public void Dispose() => _original.Dispose();

    private PixelImage? CropFromOriginal(OcrBox box, int recognitionHeight)
    {
        var scaled = new OcrBox(box.Left * _scaleX, box.Top * _scaleY, box.Right * _scaleX, box.Bottom * _scaleY);
        float lineHeight = Math.Min(scaled.Width, scaled.Height);
        if (SourceRegion(scaled, lineHeight * MarginInLineHeights) is not { } region)
        {
            return null;
        }

        using CGImage? cropped = _original.WithImageInRect(region);
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
    private CGRect? SourceRegion(OcrBox box, float margin)
    {
        int left = Math.Clamp((int)MathF.Floor(box.Left - margin), 0, (int)_original.Width);
        int top = Math.Clamp((int)MathF.Floor(box.Top - margin), 0, (int)_original.Height);
        int right = Math.Clamp((int)MathF.Ceiling(box.Right + margin), 0, (int)_original.Width);
        int bottom = Math.Clamp((int)MathF.Ceiling(box.Bottom + margin), 0, (int)_original.Height);
        return right > left && bottom > top ? new CGRect(left, top, right - left, bottom - top) : null;
    }
}
