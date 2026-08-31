using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.ExtendedProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Infrastructure.Documents;
using DocxProperties = DocumentFormat.OpenXml.ExtendedProperties.Properties;

namespace ReadTheStupidText.Tests;

public class DocxTextExtractorTests
{
    [Theory]
    [InlineData(".docx", true)]
    [InlineData(".DOCX", true)]
    [InlineData(".txt", false)]
    [InlineData(".pdf", false)]
    public void CanHandle_matches_only_docx(string extension, bool expected)
    {
        Assert.Equal(expected, new DocxTextExtractor().CanHandle(extension));
    }

    [Fact]
    public async Task ExtractTextAsync_joins_paragraph_text_in_order()
    {
        string path = BuildFixtureDocx(pageCount: null, "First paragraph", "Second paragraph");
        try
        {
            string text = await new DocxTextExtractor().ExtractTextAsync(path);
            int firstIndex = text.IndexOf("First paragraph", StringComparison.Ordinal);
            int secondIndex = text.IndexOf("Second paragraph", StringComparison.Ordinal);
            Assert.True(firstIndex >= 0 && secondIndex > firstIndex);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExtractTextAsync_throws_DocumentTooLargeException_above_the_cached_page_cap()
    {
        string path = BuildFixtureDocx(pageCount: 201, "Some text");
        try
        {
            DocumentTooLargeException ex = await Assert.ThrowsAsync<DocumentTooLargeException>(
                () => new DocxTextExtractor().ExtractTextAsync(path));
            Assert.Equal(201, ex.Actual);
            Assert.Equal(200, ex.Limit);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExtractTextAsync_skips_the_cap_when_no_cached_page_count_is_present()
    {
        string path = BuildFixtureDocx(pageCount: null, "Some text");
        try
        {
            string text = await new DocxTextExtractor().ExtractTextAsync(path);
            Assert.Contains("Some text", text, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string BuildFixtureDocx(int? pageCount, params string[] paragraphsText)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
        using (WordprocessingDocument document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(
                paragraphsText.Select(text => new Paragraph(new Run(new Text(text))))));
            mainPart.Document.Save();

            if (pageCount is { } pages)
            {
                ExtendedFilePropertiesPart extendedProperties = document.AddExtendedFilePropertiesPart();
                extendedProperties.Properties = new DocxProperties(new Pages(pages.ToString()));
                extendedProperties.Properties.Save();
            }
        }

        return path;
    }
}
