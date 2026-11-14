using DinoReader.Core;

namespace DinoReader.Tests;

public class ReadingModesTests
{
    private static readonly LanguageRules Rules = LanguageRules.English;

    // Ten units per character, so a line of N characters is 10 * N wide.
    private sealed class FixedMeasurer : ITextMeasurer
    {
        public double Width(string text) => text.Length * 10;
    }

    private static string Cut(string text, SentenceSpan s) => text[s.Start..s.End];

    private static List<string> Sentences(string text) =>
        StorySentences.Build(text, Rules).Select(s => Cut(text, s)).ToList();

    [Fact]
    public void Sentences_split_at_terminators_and_trim_whitespace()
    {
        Assert.Equal(["Peter ran.", "Did he stop?", "No!"], Sentences("Peter ran.  Did he stop? No!"));
    }

    [Fact]
    public void A_quote_stays_with_its_attribution()
    {
        string text = "“Only if you’re quick,” said Cotton Tail, and off she ran. “I’m quick!” Peter said.";
        Assert.Equal(
            ["“Only if you’re quick,” said Cotton Tail, and off she ran.", "“I’m quick!” Peter said."],
            Sentences(text));
    }

    [Fact]
    public void Two_quotes_in_a_row_are_two_sentences()
    {
        Assert.Equal(["“Go!”", "“No!” said Mum."], Sentences("“Go!” “No!” said Mum."));
    }

    [Fact]
    public void A_title_abbreviation_does_not_end_a_sentence()
    {
        Assert.Equal(["Mr. Fox smiled.", "Then he left."], Sentences("Mr. Fox smiled. Then he left."));
    }

    [Fact]
    public void A_line_break_ends_a_sentence_even_without_a_terminator()
    {
        Assert.Equal(["The Little Lighthouse", "Once there was a light."], Sentences("The Little Lighthouse\n\nOnce there was a light."));
    }

    [Fact]
    public void Sentences_never_overlap_and_cover_every_non_space_character()
    {
        string text = "Dino put on his backpack. “Don’t forget your lunch!” called Mum.\nHe ran";
        IReadOnlyList<SentenceSpan> spans = StorySentences.Build(text, Rules);
        string covered = string.Concat(spans.Select(s => Cut(text, s)).SelectMany(x => x).Where(c => !char.IsWhiteSpace(c)));
        Assert.Equal(string.Concat(text.Where(c => !char.IsWhiteSpace(c))), covered);
        for (int i = 1; i < spans.Count; i++)
        {
            Assert.True(spans[i].Start >= spans[i - 1].End);
        }
    }

    [Fact]
    public void IndexAt_finds_the_sentence_the_next_one_in_a_gap_and_the_last_past_the_end()
    {
        string text = "One two.  Three four.";
        IReadOnlyList<SentenceSpan> spans = StorySentences.Build(text, Rules);
        Assert.Equal(0, StorySentences.IndexAt(spans, 0));
        Assert.Equal(0, StorySentences.IndexAt(spans, 7));
        Assert.Equal(1, StorySentences.IndexAt(spans, 9));
        Assert.Equal(1, StorySentences.IndexAt(spans, 999));
        Assert.Equal(-1, StorySentences.IndexAt([], 0));
    }

    [Fact]
    public void Slicing_from_a_sentence_drops_what_was_read_and_trims_the_segment_holding_it()
    {
        string text = "Peter ran. Then he stopped. “Wait!” said Mum.";
        IReadOnlyList<Segment> segments = new StorySegmenter(Rules).Split(text);
        int start = text.IndexOf("Then", StringComparison.Ordinal);
        IReadOnlyList<Segment> cut = SegmentSlicer.From(segments, text, start);
        Assert.Equal("Then he stopped.", cut[0].Text);
        Assert.Equal(start, cut[0].SourceStart);
        Assert.Equal(SegmentKind.Quote, cut[1].Kind);
        Assert.Equal(3, cut.Count);
    }

    [Fact]
    public void Slicing_from_a_quote_mark_keeps_the_whole_quote()
    {
        string text = "He said. “Wait for me!” puffed Peter.";
        IReadOnlyList<Segment> segments = new StorySegmenter(Rules).Split(text);
        IReadOnlyList<Segment> cut = SegmentSlicer.From(segments, text, text.IndexOf('“'));
        Assert.Equal("Wait for me!", cut[0].Text);
        Assert.Equal(SegmentKind.Quote, cut[0].Kind);
    }

