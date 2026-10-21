using DinoReader.Core;

namespace DinoReader.Tests;

public class ScanLayoutTests
{
    private static NormalizedRect Box(double y) => new(0.1, y, 0.8, 0.1);

    // Page 0: a scan of three lines, the last sentence runs over two lines. Page 1: typed, no picture.
    // Page 2: a scan whose sentence wraps onto the next scan page 3.
    private static readonly IReadOnlyList<StoryPage> Pages =
    [
        new("Peter hopped up.\nCan I come too? asked Peter.\nOnly if you are quick,\nsaid Cotton Tail.", "p0.jpg",
            [new("Peter hopped up.", Box(0.1)), new("Can I come too? asked Peter.", Box(0.3)),
             new("Only if you are quick,", Box(0.5)), new("said Cotton Tail.", Box(0.6))]),
        new("A typed page."),
        new("The rabbit ran and ran", "p2.jpg", [new("The rabbit ran and ran", Box(0.8))]),
        new("until night fell.", "p3.jpg", [new("until night fell.", Box(0.1))]),
    ];

    private static readonly string Text = Story.JoinPages(Pages);

    private static IReadOnlyList<SentenceSpan> Sentences()
    {
        var spans = new List<SentenceSpan>();
        foreach (string s in new[] { "Peter hopped up.", "Can I come too?", "asked Peter.", "Only if you are quick,\nsaid Cotton Tail.",
                     "A typed page.", "The rabbit ran and ran\n\nuntil night fell." })
        {
            int at = Text.IndexOf(s, StringComparison.Ordinal);
            spans.Add(new SentenceSpan(at, at + s.Length));
        }

        return spans;
    }

    private static readonly ScanLayout Layout = new(Pages, Sentences());

    [Fact]
    public void Pages_with_a_picture_and_found_lines_are_scanned_and_typed_pages_are_not()
    {
        Assert.True(Layout.IsScanned(0));
        Assert.False(Layout.IsScanned(1));
        Assert.True(Layout.IsScanned(3));
        Assert.True(Layout.HasScannedPages);
    }

    [Fact]
    public void A_picture_whose_lines_are_not_in_the_text_is_not_scanned()
    {
        StoryPage[] pages = [new("Hello there.", "a.jpg", [new("Something else entirely", Box(0.1))])];

        var layout = new ScanLayout(pages, [new SentenceSpan(0, 12)]);

        Assert.False(layout.IsScanned(0));
        Assert.False(layout.HasScannedPages);
    }

    [Fact]
    public void A_sentence_over_two_lines_has_two_boxes_in_order()
    {
        IReadOnlyList<PageBox> boxes = Layout.BoxesOf(3);

        Assert.Equal([new PageBox(0, Box(0.5)), new PageBox(0, Box(0.6))], boxes);
    }

    [Fact]
    public void A_line_holding_two_sentences_is_boxed_for_each()
    {
        Assert.Equal([new PageBox(0, Box(0.3))], Layout.BoxesOf(1));
        Assert.Equal([new PageBox(0, Box(0.3))], Layout.BoxesOf(2));
    }

    [Fact]
    public void A_sentence_over_two_pages_has_a_box_on_each()
    {
        Assert.Equal([new PageBox(2, Box(0.8)), new PageBox(3, Box(0.1))], Layout.BoxesOf(5));
    }

    [Fact]
    public void A_sentence_on_a_typed_page_has_no_boxes()
    {
        Assert.Empty(Layout.BoxesOf(4));
        Assert.Empty(Layout.BoxesOf(99));
    }

    [Fact]
    public void The_page_of_a_sentence_follows_the_position_being_spoken()
    {
        int sentence = 5;
        Assert.Equal(2, Layout.PageOfSentence(sentence));
        Assert.Equal(3, Layout.PageOfSentence(sentence, Text.IndexOf("until", StringComparison.Ordinal)));
        Assert.Equal(1, Layout.PageOfSentence(4));
        Assert.Equal(-1, Layout.PageOfSentence(-1));
    }

