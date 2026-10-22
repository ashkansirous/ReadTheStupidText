using CoreGraphics;
using Foundation;
using ImageIO;
using ReadTheStupidText.Application.Images;
using Vision;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Apple's on-device Vision text recognition (<see cref="VNRecognizeTextRequest"/>, accurate
/// level), the iOS counterpart of <c>MlKitImageTextExtractor</c> (Slice 49, Decision 65). Since
/// Slice 55 it is the built-in fallback behind <see cref="LanguageImageTextExtractor"/>: used
/// while a language's PaddleOCR pack is not installed, and when that pack cannot run. Everything
/// runs on the device; the image is never uploaded.
/// </summary>
public sealed class VisionImageTextExtractor : IBuiltInTextRecognizer
{
    // Japanese, Chinese and Russian (Cyrillic) are read only on iOS 16+, where the request detects the
    // language itself (see Recognize). iOS 15 reads Latin, so those languages wait for a pack there.
    // Arabic and Persian always wait for a pack.
    private static readonly HashSet<string> NonLatinCovered = new(StringComparer.OrdinalIgnoreCase) { "ja", "zh", "ru" };

    // EXIF orientations 5 to 8 are the ones that rotate the stored image a quarter turn.
    private const int FirstQuarterTurnOrientation = 5;

    public bool Covers(string language) =>
        OcrLanguages.IsLatinScript(language) || (OperatingSystem.IsIOSVersionAtLeast(16) && NonLatinCovered.Contains(language));

    /// <summary>The recognised lines in reading order, with their boxes in the upright image's pixels and that frame's size.</summary>
    public Task<OcrReading> ExtractLinesAsync(string imagePath) => Task.Run(() => Recognize(imagePath));

    private static OcrReading Recognize(string imagePath)
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

        (float width, float height) = UprightSize(url);
        IReadOnlyList<OcrLine> lines = ToLines(request.GetResults<VNRecognizedTextObservation>(), (width, height));
        return new OcrReading(lines, (int)width, (int)height);
    }

    // Vision returns one observation per recognised line, in reading order (top to bottom), with
    // a box normalised to the upright image and measured from its bottom-left corner.
    private static IReadOnlyList<OcrLine> ToLines(VNRecognizedTextObservation[]? observations, (float Width, float Height) size)
    {
        if (observations is null)
        {
            return [];
        }

        return observations
            .Select(o => (Text: o.TopCandidates(1).FirstOrDefault()?.String, o.BoundingBox))
            .Where(o => !string.IsNullOrWhiteSpace(o.Text))
            .Select(o => new OcrLine(o.Text!, ToBox(o.BoundingBox, size)))
            .ToList();
    }

    private static OcrBox ToBox(CGRect normalized, (float Width, float Height) size) =>
        OcrBox.FromNormalizedBottomLeft(normalized.X, normalized.Y, normalized.Width, normalized.Height, size.Width, size.Height);

    // The image's size after its EXIF orientation: the stored width and height swap for the four
    // orientations that rotate a quarter turn. Vision's boxes are normalised, so without the size
    // every box would collapse to zero: an unreadable size fails this read, and the caller (which
    // is already the last resort) reports it, instead of returning boxes that look valid.
    private static (float Width, float Height) UprightSize(NSUrl url)
    {
        using CGImageSource? source = CGImageSource.FromUrl(url);
        var properties = source?.GetProperties(0);
        float width = properties?.PixelWidth ?? 0;
        float height = properties?.PixelHeight ?? 0;
        if (width <= 0 || height <= 0)
        {
            throw new IOException($"Could not read the size of the image '{url.Path}'.");
        }

        bool swap = (int)(properties?.Orientation ?? 0) >= FirstQuarterTurnOrientation;
        return swap ? (height, width) : (width, height);
    }
}
