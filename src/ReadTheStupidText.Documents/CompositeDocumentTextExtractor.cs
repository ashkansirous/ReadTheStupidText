using ReadTheStupidText.Application.Documents;

namespace ReadTheStupidText.Documents;

/// <summary>
/// Routes an uploaded file to the extractor for its extension, mirroring
/// <c>CompositeSpeechReader</c>'s composition pattern: one named member per
/// supported type rather than a generic registered list, so each new file type
/// is added explicitly as it's implemented. Lives in this portable
/// `ReadTheStupidText.Documents` library — not Application (where it started
/// in Slice 27), since `PdfTextExtractor` depends on the PdfPig package and
/// Application stays framework/library-free; not the Windows-only
/// Infrastructure project either (where it moved to next), since none of these
/// extractors actually need a Windows API, and the Android Mobile project
/// (Slice 33) needs to reuse them unchanged.
/// </summary>
public sealed class CompositeDocumentTextExtractor : IDocumentTextExtractor
{
    private readonly IReadOnlyList<IDocumentTextExtractor> _extractors;

    public CompositeDocumentTextExtractor(PlainTextExtractor plainText, PdfTextExtractor pdf, DocxTextExtractor docx)
    {
        _extractors = [plainText, pdf, docx];
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
