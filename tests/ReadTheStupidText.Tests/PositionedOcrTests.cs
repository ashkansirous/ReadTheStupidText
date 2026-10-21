using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Documents;

namespace ReadTheStupidText.Tests;

public class PositionedOcrTests
{
    private const double Tolerance = 0.0001;
    private const string Language = "en";

    [Fact]
    public void ToPage_normalizes_boxes_by_the_image_size()
    {
        var reading = new ImageText([new OcrLine("Hello", new OcrBox(100, 50, 300, 100))], OcrSource.MlKit, 400, 200);

        PositionedPage page = OcrPageMapper.ToPage(reading, 2, "p.jpg");

        Assert.Equal(2, page.Index);
        Assert.Equal("p.jpg", page.ImagePath);
        TextLine line = Assert.Single(page.Lines);
        Assert.Equal(0.25, line.Box.Left, Tolerance);
        Assert.Equal(0.25, line.Box.Top, Tolerance);
        Assert.Equal(0.5, line.Box.Width, Tolerance);
        Assert.Equal(0.25, line.Box.Height, Tolerance);
    }

    [Fact]
    public void ToPage_drops_blank_lines_and_keeps_text_when_the_size_is_unknown()
    {
        var reading = new ImageText(
            [new OcrLine("  ", new OcrBox(0, 0, 1, 1)), new OcrLine(" kept ", new OcrBox(0, 0, 1, 1))], OcrSource.PaddleOcr);

        TextLine line = Assert.Single(OcrPageMapper.ToPage(reading, 0, null).Lines);

        Assert.Equal("kept", line.Text);
        Assert.Equal(default, line.Box);
    }

    [Fact]
    public async Task Image_extractor_reads_with_the_current_language_and_keeps_the_image()
    {
        var ocr = new FakeOcr(("A line", new OcrBox(0, 0, 50, 10)));
        var extractor = new OcrPositionedTextExtractor(ocr, () => Language);

        PositionedPage page = Assert.Single(await extractor.ExtractPagesAsync("scan.jpg"));

        Assert.Equal(Language, ocr.LastLanguage);
        Assert.Equal("scan.jpg", page.ImagePath);
        Assert.Equal("A line", Assert.Single(page.Lines).Text);
    }

    [Fact]
    public async Task Pdf_page_with_a_text_layer_is_not_rendered_during_extraction()
    {
        var ocr = new FakeOcr();
        var renderer = new FakeRenderer();
        var layer = new FakeTextLayer(new PositionedPage(0, null, [new TextLine("text", default)]));
        var extractor = new RenderedPdfTextExtractor(layer, renderer, new OcrPositionedTextExtractor(ocr, () => Language), NewFolder());

        PositionedPage page = Assert.Single(await extractor.ExtractPagesAsync("book.pdf"));

        Assert.Equal("text", Assert.Single(page.Lines).Text);
        Assert.Null(page.ImagePath);
        Assert.Equal(0, renderer.Sessions);
        Assert.Equal(0, ocr.Calls);
    }

    [Fact]
    public async Task RenderPagesAsync_renders_the_requested_pages_in_one_session()
    {
        string folder = NewFolder();
        var renderer = new FakeRenderer();
        var extractor = new RenderedPdfTextExtractor(
            new FakeTextLayer(), renderer, new OcrPositionedTextExtractor(new FakeOcr(), () => Language), folder);

        IReadOnlyDictionary<int, string> images = await extractor.RenderPagesAsync("book.pdf", [0, 2]);

        Assert.Equal(1, renderer.Sessions);
        Assert.Equal(Path.Combine(folder, "page-0.jpg"), images[0]);
        Assert.Equal(Path.Combine(folder, "page-2.jpg"), images[2]);
    }

    [Fact]
    public async Task Pdf_pages_without_a_text_layer_are_rendered_together_in_one_session()
    {
        var renderer = new FakeRenderer();
        var layer = new FakeTextLayer(
            new PositionedPage(0, null, []), new PositionedPage(1, null, [new TextLine("text", default)]), new PositionedPage(2, null, []));
        var extractor = new RenderedPdfTextExtractor(layer, renderer, new OcrPositionedTextExtractor(new FakeOcr(), () => Language), NewFolder());

        await extractor.ExtractPagesAsync("book.pdf");

        Assert.Equal(1, renderer.Sessions);
        Assert.Equal([0, 2], renderer.Rendered);
    }

