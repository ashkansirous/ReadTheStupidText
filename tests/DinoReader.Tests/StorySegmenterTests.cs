using DinoReader.Core;

namespace DinoReader.Tests;

public class StorySegmenterTests
{
    private static readonly StorySegmenter Segmenter = new(LanguageRules.English);

    [Fact]
    public void Splits_narration_and_quotes_with_source_ranges()
    {
        const string text = "Peter said, \"Hello there.\" Then he left.";
        var segments = Segmenter.Split(text);

        Assert.Equal(3, segments.Count);
        Assert.Equal(SegmentKind.Narration, segments[0].Kind);
        Assert.Equal("Peter said,", segments[0].Text);
        Assert.Equal(SegmentKind.Quote, segments[1].Kind);
        Assert.Equal("Hello there.", segments[1].Text);
        Assert.Equal("\"Hello there.\"", text[segments[1].SourceStart..segments[1].SourceEnd]);
        Assert.Equal("Then he left.", segments[2].Text);
    }

    [Fact]
    public void Handles_curly_quotes()
    {
        var segments = Segmenter.Split("“Hi,” said Tom.");

        Assert.Equal(SegmentKind.Quote, segments[0].Kind);
        Assert.Equal("Hi,", segments[0].Text);
        Assert.Equal("said Tom.", segments[1].Text);
    }

    [Fact]
    public void Nested_single_quotes_stay_inside_the_quote()
    {
        var segments = Segmenter.Split("\"She said 'go away' to me,\" he said.");

        Assert.Equal("She said 'go away' to me,", segments[0].Text);
        Assert.Equal(SegmentKind.Narration, segments[1].Kind);
    }

    [Fact]
    public void Unclosed_quote_runs_to_the_end()
    {
        const string text = "He said, \"Wait for me";
        var segments = Segmenter.Split(text);

        Assert.Equal(SegmentKind.Quote, segments[1].Kind);
        Assert.Equal("Wait for me", segments[1].Text);
        Assert.Equal(text.Length, segments[1].SourceEnd);
    }

    [Fact]
    public void Quote_continuing_over_a_paragraph_is_split_at_the_break()
    {
        const string text = "\"First paragraph.\n\n\"Second paragraph.\"";
        var quotes = Segmenter.Split(text).Where(s => s.Kind == SegmentKind.Quote).ToList();

        Assert.Equal(2, quotes.Count);
        Assert.Equal("First paragraph.", quotes[0].Text);
        Assert.Equal("Second paragraph.", quotes[1].Text);
    }

    [Fact]
    public void Quote_without_reopening_ends_at_the_paragraph_break()
    {
        var segments = Segmenter.Split("\"One.\n\nTwo.\" said Mum.");

        Assert.Equal("One.", segments[0].Text);
        Assert.False(segments[0].IsClosed);
    }

    [Fact]
    public void Text_without_quotes_is_one_narration_segment()
    {
        var segments = Segmenter.Split("Just a plain sentence.");

        Assert.Single(segments);
        Assert.Equal(SegmentKind.Narration, segments[0].Kind);
    }

    [Fact]
    public void Empty_text_gives_no_segments() => Assert.Empty(Segmenter.Split(""));

    [Fact]
    public void Open_quote_ends_at_a_paragraph_break_even_when_the_next_paragraph_is_narration()
    {
        var segments = new StorySegmenter(LanguageRules.English)
            .Split("Peter said, \"Where are you going\n\nLucy ran off. \"Home,\" said Lucy.");

        var quote = segments.First(s => s.Kind == SegmentKind.Quote);
        Assert.Equal("Where are you going", quote.Text);
        Assert.False(quote.IsClosed);
        Assert.Equal("Lucy ran off.", segments[2].Text);
    }
}
