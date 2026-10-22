using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class ImageTextReadingTests
{
    private static readonly string[] Page =
    [
        "THE GREAT STORY",
        "Once upon a time there were two cats.",
        "They lived in a house by the sea.",
    ];

    private static ImageText Boxed(string? fullText = null) =>
        new(
            [
                new OcrLine(Page[0], new OcrBox(0, 0, 100, 40)),
                new OcrLine(Page[1], new OcrBox(0, 90, 100, 110)),
                new OcrLine(Page[2], new OcrBox(0, 115, 100, 135)),
            ],
            OcrSource.MlKit, 200, 200, fullText);

    [Fact]
    public void A_geometry_heading_gets_a_blank_line_and_every_newline_is_kept()
    {
        ImageText page = Boxed();

        string reading = ScanReadingText.From(page);

        Assert.Equal(page.Text.Replace("STORY\n", "STORY\n\n", StringComparison.Ordinal), reading);
        Assert.Equal(page.Text.Count(c => c == '\n') + 1, reading.Count(c => c == '\n'));
    }

    [Fact]
    public void The_reading_text_reads_the_heading_as_its_own_unit_apart_from_the_paragraph()
    {
        IReadOnlyList<ReflowUnit> units = LineReflow.FromPlainText(ScanReadingText.From(Boxed()));

        Assert.Equal(2, units.Count);
        Assert.Equal("THE GREAT STORY", units[0].Text);
        Assert.Equal("Once upon a time there were two cats. They lived in a house by the sea.", units[1].Text);
    }

    [Fact]
    public void Lines_without_boxes_read_as_plain_text()
    {
        var page = new ImageText(Boxed().Lines.Select(l => l with { Box = default }).ToList(), OcrSource.PaddleOcr);

        Assert.Equal(page.Text, ScanReadingText.From(page));
    }

    [Fact]
    public void Engine_text_with_blank_lines_elsewhere_still_gets_the_heading_blank_line_from_the_boxes()
    {
        // The engine text has its own blank line before the heading; the heading's trailing one comes from the boxes.
        string full = "Prologue.\n\nTHE GREAT STORY\nOnce upon a time there were two cats.\nThey lived in a house by the sea.";
        var page = new ImageText(
            [
                new OcrLine("Prologue.", new OcrBox(0, 0, 100, 20)),
                new OcrLine(Page[0], new OcrBox(0, 80, 100, 120)),
                new OcrLine(Page[1], new OcrBox(0, 170, 100, 190)),
                new OcrLine(Page[2], new OcrBox(0, 195, 100, 215)),
            ],
            OcrSource.MlKit, 200, 300, full);

        Assert.Equal(full.Replace("STORY\n", "STORY\n\n", StringComparison.Ordinal), ScanReadingText.From(page));
    }

    [Fact]
    public void A_heading_as_the_last_line_gets_no_trailing_blank_line_and_loses_no_text()
    {
        var page = new ImageText(
            [
                new OcrLine(Page[1], new OcrBox(0, 0, 100, 20)),
                new OcrLine(Page[2], new OcrBox(0, 25, 100, 45)),
                new OcrLine("THE END", new OcrBox(0, 120, 100, 160)),
            ],
            OcrSource.MlKit, 200, 200);

        string reading = ScanReadingText.From(page);

        Assert.Equal(page.Text.Replace("\n", "", StringComparison.Ordinal), reading.Replace("\n", "", StringComparison.Ordinal));
        Assert.False(reading.EndsWith('\n'));
    }

    [Fact]
    public void Consecutive_geometry_headings_are_each_followed_by_a_blank_line()
    {
        var page = new ImageText(
            [
                new OcrLine(Page[0], new OcrBox(0, 0, 100, 40)),
                new OcrLine("A TALE OF TWO CATS", new OcrBox(0, 50, 100, 90)),
                new OcrLine(Page[1], new OcrBox(0, 150, 100, 170)),
                new OcrLine(Page[2], new OcrBox(0, 175, 100, 195)),
            ],
            OcrSource.MlKit, 200, 200);

        Assert.Equal(
            "THE GREAT STORY\n\nA TALE OF TWO CATS\n\nOnce upon a time there were two cats.\nThey lived in a house by the sea.",
            ScanReadingText.From(page));
    }

    [Fact]
    public void Crlf_engine_text_does_not_get_redundant_blank_lines()
    {
        string full = "THE GREAT STORY\r\n\r\nOnce upon a time there were two cats.\r\nThey lived in a house by the sea.";

        Assert.Equal(full, ScanReadingText.From(Boxed(full)));
    }

    [Fact]
    public void A_whitespace_only_neighbour_line_counts_as_blank()
    {
        string full = "THE GREAT STORY\n \nOnce upon a time there were two cats.\nThey lived in a house by the sea.";

        Assert.Equal(full, ScanReadingText.From(Boxed(full)));
    }

    [Fact]
    public void One_line_without_a_box_does_not_turn_geometry_off_for_the_rest_of_the_page()
    {
        string full = "Prologue.\n\nTHE GREAT STORY\nOnce upon a time there were two cats.\nThey lived in a house by the sea.";
        var page = new ImageText(
            [
                new OcrLine("Prologue.", default),
                new OcrLine(Page[0], new OcrBox(0, 80, 100, 120)),
                new OcrLine(Page[1], new OcrBox(0, 170, 100, 190)),
                new OcrLine(Page[2], new OcrBox(0, 195, 100, 215)),
            ],
            OcrSource.MlKit, 200, 300, full);

        Assert.Contains("STORY\n\nOnce", ScanReadingText.From(page), StringComparison.Ordinal);
    }

    [Fact]
    public void A_whitespace_only_recognized_line_is_skipped_when_aligning()
    {
        ImageText boxed = Boxed();
        var page = new ImageText([.. boxed.Lines, new OcrLine("  ", new OcrBox(0, 140, 100, 150))], OcrSource.MlKit, 200, 200, boxed.Text);

        Assert.Equal(ScanReadingText.From(boxed), ScanReadingText.From(page));
        Assert.Contains("STORY\n\nOnce", ScanReadingText.From(page), StringComparison.Ordinal);
    }

    [Fact]
    public void Engine_text_that_does_not_match_the_lines_is_left_unchanged()
    {
        const string full = "THE GREAT STORY Once upon a time there were two cats.\nThey lived in a house by the sea.";

        Assert.Equal(full, ScanReadingText.From(Boxed(full)));
    }
}