    [Fact]
    public async Task Pdf_page_without_a_text_layer_is_read_by_ocr_from_its_rendered_image()
    {
        var ocr = new FakeOcr(("scanned", new OcrBox(0, 0, 10, 10)));
        var layer = new FakeTextLayer(new PositionedPage(0, null, []));
        var extractor = new RenderedPdfTextExtractor(layer, new FakeRenderer(), new OcrPositionedTextExtractor(ocr, () => Language), NewFolder());

        PositionedPage page = Assert.Single(await extractor.ExtractPagesAsync("scan.pdf"));

        Assert.Equal("scanned", Assert.Single(page.Lines).Text);
        Assert.EndsWith("page-0.jpg", page.ImagePath);
        Assert.Equal(1, ocr.Calls);
    }

    [Fact]
    public async Task Pdf_page_that_cannot_be_rendered_keeps_the_text_layer_result()
    {
        var layer = new FakeTextLayer(new PositionedPage(0, null, [new TextLine("text", default)]));
        var extractor = new RenderedPdfTextExtractor(
            layer, new FakeRenderer(fail: true), new OcrPositionedTextExtractor(new FakeOcr(), () => Language), NewFolder());

        PositionedPage page = Assert.Single(await extractor.ExtractPagesAsync("book.pdf"));

        Assert.Null(page.ImagePath);
        Assert.Equal("text", Assert.Single(page.Lines).Text);
    }

    [Fact]
    public async Task A_page_whose_ocr_fails_keeps_its_result_and_the_other_pages_still_return()
    {
        var ocr = new FakeOcr("page-0.jpg", ("second", new OcrBox(0, 0, 10, 10)));
        var layer = new FakeTextLayer(new PositionedPage(0, null, []), new PositionedPage(1, null, []));
        var extractor = new RenderedPdfTextExtractor(layer, new FakeRenderer(), new OcrPositionedTextExtractor(ocr, () => Language), NewFolder());

        IReadOnlyList<PositionedPage> pages = await extractor.ExtractPagesAsync("scan.pdf");

        Assert.Equal(2, pages.Count);
        Assert.Empty(pages[0].Lines);
        Assert.Equal("second", Assert.Single(pages[1].Lines).Text);
    }

    private static string NewFolder() => Path.Combine(Path.GetTempPath(), "rtst-pages-" + Guid.NewGuid().ToString("N"));

    private sealed class FakeOcr : IOcrLineExtractor
    {
        private readonly OcrLine[] _lines;

        private readonly string? _failOn;

        public FakeOcr(params (string Text, OcrBox Box)[] lines)
            : this(null, lines)
        {
        }

        public FakeOcr(string? failOn, params (string Text, OcrBox Box)[] lines)
        {
            _failOn = failOn;
            _lines = lines.Select(l => new OcrLine(l.Text, l.Box)).ToArray();
        }

        public int Calls { get; private set; }

        public string? LastLanguage { get; private set; }

        public Task<ImageText> ExtractLinesAsync(string imagePath, string language, CancellationToken cancellationToken)
        {
            Calls++;
            LastLanguage = language;
            if (_failOn is not null && imagePath.EndsWith(_failOn, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("ocr failed");
            }

            return Task.FromResult(new ImageText(_lines, OcrSource.MlKit, 100, 100));
        }
    }

    private sealed class FakeTextLayer : IPositionedTextExtractor
    {
        private readonly PositionedPage[] _pages;

        public FakeTextLayer(params PositionedPage[] pages) => _pages = pages;

        public Task<IReadOnlyList<PositionedPage>> ExtractPagesAsync(string path) =>
            Task.FromResult<IReadOnlyList<PositionedPage>>(_pages);
    }

    private sealed class FakeRenderer : IPdfPageRenderer
    {
        private readonly bool _fail;

        public FakeRenderer(bool fail = false) => _fail = fail;

        public int Sessions { get; private set; }

        public List<int> Rendered { get; } = [];

        public Task<IReadOnlyDictionary<int, string>> RenderPagesAsync(string pdfPath, IReadOnlyList<PdfPageTarget> pages)
        {
            Sessions++;
            if (_fail)
            {
                throw new IOException("render failed");
            }

            Rendered.AddRange(pages.Select(p => p.PageIndex));
            return Task.FromResult<IReadOnlyDictionary<int, string>>(pages.ToDictionary(p => p.PageIndex, p => p.OutputPath));
        }
    }
}
