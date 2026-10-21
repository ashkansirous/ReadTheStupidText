using Android.Gms.Extensions;
using AndroidNet = Android.Net;
using ReadTheStupidText.Application.Images;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;

namespace ReadTheStupidText.Mobile;

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
public sealed class MlKitImageTextExtractor : IImageTextExtractor
{
    private readonly Android.Content.Context _context;

    // One recognizer for the process: creating one loads the native model, which is wasteful
    // per page. ML Kit recognizers are safe to share across calls; the extractor is a singleton,
    // so it is never disposed.
    private readonly Lazy<ITextRecognizer> _recognizer = new(
        () => TextRecognition.GetClient(TextRecognizerOptions.DefaultOptions),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public MlKitImageTextExtractor(Android.Content.Context context) => _context = context;

    /// <param name="imagePath">A plain file path, or a <c>file://</c> / <c>content://</c> uri string.</param>
    public async Task<string> ExtractTextAsync(string imagePath)
    {
        InputImage image = InputImage.FromFilePath(_context, ToUri(imagePath));
        Java.Lang.Object result = await _recognizer.Value.Process(image);
        return ((Text)result).GetText() ?? string.Empty;
    }

    /// <summary>The recognized lines with their boxes, for the fallback path of Slice 52 (geometry kept for Slice 63).</summary>
    public async Task<IReadOnlyList<OcrLine>> ExtractLinesAsync(string imagePath)
    {
        InputImage image = InputImage.FromFilePath(_context, ToUri(imagePath));
        Java.Lang.Object result = await _recognizer.Value.Process(image);
        return ((Text)result).TextBlocks
            .SelectMany(block => block.Lines)
            .Select(ToLine)
            .ToList();
    }

    private static OcrLine ToLine(Text.Line line)
    {
        Android.Graphics.Rect? box = line.BoundingBox;
        return new OcrLine(line.Text ?? string.Empty, box is null ? default : new OcrBox(box.Left, box.Top, box.Right, box.Bottom));
    }

    private static AndroidNet.Uri ToUri(string imagePath)
    {
        AndroidNet.Uri? uri = imagePath.Contains("://", StringComparison.Ordinal)
            ? AndroidNet.Uri.Parse(imagePath)
            : AndroidNet.Uri.FromFile(new Java.IO.File(imagePath));
        return uri ?? throw new InvalidOperationException($"Could not build a Uri for '{imagePath}'.");
    }
}
