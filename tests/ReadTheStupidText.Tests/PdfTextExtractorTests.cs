using System.Text;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Infrastructure.Documents;

namespace ReadTheStupidText.Tests;

public class PdfTextExtractorTests
{
    [Theory]
    [InlineData(".pdf", true)]
    [InlineData(".PDF", true)]
    [InlineData(".txt", false)]
    [InlineData(".docx", false)]
    public void CanHandle_matches_only_pdf(string extension, bool expected)
    {
        Assert.Equal(expected, new PdfTextExtractor().CanHandle(extension));
    }

    [Fact]
    public async Task ExtractTextAsync_joins_page_text_in_order()
    {
        string path = BuildFixturePdf("Page one text", "Page two text");
        try
        {
            string text = await new PdfTextExtractor().ExtractTextAsync(path);
            int firstIndex = text.IndexOf("Page one text", StringComparison.Ordinal);
            int secondIndex = text.IndexOf("Page two text", StringComparison.Ordinal);
            Assert.True(firstIndex >= 0 && secondIndex > firstIndex);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExtractTextAsync_throws_DocumentTooLargeException_above_the_page_cap()
    {
        string[] pages = Enumerable.Range(0, 201).Select(i => $"Page {i}").ToArray();
        string path = BuildFixturePdf(pages);
        try
        {
            DocumentTooLargeException ex = await Assert.ThrowsAsync<DocumentTooLargeException>(
                () => new PdfTextExtractor().ExtractTextAsync(path));
            Assert.Equal(201, ex.Actual);
            Assert.Equal(200, ex.Limit);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Hand-builds a minimal, valid single/multi-page PDF (one Helvetica Tj per
    // page, a correct byte-offset xref table) — PdfPig is read-only, so there is
    // no library to author a fixture with instead.
    private static string BuildFixturePdf(params string[] pagesText)
    {
        const int FontObj = 3;
        const int FirstPageObj = 4;
        var objects = new SortedDictionary<int, byte[]>
        {
            [1] = Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            [FontObj] = Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"),
        };

        string kids = string.Join(" ", Enumerable.Range(0, pagesText.Length).Select(i => $"{FirstPageObj + i * 2} 0 R"));
        objects[2] = Encoding.ASCII.GetBytes($"<< /Type /Pages /Kids [{kids}] /Count {pagesText.Length} >>");

        for (int i = 0; i < pagesText.Length; i++)
        {
            int pageObj = FirstPageObj + i * 2;
            int contentObj = pageObj + 1;
            objects[pageObj] = Encoding.ASCII.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 {FontObj} 0 R >> >> /Contents {contentObj} 0 R >>");

            string escaped = pagesText[i].Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            byte[] stream = Encoding.ASCII.GetBytes($"BT /F1 24 Tf 50 200 Td ({escaped}) Tj ET");
            objects[contentObj] = Encoding.ASCII.GetBytes($"<< /Length {stream.Length} >>\nstream\n")
                .Concat(stream)
                .Concat(Encoding.ASCII.GetBytes("\nendstream"))
                .ToArray();
        }

        using var ms = new MemoryStream();
        void WriteAscii(string s) => ms.Write(Encoding.ASCII.GetBytes(s));

        WriteAscii("%PDF-1.4\n");
        var offsets = new Dictionary<int, long>();
        foreach ((int number, byte[] body) in objects)
        {
            offsets[number] = ms.Position;
            WriteAscii($"{number} 0 obj\n");
            ms.Write(body);
            WriteAscii("\nendobj\n");
        }

        int maxObj = objects.Keys.Max();
        long xrefStart = ms.Position;
        WriteAscii($"xref\n0 {maxObj + 1}\n0000000000 65535 f \n");
        for (int n = 1; n <= maxObj; n++)
        {
            WriteAscii(offsets.TryGetValue(n, out long off)
                ? $"{off:D10} 00000 n \n"
                : "0000000000 00000 f \n");
        }
        WriteAscii($"trailer\n<< /Size {maxObj + 1} /Root 1 0 R >>\nstartxref\n{xrefStart}\n%%EOF");

        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
        File.WriteAllBytes(path, ms.ToArray());
        return path;
    }
}
