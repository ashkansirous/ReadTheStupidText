using Android.Graphics;
using Rect = Android.Graphics.Rect;
using RectF = Android.Graphics.RectF;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// One scanned page (Slice 52): the downscaled detection image plus a region decoder on the
/// original. A detected box is in the detection image's upright coordinates; it is scaled up to the
/// original's size, mapped back through the inverse of the EXIF transform to the stored (unrotated)
/// coordinates the region decoder works in, decoded with a small margin at a sample size that keeps
/// the line about twice the recognizer height, and then turned upright with the page's transform.
/// The decoder and its stream live for this page only.
/// </summary>
internal sealed class AndroidOcrPage : IOcrPage
{
    private const float MarginInLineHeights = 0.1f;

    private readonly BitmapRegionDecoder? _decoder;
    private readonly Stream? _stream;
    private readonly int _orientation;
    private readonly int _sourceWidth;
    private readonly int _sourceHeight;
    private readonly Matrix? _uprightToSource;
    private readonly float _scaleX;
    private readonly float _scaleY;
    private readonly int _uprightWidth;
    private readonly int _uprightHeight;

    public AndroidOcrPage(PixelImage detection, BitmapRegionDecoder? decoder, Stream? stream, int orientation, int sourceWidth, int sourceHeight)
    {
        Detection = detection;
        _decoder = decoder;
        _stream = stream;
        _orientation = orientation;
        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;
        bool swap = ImageOrientation.SwapsAxes(orientation);
        _uprightWidth = swap ? sourceHeight : sourceWidth;
        _uprightHeight = swap ? sourceWidth : sourceHeight;
        _scaleX = (float)_uprightWidth / detection.Width;
        _scaleY = (float)_uprightHeight / detection.Height;
        _uprightToSource = decoder is null ? null : InverseOf(orientation, sourceWidth, sourceHeight);
    }

    public PixelImage Detection { get; }

    public PixelImage? CropLine(OcrBox box, int recognitionHeight)
    {
        if (_decoder is null || _uprightToSource is null)
        {
            return ImageResizer.Crop(Detection, box);
        }

        try
        {
            return CropFromOriginal(box, recognitionHeight);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Region decode failed, cropping from the detection image: {ex.Message}");
            return ImageResizer.Crop(Detection, box);
        }
    }

    public void Dispose()
    {
        _decoder?.Recycle();
        _decoder?.Dispose();
        _stream?.Dispose();
        _uprightToSource?.Dispose();
    }

    private PixelImage? CropFromOriginal(OcrBox box, int recognitionHeight)
    {
        var upright = new RectF(box.Left * _scaleX, box.Top * _scaleY, box.Right * _scaleX, box.Bottom * _scaleY);
        float lineHeight = Math.Min(upright.Width(), upright.Height());
        Rect? region = SourceRegion(upright, lineHeight * MarginInLineHeights);
        if (region is null)
        {
            return null;
        }

        var options = new BitmapFactory.Options
        {
            InSampleSize = CropSampling.SampleSize(lineHeight, recognitionHeight),
            InPreferredConfig = Bitmap.Config.Argb8888!,
        };
        using Bitmap? decoded = _decoder!.DecodeRegion(region, options);
        if (decoded is null)
        {
            return ImageResizer.Crop(Detection, box);
        }

        using Bitmap oriented = ImageOrientation.Apply(decoded, _orientation);
        return BitmapPixels.ToPixelImage(oriented);
    }

    // The margin-grown box in upright pixels, mapped to the stored image and clamped to it; null when empty.
    private Rect? SourceRegion(RectF upright, float margin)
    {
        var grown = new RectF(
            Math.Max(0, upright.Left - margin),
            Math.Max(0, upright.Top - margin),
            Math.Min(_uprightWidth, upright.Right + margin),
            Math.Min(_uprightHeight, upright.Bottom + margin));
        _uprightToSource!.MapRect(grown);
        int left = Math.Clamp((int)MathF.Floor(grown.Left), 0, _sourceWidth);
        int top = Math.Clamp((int)MathF.Floor(grown.Top), 0, _sourceHeight);
        int right = Math.Clamp((int)MathF.Ceiling(grown.Right), 0, _sourceWidth);
        int bottom = Math.Clamp((int)MathF.Ceiling(grown.Bottom), 0, _sourceHeight);
        return right > left && bottom > top ? new Rect(left, top, right, bottom) : null;
    }

    // Stored pixels -> upright pixels is the EXIF matrix plus the shift that brings the result to the
    // origin (the same shift Bitmap.CreateBitmap applies); this is its inverse.
    private static Matrix? InverseOf(int orientation, int sourceWidth, int sourceHeight)
    {
        using Matrix? forward = ImageOrientation.MatrixFor(orientation);
        var inverse = new Matrix();
        if (forward is null)
        {
            return inverse;
        }

        var bounds = new RectF(0, 0, sourceWidth, sourceHeight);
        forward.MapRect(bounds);
        forward.PostTranslate(-bounds.Left, -bounds.Top);
        if (forward.Invert(inverse))
        {
            return inverse;
        }

        inverse.Dispose();
        return null;
    }
}
