using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Documents;

namespace ReadTheStupidText.Tests;

public class PositionedTextTests
{
    private const double Tolerance = 0.0001;

    [Fact]
    public void FromBox_divides_by_the_page_size()
    {
        NormalizedRect rect = NormalizedRect.FromBox(100, 50, 200, 25, 400, 200);

        Assert.Equal(0.25, rect.Left, Tolerance);
        Assert.Equal(0.25, rect.Top, Tolerance);
        Assert.Equal(0.5, rect.Width, Tolerance);
        Assert.Equal(0.125, rect.Height, Tolerance);
    }

    [Fact]
    public void FromBox_clamps_a_box_that_overhangs_the_page()
    {
        NormalizedRect rect = NormalizedRect.FromBox(-10, 90, 50, 50, 100, 100);

        Assert.Equal(0, rect.Left, Tolerance);
        Assert.Equal(0.4, rect.Width, Tolerance);
        Assert.Equal(0.9, rect.Top, Tolerance);
        Assert.Equal(0.1, rect.Height, Tolerance);
    }

    [Fact]
    public void FromBox_of_an_empty_page_is_empty()
    {
        Assert.Equal(default, NormalizedRect.FromBox(1, 1, 1, 1, 0, 0));
    }

    [Fact]
    public void Union_covers_both_rects()
    {
        var union = new NormalizedRect(0.1, 0.1, 0.2, 0.1).Union(new NormalizedRect(0.5, 0.3, 0.2, 0.1));

        Assert.Equal(new NormalizedRect(0.1, 0.1, 0.6, 0.3).Left, union.Left, Tolerance);
        Assert.Equal(0.6, union.Width, Tolerance);
        Assert.Equal(0.3, union.Height, Tolerance);
    }

    [Fact]
    public void Words_on_one_baseline_form_one_line_left_to_right()
    {
        PositionedWord[] words =
        [
            new("world", 60, 10, 40, 10),
            new("Hello", 10, 11, 40, 10),
        ];

        IReadOnlyList<TextLine> lines = WordLineGrouper.Group(words, 100, 100);

        TextLine line = Assert.Single(lines);
        Assert.Equal("Hello world", line.Text);
        Assert.Equal(0.1, line.Box.Left, Tolerance);
        Assert.Equal(1.0, line.Box.Left + line.Box.Width, Tolerance);
    }

    [Fact]
    public void Lines_come_out_top_to_bottom()
    {
        PositionedWord[] words = [new("second", 10, 50, 40, 10), new("first", 10, 10, 40, 10)];

        IReadOnlyList<TextLine> lines = WordLineGrouper.Group(words, 100, 100);

        Assert.Equal(["first", "second"], lines.Select(l => l.Text));
    }

    [Fact]
    public void An_arabic_line_reads_right_to_left()
    {
        PositionedWord[] words =
        [
            new("مرحبا", 70, 10, 20, 10),
            new("بكم", 10, 10, 20, 10),
        ];

        IReadOnlyList<TextLine> lines = WordLineGrouper.Group(words, 100, 100);

        Assert.Equal("مرحبا بكم", Assert.Single(lines).Text);
    }

    [Fact]
    public void Blank_words_are_ignored()
    {
        Assert.Empty(WordLineGrouper.Group([new PositionedWord(" ", 0, 0, 5, 5)], 100, 100));
    }

    [Fact]
    public async Task Pdf_text_layer_yields_a_line_with_a_box_in_the_right_place()
    {
        string path = PdfTextExtractorTests.BuildFixturePdf("Hello there");
        try
        {
            IReadOnlyList<PositionedPage> pages = await new PdfPositionedTextExtractor().ExtractPagesAsync(path);

            PositionedPage page = Assert.Single(pages);
            Assert.Equal(0, page.Index);
            TextLine line = Assert.Single(page.Lines);
            Assert.Equal("Hello there", line.Text);
            Assert.Equal(50d / 300, line.Box.Left, 0.01);
            Assert.InRange(line.Box.Top, 0.2, 0.3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("100 100 400 400", null)]
    [InlineData("0 0 400 400", "100 100 400 400")]
    public async Task Pdf_box_is_relative_to_a_visible_box_with_a_non_zero_origin(string mediaBox, string? cropBox)
    {
        // The text sits 50 in and 100 pt below the top of a 300 x 300 visible box whose corner is (100, 100).
        // PdfPig reports word coordinates relative to that corner already; the extractor must not subtract it again.
        string path = PdfTextExtractorTests.BuildFixturePdfWithBox(mediaBox, cropBox, "Hello there");
        try
        {
            IReadOnlyList<PositionedPage> pages = await new PdfPositionedTextExtractor().ExtractPagesAsync(path);

            TextLine line = Assert.Single(Assert.Single(pages).Lines);
            Assert.Equal(50d / 300, line.Box.Left, 0.01);
            Assert.InRange(line.Box.Top, 0.2, 0.3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_tall_drop_cap_does_not_swallow_the_lines_it_spans()
    {
        PositionedWord[] words =
        [
            new("T", 0, 0, 20, 30),
            new("he", 25, 0, 20, 10),
            new("cat", 25, 10, 20, 10),
            new("sat", 25, 20, 20, 10),
            new("down", 50, 20, 20, 10),
        ];

        IReadOnlyList<TextLine> lines = WordLineGrouper.Group(words, 100, 100);

        Assert.Equal(3, lines.Count);
        Assert.Equal("sat down", lines[2].Text);
    }

    [Fact]
    public void A_superscript_stays_on_its_line()
    {
        PositionedWord[] words =
        [
            new("E", 10, 10, 8, 10),
            new("equals", 20, 10, 30, 10),
            new("mc", 52, 10, 15, 10),
            new("2", 68, 7, 4, 7),
            new("Next", 10, 30, 20, 10),
        ];

        IReadOnlyList<TextLine> lines = WordLineGrouper.Group(words, 100, 100);

        Assert.Equal(["E equals mc 2", "Next"], lines.Select(l => l.Text));
    }

    [Fact]
    public void A_word_with_a_descender_stays_on_its_line()
    {
        PositionedWord[] words =
        [
            new("The", 10, 10, 20, 10),
            new("big", 32, 10, 20, 10),
            new("jumpy", 54, 15, 30, 13),
            new("Next", 10, 30, 20, 10),
        ];

        IReadOnlyList<TextLine> lines = WordLineGrouper.Group(words, 100, 100);

        Assert.Equal(["The big jumpy", "Next"], lines.Select(l => l.Text));
    }
}
