using Android.Graphics;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Opens a scanned page for the OCR engine (Slice 52). The detection image is decoded with
/// <see cref="BitmapFactory"/>, sampled down by a power of two so the longest side is at or below
/// the requested maximum and a 12 MP photo never sits in memory at full size, then turned upright
/// from its EXIF orientation (all eight, mirrored ones included) and converted row by row to the
/// B, G, R bytes the models expect. Recognition crops are decoded afresh from the original with a
/// <see cref="BitmapRegionDecoder"/> (see <see cref="AndroidOcrPage"/>), so small print keeps its detail.
/// </summary>
public sealed class AndroidPixelImageLoader : IPixelImageLoader
{
    private readonly Android.Content.Context _context;

    public AndroidPixelImageLoader(Android.Content.Context context) => _context = context;

    public IOcrPage Open(string imagePath, int maxSide)
    {
        (int width, int height) = AndroidImageSource.ReadBounds(_context, imagePath);
        int orientation = AndroidImageSource.ReadOrientation(_context, imagePath);
        PixelImage detection = LoadDetection(imagePath, SampleSize(Math.Max(width, height), maxSide), orientation);
        (BitmapRegionDecoder? decoder, Stream? stream) = TryOpenRegionDecoder(imagePath);
        return new AndroidOcrPage(detection, decoder, stream, orientation, width, height);
    }

    // Null decoder when the format has none: crops then come from the detection image.
    private (BitmapRegionDecoder? Decoder, Stream? Stream) TryOpenRegionDecoder(string imagePath)
    {
        Stream? stream = null;
        try
        {
            string? local = ScannedText.ToLocalPath(imagePath);
            if (local is not null)
            {
                return (BitmapRegionDecoder.NewInstance(local), null);
            }

            stream = AndroidImageSource.Open(_context, imagePath);
            BitmapRegionDecoder? decoder = BitmapRegionDecoder.NewInstance(stream);
            if (decoder is null)
            {
                stream.Dispose();
            }

            return (decoder, decoder is null ? null : stream);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"No region decoder for '{imagePath}', cropping from the detection image: {ex.Message}");
            stream?.Dispose();
            return (null, null);
        }
    }

    // Both bitmaps are disposed on every path, including a failed rotation.
    private PixelImage LoadDetection(string imagePath, int sampleSize, int orientation)
    {
        using Bitmap decoded = Decode(imagePath, sampleSize);
        using Bitmap upright = ImageOrientation.Apply(decoded, orientation);
        return BitmapPixels.ToPixelImage(upright);
    }

    // A decode failure is a plain I/O error of this image, not a model fault (see LanguageImageTextExtractor).
    private Bitmap Decode(string imagePath, int sampleSize)
    {
        var options = new BitmapFactory.Options { InSampleSize = sampleSize, InPreferredConfig = Bitmap.Config.Argb8888! };
        using Stream stream = AndroidImageSource.Open(_context, imagePath);
        return BitmapFactory.DecodeStream(stream, null, options)
            ?? throw new IOException($"Could not decode the image '{imagePath}'.");
    }

    // Smallest power of two that brings the decoded image to at or below maxSide.
    private static int SampleSize(int longestSide, int maxSide)
    {
        int sample = 1;
        while (longestSide / sample > maxSide)
        {
            sample *= 2;
        }

        return sample;
    }
}