    [Fact]
    public void Slicing_from_the_start_keeps_everything_and_past_the_end_keeps_nothing()
    {
        string text = "One. “Two.” Three.";
        IReadOnlyList<Segment> segments = new StorySegmenter(Rules).Split(text);
        Assert.Equal(segments.Count, SegmentSlicer.From(segments, text, 0).Count);
        Assert.Empty(SegmentSlicer.From(segments, text, text.Length));
    }

    [Fact]
    public void Wrap_breaks_between_words_at_the_width()
    {
        Assert.Equal(["aaa bbb", "ccc"], LineWrapper.Wrap("aaa bbb ccc", 70, new FixedMeasurer()));
    }

    [Fact]
    public void Wrap_keeps_a_word_wider_than_the_line_alone()
    {
        Assert.Equal(["a", "longerthanline", "b"], LineWrapper.Wrap("a longerthanline b", 50, new FixedMeasurer()));
    }

    [Fact]
    public void WrapWords_reports_where_each_word_starts()
    {
        IReadOnlyList<IReadOnlyList<WordAt>> lines = LineWrapper.WrapWords("aaa  bbb ccc", 100, 70, new FixedMeasurer());
        Assert.Equal([new WordAt("aaa", 100), new WordAt("bbb", 105)], lines[0]);
        Assert.Equal([new WordAt("ccc", 109)], lines[1]);
    }

    [Fact]
    public void WrapParagraphs_keeps_offsets_across_line_breaks()
    {
        IReadOnlyList<IReadOnlyList<WordAt>> lines = LineWrapper.WrapParagraphs("one\n\ntwo", 10, 500, new FixedMeasurer());
        Assert.Equal(3, lines.Count);
        Assert.Empty(lines[1]);
        Assert.Equal(15, lines[2][0].Start);
    }

    [Fact]
    public void CountLines_counts_a_blank_line()
    {
        Assert.Equal(3, LineWrapper.CountLines("one\n\ntwo", 500, new FixedMeasurer()));
    }

    private static readonly PageGeometry Geometry = new(Width: 400, Height: 200, PaddingX: 20, PaddingY: 20,
        LineHeight: 30, LineGap: 5, HighlightOutset: 6);

    private static (string Text, IReadOnlyList<SentenceSpan> Sentences, IReadOnlyList<ComposedPage> Pages) Compose(string text)
    {
        IReadOnlyList<SentenceSpan> sentences = StorySentences.Build(text, Rules);
        return (text, sentences, new PageComposer(Geometry, new FixedMeasurer()).Compose(text, sentences));
    }

    [Fact]
    public void Composed_text_covers_every_word_of_every_sentence()
    {
        (string text, IReadOnlyList<SentenceSpan> sentences, IReadOnlyList<ComposedPage> pages) =
            Compose("Dino put on his backpack today. “Don’t forget your lunch!” called Mum. Dino was too excited to hear. The bus came. He ran after it.");
        string composed = string.Join(' ', pages.SelectMany(p => p.Lines).Select(l => l.Text));
        string original = string.Join(' ', sentences.Select(s => Cut(text, s)));
        Assert.Equal(original.Split(' '), composed.Split(' '));
    }

    [Fact]
    public void Every_sentence_starts_on_its_own_line()
    {
        (_, _, IReadOnlyList<ComposedPage> pages) = Compose("Short one. Another short one. A third.");
        IEnumerable<ComposedLine> lines = pages.SelectMany(p => p.Lines);
        Assert.Equal(3, lines.Count());
        Assert.Equal([0, 1, 2], lines.Select(l => l.SentenceIndex));
    }

    [Fact]
    public void Line_boxes_on_a_page_do_not_overlap_and_stay_inside_the_page()
    {
        (_, _, IReadOnlyList<ComposedPage> pages) = Compose(string.Join(' ', Enumerable.Range(1, 12).Select(i => $"Sentence number {i} is here.")));
        foreach (ComposedPage page in pages)
        {
            for (int i = 0; i < page.Lines.Count; i++)
            {
                NormalizedRect box = page.Lines[i].Box;
                Assert.InRange(box.Y, 0, 1);
                Assert.InRange(box.Bottom, 0, 1);
                Assert.InRange(box.Right, 0, 1);
                Assert.True(i == 0 || box.Y >= page.Lines[i - 1].Box.Bottom);
            }
        }
    }

