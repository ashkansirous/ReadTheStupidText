using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 110: the translated text the reading screen shows, one span per source sentence.</summary>
public class TranslatedStoryTests
{
    private static Story StoryOf(params StoryPage[] pages) =>
        new(StoryIds.New(), "Title", "en", DateTimeOffset.UnixEpoch, pages, [], CoverStyle.Colour, 0, string.Empty);

    private static async Task<TranslatedStory> TranslateAsync(Story story, Func<string, string> translate)
    {
        var sentences = StorySentences.Build(story.Text, LanguageRules.For("en"), story.BreakMap());
        var pieces = await StoryTranslation.TranslateAsync(StoryPieces.Of(story), (t, _) => Task.FromResult(translate(t)), CancellationToken.None);
        return TranslatedStoryBuilder.Build(story.Pages, sentences, pieces);
    }

    [Fact]
    public async Task Each_source_sentence_gets_a_span_holding_its_translation()
    {
        Story story = StoryOf(new StoryPage("The pig ran. The wolf came."));

        TranslatedStory result = await TranslateAsync(story, t => t == "The pig ran." ? "خوک دوید." : "گرگ آمد.");

        Assert.Equal("خوک دوید. گرگ آمد.", result.Text);
        Assert.Equal(2, result.Sentences.Count);
        Assert.Equal("خوک دوید.", result.Text[result.Sentences[0].Start..result.Sentences[0].End]);
        Assert.Equal("گرگ آمد.", result.Text[result.Sentences[1].Start..result.Sentences[1].End]);
    }

    [Fact]
    public async Task A_long_translation_cut_for_speech_stays_in_its_one_sentence()
    {
        Story story = StoryOf(new StoryPage("The pig ran. The wolf came."));
        string longSentence = string.Join(' ', Enumerable.Repeat("word", 70)) + ".";

        TranslatedStory result = await TranslateAsync(story, t => t == "The pig ran." ? longSentence : "Two.");

        Assert.Equal(2, result.Sentences.Count);
        Assert.Equal(longSentence, result.Text[result.Sentences[0].Start..result.Sentences[0].End]);
    }

    [Fact]
    public async Task A_quote_is_shown_in_quote_marks_and_its_attribution_stays_in_the_same_sentence()
    {
        Story story = StoryOf(new StoryPage("\"Go away!\" said the pig."));

        TranslatedStory result = await TranslateAsync(story, t => "T:" + t);

        Assert.Single(result.Sentences);
        Assert.Contains("\u201CT:Go away!\u201D", result.Text);
        Assert.Contains("T:said the pig.", result.Text);
    }

    [Fact]
    public async Task A_paragraph_break_stays_a_paragraph_break()
    {
        Story story = StoryOf(new StoryPage("One pig.\n\nTwo pigs."));

        TranslatedStory result = await TranslateAsync(story, t => "T:" + t);

        Assert.Equal("T:One pig.\n\nT:Two pigs.", result.Text);
        Assert.Equal("T:Two pigs.", result.Text[result.Sentences[1].Start..result.Sentences[1].End]);
    }

    [Fact]
    public async Task Pages_keep_their_pictures_lose_their_boxes_and_the_spans_follow_the_joined_text()
    {
        var line = new PageLine("The pig ran.", new NormalizedRect(0, 0, 1, 0.1));
        Story story = StoryOf(new StoryPage("The pig ran.", "1.jpg", [line]), new StoryPage("The wolf came.", "2.jpg"));

        TranslatedStory result = await TranslateAsync(story, t => "T:" + t);

        Assert.Equal(["1.jpg", "2.jpg"], result.Pages.Select(p => p.ImageFile));
        Assert.All(result.Pages, p => Assert.Null(p.Lines));
        Assert.Equal(["T:The pig ran.", "T:The wolf came."], result.Pages.Select(p => p.Text));
        Assert.Equal("T:The wolf came.", result.Text[result.Sentences[1].Start..result.Sentences[1].End]);
        Assert.True(PictureTextBook.IsPictureTextBook(result.Pages));
    }

    [Fact]
    public void A_quote_over_two_sentences_is_one_quote()
    {
        const string text = "\"Go away. Not now!\" said the pig.";
        var sentences = new[] { new SentenceSpan(0, 10), new SentenceSpan(11, text.Length) };
        SplitSegment[] pieces =
        [
            new(new Segment(SegmentKind.Quote, "T:Go away.", 0, 10), 1),
            new(new Segment(SegmentKind.Quote, "T:Not now!", 11, 19), 11),
            new(new Segment(SegmentKind.Narration, "T:said the pig.", 19, text.Length), 20),
        ];

        TranslatedStory result = TranslatedStoryBuilder.Build([new StoryPage(text)], sentences, pieces);

        Assert.Equal("“T:Go away. T:Not now!” T:said the pig.", result.Text);
        Assert.Equal("“T:Go away.", result.Text[result.Sentences[0].Start..result.Sentences[0].End]);
    }