    [Fact]
    public void The_sentences_that_start_on_a_page_are_a_range()
    {
        Assert.Equal((0, 3), Layout.SentencesOnPage(0));
        Assert.Equal((4, 4), Layout.SentencesOnPage(1));
        Assert.Equal((5, 5), Layout.SentencesOnPage(2));
        Assert.Equal((-1, -1), Layout.SentencesOnPage(3));
    }

    [Fact]
    public void A_tap_inside_a_line_box_finds_its_sentence_and_outside_finds_none()
    {
        Assert.Equal(0, Layout.SentenceAt(0, 0.5, 0.15));
        Assert.Equal(3, Layout.SentenceAt(0, 0.5, 0.65));
        Assert.Equal(-1, Layout.SentenceAt(0, 0.5, 0.9));
        Assert.Equal(-1, Layout.SentenceAt(1, 0.5, 0.15));
    }

    [Fact]
    public void Page_starts_follow_the_joined_text()
    {
        Assert.Equal(0, Layout.PageStart(0));
        Assert.Equal(Text.IndexOf("A typed", StringComparison.Ordinal), Layout.PageStart(1));
        Assert.Equal(3, Layout.PageContaining(Text.IndexOf("until", StringComparison.Ordinal)));
    }

    [Fact]
    public void Remapping_a_composed_page_renumbers_its_sentences_to_the_real_ones()
    {
        var line = new ComposedLine("x", 0, 0, new NormalizedRect(0, 0, 1, 1), new NormalizedRect(0, 0, 1, 1));
        var page = new ComposedPage([line, line with { SentenceIndex = 1 }]);

        ComposedPage remapped = page.RemapSentences([4, 7]);

        Assert.Equal([4, 7], remapped.Lines.Select(l => l.SentenceIndex));
    }

    [Fact]
    public void A_zero_length_sentence_is_skipped_and_the_others_keep_their_real_index()
    {
        StoryPage[] pages = [new("One. Two."), new("Three.")];
        string text = Story.JoinPages(pages);
        int three = text.IndexOf("Three", StringComparison.Ordinal);
        SentenceSpan[] sentences = [new(0, 4), new(5, 5), new(5, 9), new(three, text.Length)];
        var layout = new ScanLayout(pages, sentences);

        IReadOnlyList<GeneratedSpan> spans = layout.SpansForGeneratedPage(0, carryTail: false);

        Assert.Equal([0, 2], spans.Select(s => s.Sentence));
        Assert.Equal((0, 2), layout.SentencesOnPage(0));
        Assert.Equal([3], layout.SpansForGeneratedPage(1, carryTail: false).Select(s => s.Sentence));
    }

    [Fact]
    public void Lines_are_found_in_flowing_text_despite_whitespace_and_end_of_line_hyphens()
    {
        string flowing = "The recognition of  hard\nwords is a  skill.";
        string[] lines = ["The recogni-", "tion of hard words", "is a skill."];

        IReadOnlyList<LocatedLine> located = RangeToBoxMapper.Locate(flowing, [lines]);

        Assert.Equal(3, located.Count);
        Assert.Equal(0, located[0].Start);
        Assert.Equal(flowing.IndexOf("tion", StringComparison.Ordinal) + "tion of  hard\nwords".Length, located[1].End);
        Assert.Equal(flowing.Length, located[2].End);
    }

    [Fact]
    public void A_layout_over_flowing_text_still_boxes_each_sentence()
    {
        StoryPage[] pages = [new("Peter hopped up and ran\naway from the garden.", "a.jpg",
            [new("Peter hopped up", Box(0.1)), new("and ran away", Box(0.2)), new("from the garden.", Box(0.3))])];

        var layout = new ScanLayout(pages, [new SentenceSpan(0, pages[0].Text.Length)]);

        Assert.Equal(3, layout.BoxesOf(0).Count);
    }

    [Fact]
    public void Flowing_text_is_cut_per_page_where_each_pages_lines_start()
    {
        string flowing = "Page one text goes\nhere. Page two starts\nthere and ends.";
        IReadOnlyList<string>? pages = ImportedText.SplitByPages(flowing,
            [["Page one text goes", "here."], ["Page two starts", "there and ends."]]);

        Assert.Equal(["Page one text goes\nhere.", "Page two starts\nthere and ends."], pages);
        Assert.Null(ImportedText.SplitByPages("unrelated", [["nothing here"]]));
    }

