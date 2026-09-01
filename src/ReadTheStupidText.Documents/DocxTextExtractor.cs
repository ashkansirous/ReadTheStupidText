using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ReadTheStupidText.Application.Documents;

namespace ReadTheStupidText.Documents;

/// <summary>Reads a <c>.docx</c> file's paragraph text, in document order, via
/// the Open XML SDK (MIT). Enforces the same soft page cap as
/// <see cref="PdfTextExtractor"/> (Decision 35), read from the document's
/// cached <c>app.xml</c> page count (<c>ExtendedFilePropertiesPart</c>) — the
/// only page count OOXML carries, since a .docx has no fixed pagination.
/// Documents lacking that property (never opened/saved in Word) skip the cap
/// rather than being rejected on a number we don't have.</summary>
public sealed class DocxTextExtractor : IDocumentTextExtractor
{
    private const string Extension = ".docx";
    private const int MaxPages = 200;

    public bool CanHandle(string extension) =>
        string.Equals(extension, Extension, StringComparison.OrdinalIgnoreCase);

    public Task<string> ExtractTextAsync(string filePath) => Task.Run(() =>
    {
        using WordprocessingDocument document = WordprocessingDocument.Open(filePath, false);
        int? pageCount = ReadCachedPageCount(document);
        if (pageCount is { } pages && pages > MaxPages)
        {
            throw new DocumentTooLargeException(pages, MaxPages);
        }

        Body? body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            body.Descendants<Paragraph>().Select(paragraph => paragraph.InnerText));
    });

    private static int? ReadCachedPageCount(WordprocessingDocument document)
    {
        string? text = document.ExtendedFilePropertiesPart?.Properties?.Pages?.Text;
        return int.TryParse(text, out int pages) ? pages : null;
    }
}
