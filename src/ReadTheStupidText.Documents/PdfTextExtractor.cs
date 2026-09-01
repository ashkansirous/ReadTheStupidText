using ReadTheStupidText.Application.Documents;
using UglyToad.PdfPig;

namespace ReadTheStupidText.Documents;

/// <summary>Reads a <c>.pdf</c> file's text-layer contents, page by page in
/// order, via PdfPig (Apache-2.0). Rejects documents above <see cref="MaxPages"/>
/// so an oversized scan doesn't hang synthesis on tens of thousands of words
/// (Decision 35) — image-only/scanned PDFs have no text layer and yield empty
/// pages, not an error; there is no OCR fallback.</summary>
public sealed class PdfTextExtractor : IDocumentTextExtractor
{
    private const string Extension = ".pdf";
    private const int MaxPages = 200;

    public bool CanHandle(string extension) =>
        string.Equals(extension, Extension, StringComparison.OrdinalIgnoreCase);

    public Task<string> ExtractTextAsync(string filePath) => Task.Run(() =>
    {
        using PdfDocument document = PdfDocument.Open(filePath);
        if (document.NumberOfPages > MaxPages)
        {
            throw new DocumentTooLargeException(document.NumberOfPages, MaxPages);
        }

        return string.Join(
            Environment.NewLine,
            document.GetPages().Select(page => page.Text));
    });
}
