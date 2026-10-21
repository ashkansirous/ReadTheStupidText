using Foundation;
using ReadTheStupidText.Application.Images;
using Vision;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// <see cref="IImageTextExtractor"/> over Apple's on-device Vision text recognition
/// (<see cref="VNRecognizeTextRequest"/>, accurate level), the iOS counterpart of
/// <c>MlKitImageTextExtractor</c> (Slice 49, Decision 65). It is a stand-in: Slice 55 replaces it
/// with PaddleOCR for the languages Vision reads poorly (Arabic and Farsi in particular).
/// Everything runs on the device; the image is never uploaded.
/// </summary>
public sealed class VisionImageTextExtractor : IImageTextExtractor
{
    /// <param name="imagePath">A plain file path or a <c>file://</c> uri string.</param>
    public Task<string> ExtractTextAsync(string imagePath) => Task.Run(() => Recognize(imagePath));

    private static string Recognize(string imagePath)
    {
        string path = ScannedText.ToLocalPath(imagePath)
            ?? throw new InvalidOperationException($"Not a local image: '{imagePath}'.");

        // Built from the file url so Vision applies the image's own EXIF orientation.
        using var url = NSUrl.FromFilename(path);
        using var handler = new VNImageRequestHandler(url, new NSDictionary());
        using var request = new VNRecognizeTextRequest(null)
        {
            RecognitionLevel = VNRequestTextRecognitionLevel.Accurate,
            UsesLanguageCorrection = true,
        };

        if (OperatingSystem.IsIOSVersionAtLeast(16))
        {
            request.AutomaticallyDetectsLanguage = true;
        }

        handler.Perform([request], out NSError? error);
        if (error is not null)
        {
            throw new InvalidOperationException(error.LocalizedDescription);
        }

        return JoinLines(request.GetResults<VNRecognizedTextObservation>());
    }

    // Vision returns one observation per recognised line, in reading order (top to bottom).
    private static string JoinLines(VNRecognizedTextObservation[]? observations)
    {
        if (observations is null)
        {
            return string.Empty;
        }

        IEnumerable<string?> lines = observations
            .Select(o => o.TopCandidates(1).FirstOrDefault()?.String)
            .Where(line => !string.IsNullOrWhiteSpace(line));
        return string.Join('\n', lines);
    }
}
