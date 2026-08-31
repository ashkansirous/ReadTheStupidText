using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Infrastructure.Documents;

namespace ReadTheStupidText.Tests;

public class DocumentTextExtractorTests
{
    [Theory]
    [InlineData(".txt", true)]
    [InlineData(".TXT", true)]
    [InlineData(".pdf", false)]
    [InlineData(".docx", false)]
    public void PlainTextExtractor_handles_only_txt(string extension, bool expected)
    {
        Assert.Equal(expected, new PlainTextExtractor().CanHandle(extension));
    }

    [Fact]
    public async Task PlainTextExtractor_reads_file_contents_verbatim()
    {
        string path = await WriteTempFileAsync(".txt", "hello world");
        try
        {
            Assert.Equal("hello world", await new PlainTextExtractor().ExtractTextAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Composite_routes_a_txt_file_to_the_plain_text_extractor()
    {
        string path = await WriteTempFileAsync(".txt", "routed text");
        try
        {
            var composite = new CompositeDocumentTextExtractor(new PlainTextExtractor(), new PdfTextExtractor());
            Assert.Equal("routed text", await composite.ExtractTextAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Composite_CanHandle_reflects_its_registered_extractors()
    {
        var composite = new CompositeDocumentTextExtractor(new PlainTextExtractor(), new PdfTextExtractor());
        Assert.True(composite.CanHandle(".txt"));
        Assert.True(composite.CanHandle(".pdf"));
        Assert.False(composite.CanHandle(".docx"));
    }

    [Fact]
    public async Task Composite_throws_for_an_unregistered_extension()
    {
        var composite = new CompositeDocumentTextExtractor(new PlainTextExtractor(), new PdfTextExtractor());
        await Assert.ThrowsAsync<NotSupportedException>(() => composite.ExtractTextAsync("report.docx"));
    }

    private static async Task<string> WriteTempFileAsync(string extension, string contents)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        await File.WriteAllTextAsync(path, contents);
        return path;
    }
}
