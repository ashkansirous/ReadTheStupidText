using DinoReader.Core;

namespace DinoReader.Tests;

public class SegmentTextLocatorTests
{
    private const string Story = "Peter ran. “Come here!” Then he stopped.";

    [Fact]
    public void A_quote_text_starts_after_its_opening_mark()
    {
        int open = Story.IndexOf('“');
        int close = Story.IndexOf('”') + 1;
        var segment = new Segment(SegmentKind.Quote, "Come here!", open, close);

        Assert.Equal(open + 1, SegmentTextLocator.TextStart(Story, segment));
    }

    [Fact]
    public void A_quote_with_extra_leading_characters_is_found_where_it_really_starts()
    {
        const string story = "x  \"\"Hi there\"";
        var segment = new Segment(SegmentKind.Quote, "Hi there", 1, story.Length);

        Assert.Equal(story.IndexOf("Hi there", StringComparison.Ordinal), SegmentTextLocator.TextStart(story, segment));
    }

    [Fact]
    public void Narration_starts_at_its_range()
    {
        var segment = new Segment(SegmentKind.Narration, "Peter ran.", 0, 10);

        Assert.Equal(0, SegmentTextLocator.TextStart(Story, segment));
    }

    [Fact]
    public void Text_not_found_in_the_range_falls_back_to_the_range_start()
    {
        var segment = new Segment(SegmentKind.Quote, "missing", 11, 24);

        Assert.Equal(11, SegmentTextLocator.TextStart(Story, segment));
    }

    [Fact]
    public void An_out_of_range_segment_falls_back_without_throwing()
    {
        var segment = new Segment(SegmentKind.Quote, "x", 5, 999);

        Assert.Equal(5, SegmentTextLocator.TextStart(Story, segment));
    }
}
