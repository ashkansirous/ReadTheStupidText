using DinoReader.Core;

namespace DinoReader.Tests;

public class RangeToBoxMapperTests
{
    private const string Story = "The cat sat on\nthe warm mat. Then it\nslept all day.\nNext page begins.";

    private static readonly IReadOnlyList<IReadOnlyList<string>> Pages =
    [
        ["The cat sat on", "the warm mat. Then it", "slept all day."],
        ["Next page begins."],
    ];

    private static (int Start, int End) Range(string from, string to)
    {
        int start = Story.IndexOf(from, StringComparison.Ordinal);
        return (start, Story.IndexOf(to, StringComparison.Ordinal) + to.Length);
    }

    [Fact]
    public void A_sentence_over_three_lines_returns_three_lines_in_order()
    {
        (int start, int end) = Range("sat on", "all day.");

        IReadOnlyList<LineRef> hits = RangeToBoxMapper.Map(Story, Pages, start, end);

        Assert.Equal([new LineRef(0, 0), new LineRef(0, 1), new LineRef(0, 2)], hits);
    }

    [Fact]
    public void A_partial_line_is_boxed_whole()
    {
        (int start, int end) = Range("warm", "mat.");

        Assert.Equal([new LineRef(0, 1)], RangeToBoxMapper.Map(Story, Pages, start, end));
    }

    [Fact]
    public void A_sentence_over_two_pages_returns_lines_from_both()
    {
        (int start, int end) = Range("day.", "Next page");

        Assert.Equal([new LineRef(0, 2), new LineRef(1, 0)], RangeToBoxMapper.Map(Story, Pages, start, end));
    }

    [Fact]
    public void A_range_touching_no_line_returns_nothing()
    {
        Assert.Empty(RangeToBoxMapper.Map(Story, Pages, 5000, 5010));
    }

    [Fact]
    public void A_line_missing_from_the_story_is_skipped_without_losing_the_rest()
    {
        IReadOnlyList<IReadOnlyList<string>> pages = [["The cat sat on", "not in story", "the warm mat. Then it"]];

        IReadOnlyList<LineRef> hits = RangeToBoxMapper.Map(Story, pages, 0, Story.Length);

        Assert.Equal([new LineRef(0, 0), new LineRef(0, 2)], hits);
    }

    [Fact]
    public void A_dropped_page_number_line_does_not_derail_the_lines_after_it()
    {
        const string story = "Page one text\nmore words here\nThen 12 apples fell";
        IReadOnlyList<IReadOnlyList<string>> pages = [["Page one text", "12", "more words here", "Then 12 apples fell"]];

        IReadOnlyList<LineRef> hits = RangeToBoxMapper.Map(story, pages, 0, story.Length);

        Assert.Equal([new LineRef(0, 0), new LineRef(0, 2), new LineRef(0, 3)], hits);
    }

    [Fact]
    public void A_hyphenated_line_matches_a_story_that_joined_the_word()
    {
        const string story = "an extraordinary day";
        IReadOnlyList<IReadOnlyList<string>> pages = [["an extra-", "ordinary day"]];

        Assert.Equal([new LineRef(0, 0), new LineRef(0, 1)], RangeToBoxMapper.Map(story, pages, 0, story.Length));
    }

    [Fact]
    public void An_ocr_misread_line_does_not_stop_the_lines_after_it()
    {
        IReadOnlyList<IReadOnlyList<string>> pages = [["The cat sat on", "the warm rnat. Then it", "slept all day."], ["Next page begins."]];

        IReadOnlyList<LineRef> hits = RangeToBoxMapper.Map(Story, pages, 0, Story.Length);

        Assert.Equal([new LineRef(0, 0), new LineRef(0, 2), new LineRef(1, 0)], hits);
    }

    [Fact]
    public void An_extra_heading_in_the_story_resyncs_the_cursor()
    {
        const string story = "The cat sat on\nCHAPTER TWO: THE MAT\nthe warm mat. Then it\nslept all day.";
        IReadOnlyList<IReadOnlyList<string>> pages = [["The cat sat on", "the warm mat. Then it", "slept all day."]];

        IReadOnlyList<LineRef> hits = RangeToBoxMapper.Map(story, pages, 0, story.Length);

        Assert.Equal([new LineRef(0, 0), new LineRef(0, 1), new LineRef(0, 2)], hits);
    }
}