    [Fact]
    public void A_sentence_that_fits_a_page_is_not_split_across_pages()
    {
        // Geometry holds 4 lines a page; each sentence below takes 3 lines.
        string text = string.Join(' ', Enumerable.Range(1, 5).Select(_ => "aaaaaaaaaaaaaaaaaaa bbbbbbbbbbbbbbbbbbb ccccccccccccccccccc."));
        (_, IReadOnlyList<SentenceSpan> sentences, IReadOnlyList<ComposedPage> pages) = Compose(text);
        for (int s = 0; s < sentences.Count; s++)
        {
            Assert.Equal(1, pages.Count(p => p.Holds(s)));
        }
    }

    [Fact]
    public void A_sentence_longer_than_a_page_runs_over_onto_the_next_page()
    {
        string text = string.Join(' ', Enumerable.Repeat("wordwordword", 40)) + ".";
        (_, _, IReadOnlyList<ComposedPage> pages) = Compose(text);
        Assert.True(pages.Count > 1);
        Assert.Equal(0, PageComposer.PageOf(pages, 0));
        Assert.All(pages, p => Assert.True(p.Holds(0)));
    }

    [Fact]
    public void A_multi_line_sentence_has_stacked_boxes_and_a_point_finds_its_sentence()
    {
        (_, _, IReadOnlyList<ComposedPage> pages) = Compose("aaaaaaaaaaaaaaaaaaa bbbbbbbbbbbbbbbbbbb ccccccccccccccccccc. Short.");
        ComposedPage page = pages[0];
        IReadOnlyList<NormalizedRect> boxes = page.BoxesOf(0);
        Assert.Equal(3, boxes.Count);
        Assert.True(boxes[1].Y > boxes[0].Bottom);
        Assert.Equal(0, page.SentenceAt(0.5, boxes[1].Y + boxes[1].Height / 2));
        Assert.Equal(1, page.SentenceAt(0.5, page.Lines[^1].Box.Y + 0.01));
        Assert.Equal(-1, page.SentenceAt(0.5, 0.001));
    }

    [Fact]
    public void Page_text_is_vertically_centred()
    {
        (_, _, IReadOnlyList<ComposedPage> pages) = Compose("Only one line.");
        NormalizedRect box = pages[0].Lines[0].Box;
        Assert.Equal(1 - box.Bottom, box.Y, 6);
    }

    [Fact]
    public void FlowPager_fills_pages_with_whole_sentences()
    {
        string text = string.Join(' ', Enumerable.Range(1, 9).Select(i => $"Sentence {i} ends."));
        IReadOnlyList<SentenceSpan> sentences = StorySentences.Build(text, Rules);
        IReadOnlyList<FlowPage> pages = FlowPager.Paginate(text, sentences, 200, 2, new FixedMeasurer());
        Assert.True(pages.Count > 1);
        Assert.Equal(0, pages[0].FirstSentence);
        Assert.Equal(sentences.Count - 1, pages[^1].LastSentence);
        for (int i = 1; i < pages.Count; i++)
        {
            Assert.Equal(pages[i - 1].LastSentence + 1, pages[i].FirstSentence);
        }

        Assert.All(pages, p => Assert.True(LineWrapper.CountLines(text[p.Start..p.End], 200, new FixedMeasurer()) <= 2));
        Assert.Equal(1, FlowPager.PageOf(pages, pages[1].FirstSentence));
    }

    [Fact]
    public void FlowPager_gives_an_overlong_sentence_a_page_of_its_own()
    {
        string text = "Short. " + string.Join(' ', Enumerable.Repeat("wordwordword", 30)) + ". Tail.";
        IReadOnlyList<SentenceSpan> sentences = StorySentences.Build(text, Rules);
        IReadOnlyList<FlowPage> pages = FlowPager.Paginate(text, sentences, 300, 3, new FixedMeasurer());
        Assert.Contains(pages, p => p.FirstSentence == 1 && p.LastSentence == 1);
    }

    [Fact]
    public void Follower_follows_the_reading_page_until_the_child_swipes_away()
    {
        var follower = new PageFollower();
        follower.Read(0);
        follower.Read(1);
        Assert.Equal(1, follower.ShownPage);
        follower.Swiped(3);
        Assert.True(follower.IsWandering);
        follower.Read(2);
        Assert.Equal(3, follower.ShownPage);
        Assert.Equal(2, follower.ReadingPage);
    }

