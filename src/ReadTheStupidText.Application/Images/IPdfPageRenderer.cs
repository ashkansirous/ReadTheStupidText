namespace ReadTheStupidText.Application.Images;

/// <summary>One page to render: its 0-based index and the file to write.</summary>
public sealed record PdfPageTarget(int PageIndex, string OutputPath);

/// <summary>
/// Renders PDF pages to image files (Slice 63), for showing a page and for OCR of one that has no
/// text layer. A set of pages is rendered in one call so the document is opened once. Android's
/// PdfRenderer implements it first; iOS and Windows renderers come with their own slices.
/// </summary>
public interface IPdfPageRenderer
{
    /// <summary>
    /// Renders each target page and returns the output path per page index. A page that cannot be
    /// rendered (out of range, or a render error) is left out of the result; a document that cannot
    /// be opened throws.
    /// </summary>
    Task<IReadOnlyDictionary<int, string>> RenderPagesAsync(string pdfPath, IReadOnlyList<PdfPageTarget> pages);
}
