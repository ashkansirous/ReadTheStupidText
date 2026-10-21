using System.Globalization;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Documents;

/// <summary>
/// The PDF <see cref="IPositionedTextExtractor"/> for Dino Reader (Slice 63): the text layer of
/// <see cref="PdfPositionedTextExtractor"/>, with OCR of a rendered page image for any page that
/// has no text layer. Only those pages are rendered during extraction, in one session; a page with
/// a text layer keeps a null image until a caller asks for it with <see cref="RenderPagesAsync"/>
/// (picture mode). A page that cannot be rendered, or whose OCR fails, keeps its text-layer result (empty when it has
/// none) and the other pages still return.
/// </summary>
public sealed class RenderedPdfTextExtractor : IPositionedTextExtractor
{
    private const string PageImageFormat = "page-{0}.jpg";

    private readonly IPositionedTextExtractor _textLayer;
    private readonly IPdfPageRenderer _renderer;
    private readonly OcrPositionedTextExtractor _ocr;
    private readonly string _imageFolder;

    public RenderedPdfTextExtractor(
        IPositionedTextExtractor textLayer, IPdfPageRenderer renderer, OcrPositionedTextExtractor ocr, string imageFolder)
    {
        _textLayer = textLayer;
        _renderer = renderer;
        _ocr = ocr;
        _imageFolder = imageFolder;
    }

    public async Task<IReadOnlyList<PositionedPage>> ExtractPagesAsync(string path)
    {
        IReadOnlyList<PositionedPage> pages = await _textLayer.ExtractPagesAsync(path).ConfigureAwait(false);
        int[] needOcr = pages.Where(p => p.Lines.Count == 0).Select(p => p.Index).ToArray();
        IReadOnlyDictionary<int, string> images = await RenderPagesAsync(path, needOcr).ConfigureAwait(false);
        var result = new List<PositionedPage>(pages.Count);
        foreach (PositionedPage page in pages)
        {
            result.Add(await ReadPageAsync(page, images).ConfigureAwait(false));
        }

        return result;
    }

    /// <summary>
    /// Renders the given 0-based pages to the image folder in one session and returns the image
    /// path per page index. Pages that cannot be rendered are left out, as are all pages when the
    /// document cannot be opened.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, string>> RenderPagesAsync(string pdfPath, IReadOnlyCollection<int> pageIndexes)
    {
        if (pageIndexes.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        Directory.CreateDirectory(_imageFolder);
        List<PdfPageTarget> targets = pageIndexes.Distinct().Select(i => new PdfPageTarget(i, ImagePathFor(i))).ToList();
        try
        {
            return await _renderer.RenderPagesAsync(pdfPath, targets).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Dictionary<int, string>();
        }
    }

    private async Task<PositionedPage> ReadPageAsync(PositionedPage page, IReadOnlyDictionary<int, string> images)
    {
        if (page.Lines.Count > 0 || !images.TryGetValue(page.Index, out string? image))
        {
            return page;
        }

        try
        {
            return await _ocr.ExtractPageAsync(image, page.Index, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return page;
        }
    }

    private string ImagePathFor(int pageIndex) =>
        Path.Combine(_imageFolder, string.Format(CultureInfo.InvariantCulture, PageImageFormat, pageIndex));
}