    [Fact]
    public void Follower_follows_again_on_resume_on_a_tap_or_a_swipe_back()
    {
        var follower = new PageFollower();
        follower.Read(1);
        follower.Swiped(4);
        follower.Resume();
        Assert.True(follower.IsFollowing);
        Assert.Equal(1, follower.ShownPage);

        follower.Swiped(0);
        follower.ResumeAt(0);
        Assert.True(follower.IsFollowing);
        Assert.Equal(0, follower.ReadingPage);

        follower.Read(2);
        follower.Swiped(0);
        follower.Swiped(2);
        Assert.True(follower.IsFollowing);
    }

    [Fact]
    public void Mode_is_picture_and_text_when_the_story_has_pictures_else_text_only()
    {
        Assert.Equal(ReadingMode.Text, Make([new StoryPage("a")], null).Mode);
        Assert.Equal(ReadingMode.PictureAndText, Make([new StoryPage("a", "page-01.jpg")], null).Mode);
        Assert.Equal(ReadingMode.Text, Make([new StoryPage("a", "page-01.jpg")], ReadingMode.Text).Mode);
        Assert.Equal(ReadingMode.PictureOnly, Make([new StoryPage("a", "page-01.jpg")], ReadingMode.PictureOnly).Mode);
        Assert.Equal(ReadingMode.Text, Make([new StoryPage("a")], ReadingMode.PictureOnly).Mode);
        Assert.Equal(ReadingMode.Text, Make([new StoryPage("a")], ReadingMode.PictureAndText).Mode);
    }

    [Theory]
    [InlineData("Picture", ReadingMode.PictureAndText)]
    [InlineData("picture", ReadingMode.PictureAndText)]
    [InlineData("Text", ReadingMode.Text)]
    [InlineData("PictureOnly", ReadingMode.PictureOnly)]
    [InlineData("PictureAndText", ReadingMode.PictureAndText)]
    public void A_story_file_saved_before_the_three_modes_reads_its_picture_choice_as_picture_and_text(string stored, ReadingMode expected)
    {
        string json = StoryJson.Serialize(Make([new StoryPage("a", "page-01.jpg")], ReadingMode.Text)).Replace("\"Text\",", $"\"{stored}\",");
        Assert.Contains($"\"{stored}\"", json);

        Assert.Equal(expected, StoryJson.Deserialize(json).PreferredMode);
    }

    [Theory]
    [InlineData("\"Picture\"", ReadingMode.PictureOnly)]
    [InlineData("null", ReadingMode.PictureOnly)]
    [InlineData("\"Text\"", ReadingMode.Text)]
    [InlineData("\"PictureOnly\"", ReadingMode.PictureOnly)]
    public void A_book_saved_with_the_words_off_opens_picture_only_unless_the_grown_up_chose_text(string stored, ReadingMode expected)
    {
        string json = StoryJson.Serialize(Make([new StoryPage("a", "page-01.jpg")], ReadingMode.Text))
            .Replace("\"PreferredMode\": \"Text\",", $"\"PreferredMode\": {stored}, \"WordsOverPictures\": false,");
        Assert.Contains("\"WordsOverPictures\"", json);

        Story back = StoryJson.Deserialize(json);

        Assert.Equal(expected, back.PreferredMode);
        Assert.DoesNotContain("WordsOverPictures", StoryJson.Serialize(back));
    }

    [Fact]
    public void A_book_saved_with_the_words_on_keeps_its_picture_and_text_view()
    {
        string json = StoryJson.Serialize(Make([new StoryPage("a", "page-01.jpg")], ReadingMode.Text))
            .Replace("\"PreferredMode\": \"Text\",", "\"PreferredMode\": \"Picture\", \"WordsOverPictures\": true,");

        Assert.Equal(ReadingMode.PictureAndText, StoryJson.Deserialize(json).PreferredMode);
    }

    [Fact]
    public void A_drafts_picture_only_choice_survives_a_save_without_pictures_and_applies_again_when_they_return()
    {
        var withPictures = Make([new StoryPage("a", "page-01.jpg")], ReadingMode.PictureOnly);
        var draft = new StoryDraft(withPictures);

        Story noPictures = draft.Current with { Pages = [new StoryPage("a", null)] };
        Assert.Equal(ReadingMode.Text, noPictures.Mode);

        Assert.Equal(ReadingMode.PictureOnly, (noPictures with { Pages = withPictures.Pages }).Mode);
    }

