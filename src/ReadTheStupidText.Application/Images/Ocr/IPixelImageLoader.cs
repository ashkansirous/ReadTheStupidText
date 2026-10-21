namespace ReadTheStupidText.Application.Images;

/// <summary>
/// The one platform-specific piece of the OCR engine: open an image file as an <see cref="IOcrPage"/>
/// (upright, EXIF orientation applied) whose detection image is no larger than <c>maxSide</c> pixels
/// on its longest side.
/// </summary>
public interface IPixelImageLoader
{
    IOcrPage Open(string imagePath, int maxSide);
}

/// <summary>
/// One page being read. <see cref="Detection"/> is the downscaled upright image the detector sees;
/// <see cref="CropLine"/> cuts a recognition crop for a box in that image's coordinates, from the
/// original at a higher resolution where the platform can. Held for one page only, then disposed.
/// </summary>
public interface IOcrPage : IDisposable
{
    PixelImage Detection { get; }

    /// <summary>The upright crop of a detected line, or null when nothing is left of the box.</summary>
    PixelImage? CropLine(OcrBox box, int recognitionHeight);
}

/// <summary>A page held fully in memory: crops are cut from the detection image itself.</summary>
public sealed class PixelOcrPage : IOcrPage
{
    public PixelOcrPage(PixelImage image) => Detection = image;

    public PixelImage Detection { get; }

    public PixelImage? CropLine(OcrBox box, int recognitionHeight) => ImageResizer.Crop(Detection, box);

    public void Dispose()
    {
    }
}
