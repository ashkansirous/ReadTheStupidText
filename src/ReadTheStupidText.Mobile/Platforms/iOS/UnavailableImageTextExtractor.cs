using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile;

/// <summary>Placeholder OCR for iOS until Slice 49 adds the Vision-framework extractor.</summary>
public sealed class UnavailableImageTextExtractor : IImageTextExtractor
{
    public Task<string> ExtractTextAsync(string imagePath) =>
        throw new OcrUnavailableException();
}