    [Fact]
    public void Picture_only_is_written_under_its_own_name_so_it_is_not_taken_for_the_old_picture_choice()
    {
        Story story = Make([new StoryPage("a", "page-01.jpg")], ReadingMode.PictureOnly);
        string json = StoryJson.Serialize(story);

        Assert.Contains("\"PictureOnly\"", json);
        Assert.Equal(ReadingMode.PictureOnly, StoryJson.Deserialize(json).PreferredMode);
    }

    [Fact]
    public void The_reading_mode_survives_a_story_json_round_trip_and_an_old_file_has_none()
    {
        Story story = Make([new StoryPage("a")], ReadingMode.PictureAndText);
        Assert.Equal(ReadingMode.PictureAndText, StoryJson.Deserialize(StoryJson.Serialize(story)).PreferredMode);

        string old = StoryJson.Serialize(story with { PreferredMode = null });
        Assert.Null(StoryJson.Deserialize(old).PreferredMode);
    }

    [Fact]
    public void A_sentence_ends_after_a_closing_quote_unless_an_attribution_follows()
    {
        Assert.Equal(
            ["“I love carrots.”", "The rabbit hopped home.", "The end."],
            Sentences("“I love carrots.” The rabbit hopped home. The end."));
        Assert.Equal(["“Hi!” he said.", "Then he left."], Sentences("“Hi!” he said. Then he left."));
    }

    [Fact]
    public void A_quote_ending_in_a_comma_is_never_split_inside()
    {
        string text = "\"Go away! I'm busy,\" said Tom.";
        Assert.Equal([text], Sentences(text));
        string curly = "“Go away! I’m busy,” said Tom. He left.";
        Assert.Equal(["“Go away! I’m busy,” said Tom.", "He left."], Sentences(curly));
    }

    [Fact]
    public void A_quote_followed_by_a_new_sentence_may_split_inside_and_after()
    {
        Assert.Equal(["“Go home.", "Now!”", "Tom left."], Sentences("“Go home. Now!” Tom left."));
    }

    [Fact]
    public void Farsi_question_marks_and_guillemets_keep_a_quote_with_its_attribution()
    {
        string text = "«من هم بیام؟» پیتر پرسید. او رفت.";
        Assert.Equal(["«من هم بیام؟» پیتر پرسید.", "او رفت."], Sentences(text));
    }

    [Fact]
    public void Splitting_segments_gives_one_piece_per_sentence_with_exact_positions()
    {
        string text = "Peter ran. He hid. Then he slept.";
        IReadOnlyList<SentenceSpan> sentences = StorySentences.Build(text, Rules);
        IReadOnlyList<Segment> segments = new StorySegmenter(Rules).Split(text);
        IReadOnlyList<SplitSegment> pieces = SegmentSentenceSplitter.Split(segments, text, sentences);
        Assert.Equal(["Peter ran.", "He hid.", "Then he slept."], pieces.Select(p => p.Segment.Text).ToList());
        Assert.Equal(sentences.Select(x => x.Start).ToList(), pieces.Select(p => p.Segment.SourceStart).ToList());
        Assert.All(pieces, p => Assert.Equal(p.TextStart, p.Segment.SourceStart));
        Assert.Equal(text.Length, pieces[^1].Segment.SourceEnd);
        Assert.All(pieces, p => Assert.Equal(p.Segment.Text, text.Substring(p.TextStart, p.Segment.Text.Length)));
    }

    [Fact]
    public void Splitting_a_quote_keeps_its_opening_mark_on_the_first_piece_and_its_end_on_the_last()
    {
        string text = "“Go home. Now!” Tom left.";
        IReadOnlyList<SentenceSpan> sentences = StorySentences.Build(text, Rules);
        IReadOnlyList<Segment> segments = new StorySegmenter(Rules).Split(text);
        Segment quote = segments.First(x => x.Kind == SegmentKind.Quote);
        IReadOnlyList<SplitSegment> pieces = SegmentSentenceSplitter.Split([quote], text, sentences);
        Assert.Equal(2, pieces.Count);
        Assert.Equal(quote.SourceStart, pieces[0].Segment.SourceStart);
        Assert.Equal("Go home.", pieces[0].Segment.Text);
        Assert.Equal(text.IndexOf("Now", StringComparison.Ordinal), pieces[1].Segment.SourceStart);
        Assert.Equal(pieces[1].Segment.SourceStart, pieces[1].TextStart);
        Assert.Equal(quote.SourceEnd, pieces[1].Segment.SourceEnd);
        Assert.Equal(1, StorySentences.IndexAt(sentences, pieces[1].Segment.SourceStart));
    }

