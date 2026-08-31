namespace ReadTheStupidText.Application.Documents;

/// <summary>
/// Routes an uploaded file to the extractor for its extension, mirroring
/// <c>CompositeSpeechReader</c>'s composition pattern (Infrastructure.Reading):
/// one named member per supported type rather than a generic registered list, so
/// each new file type (PDF, DOCX) is added explicitly as it's implemented.
/// </summary>
public sealed class CompositeDocumentTextExtractor : IDocumentTextExtractor
{
    private readonly IReadOnlyList<IDocumentTextExtractor> _extractors;

    public CompositeDocumentTextExtractor(PlainTextExtractor plainText)
    {
        _extractors = [plainText];
    }

    public bool CanHandle(string extension) => FindExtractor(extension) is not null;

    public Task<string> ExtractTextAsync(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        IDocumentTextExtractor extractor = FindExtractor(extension)
            ?? throw new NotSupportedException($"No extractor registered for '{extension}' files.");
        return extractor.ExtractTextAsync(filePath);
    }

    private IDocumentTextExtractor? FindExtractor(string extension) =>
        _extractors.FirstOrDefault(e => e.CanHandle(extension));
}
