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

    public MlKitImageTextExtractor(Android.Content.Context context) => _context = context;

    public async Task<string> ExtractTextAsync(string imagePath)
    {
        using ITextRecognizer recognizer =
            TextRecognition.GetClient(TextRecognizerOptions.DefaultOptions);
        AndroidNet.Uri uri = AndroidNet.Uri.FromFile(new Java.IO.File(imagePath))
            ?? throw new InvalidOperationException($"Could not build a content Uri for '{imagePath}'.");
        InputImage image = InputImage.FromFilePath(_context, uri);

        Java.Lang.Object result = await recognizer.Process(image);
        return ((Text)result).GetText() ?? string.Empty;
    }
}