    [Fact]
    public void A_segment_that_is_not_an_exact_slice_of_the_story_is_left_whole()
    {
        string text = "One. Two.";
        var odd = new Segment(SegmentKind.Narration, "One! Two!", 0, text.Length);
        IReadOnlyList<SplitSegment> pieces = SegmentSentenceSplitter.Split([odd], text, StorySentences.Build(text, Rules));
        Assert.Single(pieces);
        Assert.Equal(odd, pieces[0].Segment);
    }

    [Fact]
    public void Follower_follows_again_when_reading_reaches_the_page_the_child_is_on()
    {
        var follower = new PageFollower();
        follower.Read(0);
        follower.Swiped(2);
        Assert.True(follower.IsWandering);
        follower.Read(1);
        Assert.True(follower.IsWandering);
        follower.Read(2);
        Assert.True(follower.IsFollowing);
        Assert.Equal(2, follower.ShownPage);
        follower.Read(3);
        Assert.Equal(3, follower.ShownPage);
    }

    [Fact]
    public void A_relayout_keeps_a_wanderer_where_they_were_and_moves_a_follower_with_the_reading()
    {
        var wanderer = new PageFollower();
        wanderer.Read(0);
        wanderer.Swiped(3);
        wanderer.Relaid(1, 5);
        Assert.True(wanderer.IsWandering);
        Assert.Equal(5, wanderer.ShownPage);
        Assert.Equal(1, wanderer.ReadingPage);

        var follower = new PageFollower();
        follower.Read(2);
        follower.Relaid(4, 0);
        Assert.True(follower.IsFollowing);
        Assert.Equal(4, follower.ShownPage);
    }

    [Fact]
    public void An_unknown_reading_mode_in_a_story_file_keeps_the_story_readable()
    {
        string json = StoryJson.Serialize(Make([new StoryPage("a")], ReadingMode.PictureOnly));
        string unknown = json.Replace("\"PictureOnly\"", "\"Hologram\"");
        Assert.NotEqual(json, unknown);
        Story story = StoryJson.Deserialize(unknown);
        Assert.Null(story.PreferredMode);
        Assert.Equal("a", story.Text);

        string numeric = json.Replace("\"PictureOnly\"", "7");
        Assert.Null(StoryJson.Deserialize(numeric).PreferredMode);
        Assert.Null(StoryJson.Deserialize(json.Replace("\"PictureOnly\"", "\"1\"")).PreferredMode);
        Assert.Equal(ReadingMode.Text, StoryJson.Deserialize(json.Replace("\"PictureOnly\"", "\"text\"")).PreferredMode);
    }

    [Fact]
    public void LineCounter_matches_a_full_count_and_can_roll_back_a_trial()
    {
        string text = "one two three four five six seven eight nine ten\n\neleven twelve";
        var measurer = new FixedMeasurer();
        var counter = new LineCounter(150, measurer);
        counter.Add(text[..20]);
        LineState before = counter.State;
        counter.Add(text[20..]);
        Assert.Equal(LineWrapper.CountLines(text, 150, measurer), counter.Lines);
        counter.State = before;
        counter.Add(text[20..]);
        Assert.Equal(LineWrapper.CountLines(text, 150, measurer), counter.Lines);
    }

    private static string LongSentence(int words) => string.Join(' ', Enumerable.Repeat("wordwordword", words)) + ".";

    [Fact]
    public void FlowPager_splits_an_overlong_sentence_across_pages_by_line_so_no_page_is_clipped()
    {
        string text = "Short. " + LongSentence(30) + " Tail.";
        IReadOnlyList<SentenceSpan> sentences = StorySentences.Build(text, Rules);
        IReadOnlyList<FlowPage> pages = FlowPager.Paginate(text, sentences, 300, 3, new FixedMeasurer());

        Assert.All(pages, p => Assert.True(LineWrapper.CountLines(text[p.Start..p.End], 300, new FixedMeasurer()) <= 3));
        Assert.True(pages.Count(p => p.Holds(1)) > 1);
        int words = pages.Sum(p => text[p.Start..p.End].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length, words);
        for (int i = 1; i < pages.Count; i++)
        {
            Assert.True(pages[i].Start >= pages[i - 1].End);
        }
    }

