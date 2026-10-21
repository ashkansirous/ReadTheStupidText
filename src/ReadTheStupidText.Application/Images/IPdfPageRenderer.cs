namespace ReadTheStupidText.Application.Images;

/// <summary>
/// Renders one PDF page to an image file (Slice 63), for showing a page that has a text layer and
/// for OCR of one that has none. Android's PdfRenderer implements it first; iOS and Windows
/// renderers come with their own slices.
/// </summary>
public interface IPdfPageRenderer
{
    /// <summary>Renders the 0-based <paramref name="pageIndex"/> to <paramref name="outputPath"/> and returns that path.</summary>
    Task<string> RenderPageAsync(string pdfPath, int pageIndex, string outputPath);
}
