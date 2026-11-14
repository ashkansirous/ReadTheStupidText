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
    public void A_heading_line_is_a_narration_segment_of_its_own()
    {
        const string text = "Where to Find Ebooks\nYou can buy ebooks from large stores such as Amazon and Kobo.";

        var segments = Segmenter.Split(text);

        Assert.Equal(2, segments.Count);
        Assert.Equal("Where to Find Ebooks", segments[0].Text);
        Assert.All(segments, s => Assert.Equal(SegmentKind.Narration, s.Kind));
    }

    [Fact]
    public void A_soft_wrapped_line_is_not_a_sentence_end()
    {
        const string text = "Now they have a good view, but\nCarrot wants to get even closer.";

        var sentences = StorySentences.Build(text, LanguageRules.English);

        Assert.Single(sentences);
        Assert.Equal(text.Length, sentences[0].End);
    }

    [Fact]
    public void A_list_item_and_a_blank_line_still_end_a_sentence()
    {
        const string text = "First thing.\n- Second thing\n\nThird thing";

        var sentences = StorySentences.Build(text, LanguageRules.English);

        Assert.Equal(3, sentences.Count);
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

    // The Bright Stadium page (Slice 79): a sign over a four-line story block, the sign classed as background.
    private static PageReading BrightStadium()
    {
        var lines = new List<PageLine>
        {
            new("BRIGHT STADIUM", new NormalizedRect(0.2, 0.05, 0.6, 0.1)),
            new("Carrot and Hazelnut sat on the hill", new NormalizedRect(0.1, 0.7, 0.8, 0.03)),
            new("and watched the big match begin.", new NormalizedRect(0.1, 0.74, 0.8, 0.03)),
            new("Now they have a good view, but", new NormalizedRect(0.1, 0.78, 0.8, 0.03)),
            new("Carrot wants to get even closer.", new NormalizedRect(0.1, 0.82, 0.8, 0.03)),
        };
        var regions = new List<PageRegion>
        {
            new(lines[0].Box, [0], Background: true),
            new(new NormalizedRect(0.1, 0.7, 0.8, 0.15), [1, 2, 3, 4], Background: false),
        };
        return new PageReading(lines, regions);
    }

    private static StoryPage PageOf(PageReading reading, string image = "page.jpg") =>
        new(reading.Text, image, reading.Lines, reading.Regions);

    [Fact]
    public void Background_region_is_not_in_the_story_text_or_its_segments()
    {
        PageReading reading = BrightStadium();

        Assert.StartsWith("Carrot and Hazelnut", reading.Text);
        Assert.DoesNotContain("BRIGHT", reading.Text);
        Assert.DoesNotContain(Segmenter.Split(reading.Text), s => s.Text.Contains("BRIGHT STADIUM"));
    }

    [Fact]
    public void Toggling_the_sign_puts_it_back_and_toggling_again_removes_it()
    {
        PageDraft draft = PageDraft.ForPicture("a.jpg", BrightStadium());

        PageDraft included = draft.WithRegionToggled(0);
        PageDraft left = included.WithRegionToggled(0);

        Assert.StartsWith("BRIGHT STADIUM", included.Text);
        Assert.DoesNotContain("BRIGHT", left.Text);
    }

    [Fact]
    public void Toggling_replaces_the_typed_text_with_the_recomposed_one()
    {
        PageDraft typed = PageDraft.ForPicture("a.jpg", BrightStadium()).WithText("My own words");

        Assert.StartsWith("BRIGHT STADIUM", typed.WithRegionToggled(0).Text);
    }

    [Fact]
    public void Story_page_with_regions_survives_the_json_round_trip()
    {
        PageReading reading = BrightStadium().WithRegionToggled(0);
        var story = new Story("0123456789abcdef0123456789abcdef", "T", "en", DateTimeOffset.UnixEpoch, [PageOf(reading)], [], CoverStyle.Colour, 0, "narrator");

        Story back = StoryJson.Deserialize(StoryJson.Serialize(story));

        PageRegion sign = back.Pages[0].Regions![0];
        Assert.True(sign.Background);
        Assert.True(sign.Included);
        Assert.Equal([1, 2, 3, 4], back.Pages[0].Regions![1].LineIndexes);
        Assert.Null(back.Pages[0].Regions![1].Included);
    }

    [Fact]
    public void Older_story_json_without_regions_includes_every_line()
    {
        PageReading reading = new(BrightStadium().Lines);
        var story = new Story("0123456789abcdef0123456789abcdef", "T", "en", DateTimeOffset.UnixEpoch, [PageOf(reading)], [], CoverStyle.Colour, 0, "narrator");

        Story back = StoryJson.Deserialize(StoryJson.Serialize(story));

        Assert.Null(back.Pages[0].Regions);
        Assert.Equal(5, back.Pages[0].IncludedLines.Count);
    }

    [Fact]
    public void A_region_pointing_past_the_lines_is_dropped_on_load()
    {
        PageReading reading = BrightStadium();
        var bad = new PageRegion(new NormalizedRect(0, 0, 0.5, 0.5), [9], Background: true);
        var story = new Story("0123456789abcdef0123456789abcdef", "T", "en", DateTimeOffset.UnixEpoch,
            [new StoryPage(reading.Text, "page.jpg", reading.Lines, [bad])], [], CoverStyle.Colour, 0, "narrator");

        Assert.Null(StoryJson.Deserialize(StoryJson.Serialize(story)).Pages[0].Regions);
    }

    [Fact]
    public void Break_map_uses_the_included_lines_boxes()
    {
        Story story = new("0123456789abcdef0123456789abcdef", "T", "en", DateTimeOffset.UnixEpoch, [PageOf(BrightStadium())], [], CoverStyle.Colour, 0, "narrator");

        // 4 story lines joined by 3 newlines: all soft wraps but the last line ends a sentence; nothing is a heading.
        Assert.Empty(story.BreakMap().HeadingBreaks);
    }

    [Fact]
    public void Break_map_follows_the_included_lines_boxes_not_the_plain_text_rule()
    {
        PageReading reading = BrightStadium();
        var heading = new PageLine("The hill at dawn", new NormalizedRect(0.1, 0.5, 0.4, 0.06));
        var lines = reading.Lines.Prepend(heading).ToList();
        var regions = new List<PageRegion>
        {
            new(lines[1].Box, [1], Background: true),
            new(new NormalizedRect(0.1, 0.5, 0.8, 0.35), [0, 2, 3, 4, 5], Background: false),
        };
        var page = new StoryPage(new PageReading(lines, regions).Text, "page.jpg", lines, regions);
        var plain = page with { Lines = null, Regions = null };

        var boxed = Story.BreakMapOf([page]);

        Assert.NotEmpty(boxed.HeadingBreaks);
        Assert.Empty(Story.BreakMapOf([plain]).HeadingBreaks);
    }

    [Fact]
    public void Break_map_keeps_the_geometry_when_only_whitespace_differs()
    {
        PageReading reading = BrightStadium();
        var heading = new PageLine("The hill at dawn", new NormalizedRect(0.1, 0.5, 0.4, 0.06));
        var lines = reading.Lines.Prepend(heading).ToList();
        var regions = new List<PageRegion>
        {
            new(lines[1].Box, [1], Background: true),
            new(new NormalizedRect(0.1, 0.5, 0.8, 0.35), [0, 2, 3, 4, 5], Background: false),
        };
        string text = new PageReading(lines, regions).Text;
        StoryPage Page(string t) => new(t, "page.jpg", lines, regions);
        string spaced = string.Join("\n", text.Split('\n').Select(l => "  " + l.Replace(" ", "  ") + " "));

        Assert.NotEmpty(Story.BreakMapOf([Page(spaced)]).HeadingBreaks);
        Assert.Empty(Story.BreakMapOf([Page(text.Replace("hill", "mountain"))]).HeadingBreaks);
    }

    [Fact]
    public void A_toggle_that_changes_nothing_keeps_the_typed_text()
    {
        PageReading reading = BrightStadium();
        var empty = new PageRegion(new NormalizedRect(0, 0, 0.1, 0.1), [], Background: true);
        PageDraft draft = PageDraft.ForPicture("a.jpg", reading with { Regions = [.. reading.Regions!, empty] }).WithText("My own words");

        Assert.Equal("My own words", draft.WithRegionToggled(reading.Regions!.Count).Text);
        Assert.Equal("My own words", draft.WithRegionToggled(99).Text);
    }

    [Fact]
    public void Typed_text_is_told_from_the_readings_own()
    {
        PageDraft draft = PageDraft.ForPicture("a.jpg", BrightStadium());

        Assert.False(draft.HasEditedText);
        Assert.False(draft.WouldReplace(draft.Text));
        Assert.True(draft.WouldReplace("My own words"));
        Assert.True(draft.WithText("My own words").HasEditedText);
        Assert.False(draft.WithText(draft.Text).HasEditedText);
    }

    [Fact]
    public void Scan_layout_matches_only_the_included_lines_and_boxes_the_right_ones()
    {
        // "STOP" is left out of the page text; the same word comes again on page 2 and must not pull the cursor there.
        var lines = new List<PageLine>
        {
            new("STOP", new NormalizedRect(0.1, 0.05, 0.3, 0.05)),
            new("The fox ran home.", new NormalizedRect(0.1, 0.6, 0.8, 0.05)),
        };
        var regions = new List<PageRegion> { new(lines[0].Box, [0], Background: true), new(lines[1].Box, [1], Background: false) };
        var first = new StoryPage("The fox ran home.", "p0.jpg", lines, regions);
        var second = new StoryPage("STOP", "p1.jpg", [new("STOP", new NormalizedRect(0.2, 0.2, 0.3, 0.1))]);
        var layout = new ScanLayout([first, second], [new SentenceSpan(0, 17), new SentenceSpan(19, 23)]);

        Assert.Equal([new PageBox(0, lines[1].Box)], layout.BoxesOf(0));
        Assert.Equal([new PageBox(1, new NormalizedRect(0.2, 0.2, 0.3, 0.1))], layout.BoxesOf(1));
    }

    [Fact]
    public void Dropping_a_damaged_line_keeps_the_regions_on_the_lines_they_named()
    {
        PageReading reading = BrightStadium();
        var damaged = new List<PageLine>(reading.Lines);
        damaged[0] = new PageLine("BRIGHT STADIUM", new NormalizedRect(0.2, 0.05, 0.6, 5)); // a box far off the page
        var story = new Story("0123456789abcdef0123456789abcdef", "T", "en", DateTimeOffset.UnixEpoch,
            [new StoryPage(reading.Text, "page.jpg", damaged, reading.Regions)], [], CoverStyle.Colour, 0, "narrator");

        StoryPage page = StoryJson.Deserialize(StoryJson.Serialize(story)).Pages[0];

        Assert.Equal(4, page.Lines!.Count);
        Assert.Equal([0, 1, 2, 3], page.Regions!.Single().LineIndexes);
        Assert.Equal(reading.Text, ImportedText.PageText(page.IncludedLines.Select(l => l.Text)));
    }
}