    [Fact]
    public void FlowPager_PageOf_picks_the_page_holding_the_spoken_position_else_the_first()
    {
        string text = "Short. " + LongSentence(30) + " Tail.";
        IReadOnlyList<SentenceSpan> sentences = StorySentences.Build(text, Rules);
        IReadOnlyList<FlowPage> pages = FlowPager.Paginate(text, sentences, 300, 3, new FixedMeasurer());
        int first = FlowPager.PageOf(pages, 1);
        int third = first + 2;

        Assert.True(pages[first].Holds(1) && pages[third].Holds(1));
        Assert.Equal(first, FlowPager.PageOf(pages, 1, sentences[1].Start));
        Assert.Equal(third, FlowPager.PageOf(pages, 1, pages[third].Start + 3));
        Assert.Equal(first, FlowPager.PageOf(pages, 1, -1));
        Assert.Equal(-1, FlowPager.PageOf(pages, 99));
    }

    [Fact]
    public void An_overlong_sentence_that_does_not_fit_the_rest_of_a_page_starts_a_fresh_page()
    {
        string text = "Short. " + LongSentence(20);
        (_, _, IReadOnlyList<ComposedPage> pages) = Compose(text);
        Assert.Equal([0], pages[0].Lines.Select(l => l.SentenceIndex).Distinct());
        Assert.Equal(1, pages[1].FirstSentence);
        Assert.True(pages.Count > 2);
    }

    [Fact]
    public void Composer_PageOf_follows_the_spoken_position_into_the_later_pages_of_a_sentence()
    {
        string text = "Short. " + LongSentence(20);
        (_, IReadOnlyList<SentenceSpan> sentences, IReadOnlyList<ComposedPage> pages) = Compose(text);
        int last = pages.Count - 1;

        Assert.Equal(1, PageComposer.PageOf(pages, 1));
        Assert.Equal(1, PageComposer.PageOf(pages, 1, sentences[1].Start));
        Assert.Equal(last, PageComposer.PageOf(pages, 1, pages[last].FirstStart));
        Assert.Equal(last, PageComposer.PageOf(pages, 1, sentences[1].End - 1));
        Assert.Equal(0, PageComposer.PageOf(pages, 0, 0));
    }

    [Fact]
    public void Skip_while_paused_or_idle_only_moves_the_mark()
    {
        Assert.Equal(new SkipPlan(4, false), SkipPlanner.Plan(3, 1, 10, isPlayingOrLoading: false));
        Assert.Equal(new SkipPlan(4, true), SkipPlanner.Plan(3, 1, 10, isPlayingOrLoading: true));
    }

    [Fact]
    public void Skip_is_clamped_to_the_story_and_empty_stories_do_nothing()
    {
        Assert.Equal(new SkipPlan(0, false), SkipPlanner.Plan(0, -1, 10, false));
        Assert.Equal(new SkipPlan(9, true), SkipPlanner.Plan(9, 1, 10, true));
        Assert.Equal(new SkipPlan(0, false), SkipPlanner.Plan(-1, 0, 10, false));
        Assert.Equal(new SkipPlan(-1, false), SkipPlanner.Plan(0, 1, 0, true));
    }

    [Fact]
    public void MarkingPicturesAsPages_OpensPictureOnly_UnlessAChoiceWasMade()
    {
        Story story = Story.NewOwnBook(Guid.NewGuid().ToString("N"), "en", DateTimeOffset.UnixEpoch, 0);
        var draft = new StoryDraft(story);
        draft.MarkPicturesAsPages();
        Assert.Equal(ReadingMode.PictureOnly, draft.Current.PreferredMode);

        var chosen = new StoryDraft(story with { PreferredMode = ReadingMode.Text });
        chosen.MarkPicturesAsPages();
        Assert.Equal(ReadingMode.Text, chosen.Current.PreferredMode);
    }

    private static Story Make(IReadOnlyList<StoryPage> pages, ReadingMode? mode) =>
        new(StoryIds.New(), "T", "en", DateTimeOffset.UnixEpoch, pages, [], CoverStyle.Colour, 0, "M1", mode);
}