    [Fact]
    public async Task A_quote_the_source_left_open_gets_no_closing_mark()
    {
        Story story = StoryOf(new StoryPage("The pig said, \"Go away."));

        TranslatedStory result = await TranslateAsync(story, t => "T:" + t);

        Assert.Contains("“T:Go away.", result.Text);
        Assert.DoesNotContain('”', result.Text);
    }

    [Fact]
    public void A_sentence_running_over_a_page_break_shows_in_full_where_it_starts_and_is_not_repeated()
    {
        StoryPage[] pages = [new("The pig ran to the"), new("house. The wolf came.")];
        string source = Story.JoinPages(pages);
        int split = source.IndexOf("The wolf", StringComparison.Ordinal);
        var sentences = new[] { new SentenceSpan(0, split - 1), new SentenceSpan(split, source.Length) };
        SplitSegment[] pieces =
        [
            new(new Segment(SegmentKind.Narration, "T1", 0, split - 1), 0),
            new(new Segment(SegmentKind.Narration, "T2", split, source.Length), split),
        ];

        TranslatedStory result = TranslatedStoryBuilder.Build(pages, sentences, pieces);

        Assert.Equal(["T1", "T2"], result.Pages.Select(p => p.Text));
        Assert.Equal("T1", result.Text[result.Sentences[0].Start..result.Sentences[0].End]);
        Assert.Equal(0, result.Sentences[0].Start);
        Assert.Equal("T2", result.Text[result.Sentences[1].Start..result.Sentences[1].End]);
        Assert.Equal(result.Pages[0].Text.Length + Story.PageSeparator.Length, result.Sentences[1].Start);
    }

    [Fact]
    public void A_piece_covering_two_sentences_is_written_once_and_the_second_sentence_has_an_empty_span()
    {
        const string text = "One. Two. Three.";
        var sentences = new[] { new SentenceSpan(0, 4), new SentenceSpan(5, 9), new SentenceSpan(10, text.Length) };
        SplitSegment[] pieces =
        [
            new(new Segment(SegmentKind.Narration, "A", 0, 9), 0),
            new(new Segment(SegmentKind.Narration, "C", 10, text.Length), 10),
        ];

        TranslatedStory result = TranslatedStoryBuilder.Build([new StoryPage(text)], sentences, pieces);

        Assert.Equal("A C", result.Text);
        Assert.Equal(result.Sentences[1].Start, result.Sentences[1].End);
        Assert.Equal("C", result.Text[result.Sentences[2].Start..result.Sentences[2].End]);
    }

    [Fact]
    public void A_sentence_lying_between_pages_gets_an_empty_span_after_the_one_before_it()
    {
        StoryPage[] pages = [new("One."), new("Two.")];
        string source = Story.JoinPages(pages);
        var sentences = new[] { new SentenceSpan(0, 4), new SentenceSpan(4, 6), new SentenceSpan(6, source.Length) };
        SplitSegment[] pieces =
        [
            new(new Segment(SegmentKind.Narration, "A", 0, 4), 0),
            new(new Segment(SegmentKind.Narration, "B", 6, source.Length), 6),
        ];

        TranslatedStory result = TranslatedStoryBuilder.Build(pages, sentences, pieces);

        Assert.Equal(["A", "B"], result.Pages.Select(p => p.Text));
        Assert.Equal(new SentenceSpan(1, 1), result.Sentences[1]);
        Assert.Equal("B", result.Text[result.Sentences[2].Start..result.Sentences[2].End]);
    }

    [Fact]
    public void A_sentence_starting_in_the_page_separator_is_still_shown()
    {
        StoryPage[] pages = [new("One."), new("Two.")];
        string source = Story.JoinPages(pages);
        var sentences = new[] { new SentenceSpan(0, 4), new SentenceSpan(4, source.Length) };
        SplitSegment[] pieces =
        [
            new(new Segment(SegmentKind.Narration, "A", 0, 4), 0),
            new(new Segment(SegmentKind.Narration, "B", 4, source.Length), 4),
        ];

        TranslatedStory result = TranslatedStoryBuilder.Build(pages, sentences, pieces);

        Assert.Equal(["A", "B"], result.Pages.Select(p => p.Text));
        Assert.Equal("B", result.Text[result.Sentences[1].Start..result.Sentences[1].End]);
    }

    [Fact]
    public void A_sentence_without_a_translated_piece_shows_its_source_text()
    {
        const string text = "Hello there.";
        var sentences = new[] { new SentenceSpan(0, text.Length) };

        TranslatedStory result = TranslatedStoryBuilder.Build([new StoryPage(text)], sentences, []);

        Assert.Equal(text, result.Text);
    }

    [Fact]
    public void The_original_text_switch_survives_the_story_file_and_is_off_by_default()
    {
        Story story = StoryOf(new StoryPage("Hi."));

        Assert.False(StoryJson.Deserialize(StoryJson.Serialize(story)).ShowOriginalText);
        Assert.True(StoryJson.Deserialize(StoryJson.Serialize(story with { ShowOriginalText = true })).ShowOriginalText);
    }
}
