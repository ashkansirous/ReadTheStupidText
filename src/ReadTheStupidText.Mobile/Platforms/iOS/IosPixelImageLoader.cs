using CoreGraphics;
using Foundation;
using ImageIO;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Opens a scanned page for the OCR engine on iOS (Slice 55). ImageIO decodes the file upright
/// (its EXIF orientation applied, mirrored ones included) at no more than
/// <see cref="MaxRecognitionSide"/> pixels on its longest side; the detection image is that picture
/// drawn down to the requested size and converted to B, G, R bytes. The upright picture itself is
/// kept for the page, so recognition crops come from more pixels than the detector saw.
/// </summary>
public sealed class IosPixelImageLoader : IPixelImageLoader
{
    // 4096 x 3072 x 4 bytes is about 50 MB once decoded: enough detail for small print on a
    // 12 MP photo without a 48 MP one costing 200 MB.
    private const int MaxRecognitionSide = 4096;

    public IOcrPage Open(string imagePath, int maxSide)
    {
        string path = ScannedText.ToLocalPath(imagePath)
            ?? throw new IOException($"Not a local image: '{imagePath}'.");

        CGImage original = DecodeUpright(path);
        try
        {
            return new IosOcrPage(DetectionImage(original, maxSide), original);
        }
        catch
        {
            original.Dispose();
            throw;
        }
    }

    // A decode failure is a plain I/O error of this image, not a model fault (see IosLanguageImageTextExtractor).
    private static CGImage DecodeUpright(string path)
    {
        using var url = NSUrl.FromFilename(path);
        using CGImageSource source = CGImageSource.FromUrl(url)
            ?? throw new IOException($"Could not read the image '{path}'.");

        var options = new CGImageThumbnailOptions
        {
            MaxPixelSize = Math.Min(MaxRecognitionSide, LongestSide(source, path)),
            CreateThumbnailFromImageAlways = true,
            CreateThumbnailWithTransform = true,
        };

        return source.CreateThumbnail(0, options)
            ?? throw new IOException($"Could not decode the image '{path}'.");
    }

    // Asked for explicitly so a small image is never enlarged by the thumbnail request.
    private static int LongestSide(CGImageSource source, string path)
    {
        var properties = source.GetProperties(0)
            ?? throw new IOException($"Could not read the size of the image '{path}'.");
        int longest = Math.Max(properties.PixelWidth ?? 0, properties.PixelHeight ?? 0);
        return longest > 0 ? longest : throw new IOException($"The image '{path}' has no size.");
    }

    private static PixelImage DetectionImage(CGImage original, int maxSide)
    {
        (int width, int height) = ImageResizer.FitWithin((int)original.Width, (int)original.Height, maxSide);
        return CgImagePixels.ToPixelImage(original, width, height);
    }
}
