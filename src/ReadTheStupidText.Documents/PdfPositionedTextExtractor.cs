using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Application.Images;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ReadTheStupidText.Documents;

/// <summary>
/// Reads a PDF's text layer with each line's position (Slice 63), via PdfPig's word extractor.
/// A page with no text layer yields no lines; the OCR-of-the-rendered-page fallback arrives with
/// Slice 52's line extractor. Page rotation is not applied (story books are upright).
/// </summary>
public sealed class PdfPositionedTextExtractor : IPositionedTextExtractor
{
    private const int MaxPages = 200;

    public Task<IReadOnlyList<PositionedPage>> ExtractPagesAsync(string path) => Task.Run(() =>
    {
        using PdfDocument document = PdfDocument.Open(path);
        if (document.NumberOfPages > MaxPages)
        {
            throw new DocumentTooLargeException(document.NumberOfPages, MaxPages);
        }

        return (IReadOnlyList<PositionedPage>)document.GetPages().Select(ToPage).ToList();
    });

    private static PositionedPage ToPage(Page page)
    {
        List<PositionedWord> words = page.GetWords().Select(w => ToWord(w, page.Height)).ToList();
        IReadOnlyList<TextLine> lines = WordLineGrouper.Group(words, page.Width, page.Height);
        return new PositionedPage(page.Number - 1, null, lines);
    }

    // PDF space has its origin bottom-left; flip y so the box is top-left based.
    private static PositionedWord ToWord(Word word, double pageHeight) => new(
        word.Text,
        word.BoundingBox.Left,
        pageHeight - word.BoundingBox.Top,
        word.BoundingBox.Width,
        word.BoundingBox.Height);
}
