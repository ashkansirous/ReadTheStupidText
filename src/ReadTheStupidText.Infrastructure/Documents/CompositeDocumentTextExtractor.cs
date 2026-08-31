using ReadTheStupidText.Application.Documents;

namespace ReadTheStupidText.Infrastructure.Documents;

/// <summary>
/// Routes an uploaded file to the extractor for its extension, mirroring
/// <c>CompositeSpeechReader</c>'s composition pattern: one named member per
/// supported type rather than a generic registered list, so each new file type
/// (DOCX) is added explicitly as it's implemented. Lives in Infrastructure
/// (not Application, where it started in Slice 27) because PdfTextExtractor
/// depends on the PdfPig package — Application stays framework/library-free.
/// </summary>
public sealed class CompositeDocumentTextExtractor : IDocumentTextExtractor
{
    private readonly IReadOnlyList<IDocumentTextExtractor> _extractors;

    public CompositeDocumentTextExtractor(PlainTextExtractor plainText, PdfTextExtractor pdf)
    {
        _extractors = [plainText, pdf];
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
