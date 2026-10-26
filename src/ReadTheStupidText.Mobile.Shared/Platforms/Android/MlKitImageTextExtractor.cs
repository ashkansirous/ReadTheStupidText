using Android.Gms.Extensions;
using ReadTheStupidText.Application.Images;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Extracts text from a captured photo using Google ML Kit's on-device Latin text
/// recognizer (Decision 40) — chosen over Cloud Vision (network per call, breaks
/// the "we collect nothing" stance) and Tesseract (lower out-of-the-box accuracy).
/// Confirmed by unzipping the built APK: the recognizer's model files ship
/// directly under <c>assets/mlkit-google-ocr-models/</c> and its native
/// libraries under <c>lib/&lt;abi&gt;/</c> — no Play-services download, no
/// network at all; recognition is fully on-device from first launch, the same
/// as every other engine this app ships.
/// </summary>
public sealed class MlKitImageTextExtractor : IBuiltInTextRecognizer
{
    private const int MaxLongEdge = 4096;

    private readonly Android.Content.Context _context;

    // One recognizer for the process: creating one loads the native model, which is wasteful
    // per page. ML Kit recognizers are safe to share across calls; the extractor is a singleton,
    // so it is never disposed.
    private readonly Lazy<ITextRecognizer> _recognizer = new(
        () => TextRecognition.GetClient(TextRecognizerOptions.DefaultOptions),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public MlKitImageTextExtractor(Android.Content.Context context) => _context = context;

    /// <summary>ML Kit's Latin recognizer reads Latin-script languages only (no Arabic, Persian, Cyrillic, CJK).</summary>
    public bool Covers(string language) => OcrLanguages.IsLatinScript(language);

    /// <summary>
    /// The recognized lines with their boxes (Slice 63), plus the pixel size of the frame the boxes
    /// are in. The image is decoded and turned upright here from its EXIF orientation (the same
    /// transform the PaddleOCR loader and the page keeper use), then given to ML Kit as an upright
    /// bitmap with rotation 0, so the frame is exactly that bitmap's width and height.
    /// ML Kit's reference documents <c>InputImage.fromBitmap(bitmap, rotationDegrees)</c> (rotation
    /// is "the image's counter-clockwise orientation degrees", 0/90/180/270) but says nothing about
    /// whether <c>fromFilePath(context, uri)</c> reads EXIF, pre-rotates the bitmap, or what
    /// <c>getWidth</c>/<c>getHeight</c> report after a rotation, so the frame is not left to it.
    /// Device check: boxes over an EXIF-rotated photo (see the Slice 63 file).
    /// </summary>
    public async Task<OcrReading> ExtractLinesAsync(string imagePath)
    {
        using Android.Graphics.Bitmap upright = DecodeUpright(imagePath);
        using InputImage image = InputImage.FromBitmap(upright, 0);
        Java.Lang.Object result = await _recognizer.Value.Process(image);
        Text text = (Text)result;
        List<OcrLine> lines = text.TextBlocks
            .SelectMany(block => block.Lines)
            .Select(ToLine)
            .ToList();
        return new OcrReading(lines, upright.Width, upright.Height, text.GetText());
    }

    // Always a bitmap the caller owns and disposes (never the intermediate one, which is disposed here).
    private Android.Graphics.Bitmap DecodeUpright(string imagePath)
    {
        (int width, int height) = AndroidImageSource.ReadBounds(_context, imagePath);
        int orientation = AndroidImageSource.ReadOrientation(_context, imagePath);
        var options = new Android.Graphics.BitmapFactory.Options { InSampleSize = SampleSize(Math.Max(width, height)) };
        using Stream stream = AndroidImageSource.Open(_context, imagePath);
        using Android.Graphics.Bitmap decoded = Android.Graphics.BitmapFactory.DecodeStream(stream, null, options)
            ?? throw new IOException($"Could not decode the image '{imagePath}'.");
        Android.Graphics.Bitmap upright = ImageOrientation.Apply(decoded, orientation);
        return ReferenceEquals(upright, decoded) ? decoded.Copy(decoded.GetConfig()!, false)! : upright;
    }

    // Smallest power of two that brings the long edge to at or below MaxLongEdge.
    private static int SampleSize(int longestSide)
    {
        int sample = 1;
        while (longestSide / sample > MaxLongEdge)
        {
            sample *= 2;
        }

        return sample;
    }

    private static OcrLine ToLine(Text.Line line)
    {
        Android.Graphics.Rect? box = line.BoundingBox;
        return new OcrLine(line.Text ?? string.Empty, box is null ? default : new OcrBox(box.Left, box.Top, box.Right, box.Bottom));
    }
}
