namespace ReadTheStupidText.Application.Images;

/// <summary>
/// Extracts text from a captured photo (Decision 40) so it can feed the existing
/// read pipeline exactly like typed text or an uploaded document — OCR is just
/// another text source, not new reading logic. Parallel to
/// <c>IDocumentTextExtractor</c>, but there is only ever one implementation
/// (no routing by extension): a single-shot camera capture is always an image.
/// </summary>
public interface IImageTextExtractor
{
    Task<string> ExtractTextAsync(string imagePath);
}