    [Fact]
    public void A_box_overshooting_the_page_by_rounding_is_clamped_and_a_far_off_box_drops_only_its_line()
    {
        var story = new Story("0123456789abcdef0123456789abcdef", "T", "en", DateTimeOffset.UnixEpoch,
            [new StoryPage("Hi there. Bye.", "a.jpg",
                [new PageLine("Hi there.", new NormalizedRect(0.1, 0.1, 0.9005, 0.1)), new PageLine("Bye.", new NormalizedRect(0.1, 0.3, 0.2, 0.1))])],
            [], CoverStyle.Colour, 0, "narrator");
        string json = StoryJson.Serialize(story).Replace("\"Y\": 0.3", "\"Y\": 4");

        StoryPage page = StoryJson.Deserialize(json).Pages[0];

        Assert.Equal("a.jpg", page.ImageFile);
        PageLine kept = Assert.Single(page.Lines!);
        Assert.Equal("Hi there.", kept.Text);
        Assert.True(kept.Box.Right <= 1);
    }

    [Fact]
    public void Lines_survive_a_save_and_load_and_a_damaged_line_is_dropped()
    {
        var story = new Story("0123456789abcdef0123456789abcdef", "T", "en", DateTimeOffset.UnixEpoch,
            [new StoryPage("Hi there.", "a.jpg", [new PageLine("Hi there.", Box(0.2))]), new StoryPage("Bye.")],
            [], CoverStyle.Colour, 0, "narrator");

        Story back = StoryJson.Deserialize(StoryJson.Serialize(story));
        Assert.Equal(Box(0.2), back.Pages[0].Lines![0].Box);
        Assert.Null(back.Pages[1].Lines);

        string damaged = StoryJson.Serialize(story).Replace("\"Width\": 0.8", "\"Width\": 7");
        Assert.Null(StoryJson.Deserialize(damaged).Pages[0].Lines);
    }

    [Fact]
    public void Locate_reports_each_found_line_with_its_text_range()
    {
        IReadOnlyList<LocatedLine> located = RangeToBoxMapper.Locate("ab cd\nef", [["ab cd", "zz", "ef"]]);

        Assert.Equal([new LocatedLine(new LineRef(0, 0), 0, 5), new LocatedLine(new LineRef(0, 2), 6, 8)], located);
    }

    [Fact]
    public void A_generated_page_after_a_scan_carries_the_tail_of_a_sentence_spanning_the_join()
    {
        StoryPage[] pages =
        [
            new("He ran and ran", "a.jpg", [new("He ran and ran", Box(0.1))]),
            new("until night fell. Then he slept."),
        ];
        string text = Story.JoinPages(pages);
        int join = text.IndexOf("until", StringComparison.Ordinal);
        int then = text.IndexOf("Then", StringComparison.Ordinal);
        SentenceSpan[] sentences = [new(0, join + "until night fell.".Length), new(then, text.Length)];
        var layout = new ScanLayout(pages, sentences);

        IReadOnlyList<GeneratedSpan> spans = layout.SpansForGeneratedPage(1, carryTail: true);

        Assert.Equal(0, spans[0].Sentence);
        Assert.Equal([new SentenceSpan(join, sentences[0].End), sentences[1]], spans.Select(s => s.Span));
        Assert.Equal(1, layout.PageOfSentence(0, join));
        Assert.Equal(0, layout.PageOfSentence(0));
        Assert.Equal(1, layout.SpansForGeneratedPage(1, carryTail: false)[0].Sentence);
    }
}

public class ImportedTextTests
{
    [Fact]
    public void A_page_is_its_lines_one_per_row()
    {
        Assert.Equal("one\ntwo", ImportedText.PageText(["one", "two"]));
    }

    [Fact]
    public void Text_is_unchanged_despite_line_endings_and_trailing_blanks_but_not_after_an_edit()
    {
        Assert.True(ImportedText.Unchanged("a\r\nb\n\n", "a\nb"));
        Assert.False(ImportedText.Unchanged("a\nB", "a\nb"));
    }
}
