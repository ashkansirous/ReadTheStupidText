using DinoReader.Core;
using Xunit;

namespace DinoReader.Tests;

/// <summary>Speaker first in Persian and Arabic (Slice 254): the tag comes before its quote, every piece keeps its range, speaker and kind.</summary>
public class SpeakerFirstTests
{
    private const string Pim = "Pim";

    // A story laid out piece by piece: a quote piece is written with its marks, a narration piece with a leading space; the sentences are given as piece ranges.
    private sealed class Layout
    {
        private readonly System.Text.StringBuilder _text = new();
        public List<SplitSegment> Pieces { get; } = [];

        public string Text => _text.ToString();

        public Layout Quote(string translated, string source, string? who = Pim)
        {
            int start = _text.Length;
            _text.Append('“').Append(source).Append('”');
            Pieces.Add(new SplitSegment(new Segment(SegmentKind.Quote, translated, start, _text.Length, who), start + 1));
            return this;
        }

        public Layout Tag(string translated, string source)
        {
            _text.Append(' ');
            int start = _text.Length;
            _text.Append(source);
            Pieces.Add(new SplitSegment(new Segment(SegmentKind.Narration, translated, start, _text.Length), start));
            return this;
        }

        public IReadOnlyList<SentenceSpan> Sentences(params int[] lastPieceOfEach)
        {
            var spans = new List<SentenceSpan>();
            int from = 0;
            foreach (int last in lastPieceOfEach)
            {
                spans.Add(new SentenceSpan(from, Pieces[last].Segment.SourceEnd));
                from = Pieces[last].Segment.SourceEnd + 1;
            }

            return spans;
        }
    }

    private static IReadOnlyList<SplitSegment> Regroup(Layout layout, string language, params int[] sentenceEnds) =>
        SpeakerFirst.Apply(layout.Pieces, SpeakerFirst.Plan(layout.Pieces, language, layout.Text, layout.Sentences(sentenceEnds)), markTags: true, layout.Text);

    [Theory]
    [InlineData("fa", "en", true, true)]
    [InlineData("ar", "en", true, true)]
    [InlineData("fa", "en", false, false)]
    [InlineData("en", "en", true, false)]
    [InlineData("fa", "fa", true, false)]
    [InlineData("nb", "en", true, false)]
    public void Regroups_only_a_real_translation_into_a_speaker_first_language(string spoken, string story, bool translated, bool expected) =>
        Assert.Equal(expected, SpeakerFirst.Regroups(spoken, story, translated));

    private static string[] Texts(IReadOnlyList<SplitSegment> pieces) => pieces.Select(p => p.Segment.Text).ToArray();

    // The texts as the page shows them (a joined tag with its colon); Texts is what a clip is looked up by.
    private static string[] Shown(IReadOnlyList<SplitSegment> pieces) => pieces.Select(p => p.IntroducesQuote ? SpeakerFirst.Colon(p.Segment.Text) : p.Segment.Text).ToArray();

    [Theory]
    [InlineData(SpeakerFirst.Persian)]
    [InlineData(SpeakerFirst.Arabic)]
    public void A_quote_tag_quote_line_reads_the_tag_then_the_whole_quote(string language)
    {
        // "A strong house," said Pim, "takes time."   (the quote's second half starts the next sentence, as in the Pigs)
        Layout layout = new Layout().Quote("Q1", "A strong house,").Tag("PIM SAID.", "said Pim,").Quote("Q2", "takes time.");

        IReadOnlyList<SplitSegment> result = Regroup(layout, language, 1, 2);

        Assert.Equal(["PIM SAID:", "Q1", "Q2"], Shown(result));
    }

    [Fact]
    public void A_quote_then_tag_line_reads_the_tag_first()
    {
        Layout layout = new Layout().Quote("Q", "Hello!").Tag("پیم گفت.", "said Pim.");

        Assert.Equal(["پیم گفت:", "Q"], Shown(Regroup(layout, SpeakerFirst.Persian, 1)));
    }

    [Theory]
    [InlineData("پیم گفت،", "پیم گفت:")]
    [InlineData("قال بيم.", "قال بيم:")]
    [InlineData("پیم گفت", "پیم گفت:")]
    [InlineData("پیم گفت。", "پیم گفت:")]
    [InlineData("پیم داد زد!", "پیم داد زد!")]
    public void A_moved_tag_ends_with_a_colon_instead_of_its_full_stop(string tag, string expected)
    {
        Layout layout = new Layout().Quote("Q", "Hello!").Tag(tag, "said Pim.");

        Assert.Equal([expected, "Q"], Shown(Regroup(layout, SpeakerFirst.Persian, 1)));
    }

    [Fact]
    public void A_tag_before_its_quote_and_plain_narration_stay_as_they_are()
    {
        Layout layout = new Layout().Tag("N1", "Pim said,").Quote("Q", "Hello!").Tag("N2", "The sun rose.");

        Assert.Equal(["N1", "Q", "N2"], Shown(Regroup(layout, SpeakerFirst.Persian, 1, 2)));
    }

    [Fact]
    public void A_tag_in_a_later_sentence_than_the_quote_is_not_its_tag()
    {
        Layout layout = new Layout().Quote("Q", "Hello!").Tag("N", "Then Pim left.");

        Assert.Equal(["Q", "N"], Shown(Regroup(layout, SpeakerFirst.Persian, 0, 1)));
    }

    [Fact]
    public void A_tag_that_ends_with_a_stop_does_not_pull_the_next_quote_into_the_group()
    {
        Layout layout = new Layout().Quote("Q1", "One!").Tag("T1.", "said Pim.").Quote("Q2", "Two!").Tag("T2.", "said Pim.");

        Assert.Equal(["T1:", "Q1", "T2:", "Q2"], Shown(Regroup(layout, SpeakerFirst.Persian, 1, 3)));
    }

    [Fact]
    public void A_quote_cut_across_sentences_with_its_tag_in_the_last_stays_as_written()
    {
        // "Hello." (sentence 0)  "Goodbye," said Pim. (sentence 1): no tag in the middle of the quote, no moved tag with a closing mark and no opening one.
        Layout layout = new Layout().Quote("Q1", "Hello.").Quote("Q2", "Goodbye,").Tag("T.", "said Pim.");
        IReadOnlyList<SentenceSpan> spans = layout.Sentences(0, 2);

        IReadOnlyList<SplitSegment> result = SpeakerFirst.Apply(layout.Pieces, SpeakerFirst.Plan(layout.Pieces, "fa", layout.Text, spans), markTags: true);

        Assert.Equal(["Q1", "Q2", "T."], Texts(result));
        int[] sentences = result.Select(p => StorySentences.IndexAt(spans, p.Segment.SourceStart)).ToArray();
        Assert.Equal(sentences.Order(), sentences);
    }

    [Theory]
    [InlineData("said the wolf, and he huffed and puffed.")]
    [InlineData("and ran home.")]
    [InlineData("said the wolf and ran all the way home.")]
    public void A_tag_with_an_action_clause_stays_after_its_quote(string narration)
    {
        Layout layout = new Layout().Quote("Q", "Come in,").Tag("N", narration);

        Assert.Equal(["Q", "N"], Shown(Regroup(layout, SpeakerFirst.Persian, 1)));
    }

    [Fact]
    public void Both_tags_of_a_quote_tag_quote_tag_line_come_first_and_nothing_is_dropped()
    {
        // "A," said Pim, "B," said Pim.
        Layout layout = new Layout().Quote("Q1", "A,").Tag("T1,", "said Pim,").Quote("Q2", "B,").Tag("T2.", "said Pim.");

        Assert.Equal(["T1,", "T2:", "Q1", "Q2"], Shown(Regroup(layout, SpeakerFirst.Persian, 3)));
    }

    [Theory]
    [InlineData("said Pim.", "said Pim.")]
    [InlineData("said Pim.", "پیم گفت.")]
    public void A_tag_that_fell_back_to_the_story_text_gets_no_colon_and_a_tag_with_a_colon_gets_no_second_one(string source, string text)
    {
        Layout layout = new Layout().Quote("Q", "Hello!").Tag(text, source);

        string tag = Shown(Regroup(layout, SpeakerFirst.Persian, 1))[0];

        Assert.DoesNotContain("::", tag);
        Assert.Equal(text == source, tag == text);
    }

    [Fact]
    public void A_tag_already_ending_with_a_colon_keeps_the_one_colon()
    {
        Layout layout = new Layout().Quote("Q", "Hello!").Tag("پیم گفت:", "said Pim.");

        Assert.Equal(["پیم گفت:", "Q"], Shown(Regroup(layout, SpeakerFirst.Persian, 1)));
    }

    [Fact]
    public void The_sound_plan_counts_the_quotes_of_a_regrouped_line_as_one_line()
    {
        // Pim's two quotes around his tag are two lines in the story, one line once the tag moves in front: the 2nd-line sound follows the line as it is heard.
        Layout layout = new Layout().Quote("Q1", "A,").Tag("T1,", "said Pim,").Quote("Q2", "B.");
        IReadOnlyList<SentenceSpan> spans = layout.Sentences(1, 2);
        var animals = new Dictionary<string, AnimalKind> { [Pim] = AnimalKind.Pig };

        IReadOnlyList<SplitSegment> heard = SpeakerFirst.JoinedSourceOrder(layout.Pieces, SpeakerFirst.Persian, layout.Text, spans);

        Assert.Single(AnimalSounds.Plan(layout.Pieces, animals, "en"));
        Assert.Empty(AnimalSounds.Plan(heard, animals, "en"));
        Assert.Equal(layout.Pieces, SpeakerFirst.JoinedSourceOrder(layout.Pieces, "en", layout.Text, spans));
    }

    [Fact]
    public void Two_quotes_put_side_by_side_read_as_one_quote_with_no_mark_between()
    {
        Layout layout = new Layout().Quote("Q1", "A,").Tag("T1,", "said Pim,").Quote("Q2", "B.");
        IReadOnlyList<SentenceSpan> spans = layout.Sentences(1, 2);
        IReadOnlyList<SplitSegment> heard = SpeakerFirst.Apply(layout.Pieces, SpeakerFirst.Plan(layout.Pieces, "fa", layout.Text, spans), markTags: true);
        StoryPage page = new(layout.Text, null);

        TranslatedStory shown = TranslatedStoryBuilder.Build([page], spans, heard);

        Assert.Equal("T1: “Q1 Q2”", shown.Text);
    }

    [Fact]
    public void A_cut_quote_and_a_cut_tag_stay_together_and_only_the_last_tag_piece_takes_the_colon()
    {
        Layout layout = new Layout().Quote("Q1a", "Hello!");
        layout.Pieces.Add(layout.Pieces[0] with { Segment = layout.Pieces[0].Segment with { Text = "Q1b" } });
        layout.Tag("T1.", "said Pim.");
        layout.Pieces.Add(layout.Pieces[2] with { Segment = layout.Pieces[2].Segment with { Text = "T2." } });

        Assert.Equal(["T1.", "T2:", "Q1a", "Q1b"], Shown(Regroup(layout, SpeakerFirst.Persian, 3)));
    }

    [Fact]
    public void Every_piece_keeps_its_range_speaker_kind_and_text_start()
    {
        Layout layout = new Layout().Quote("Q1", "A strong house,", "Pip").Tag("T.", "said Pip,").Quote("Q2", "takes time.", "Pip");

        IReadOnlyList<SplitSegment> result = Regroup(layout, SpeakerFirst.Persian, 1, 2);

        Assert.Equal(layout.Pieces.Count, result.Count);
        foreach (SplitSegment original in layout.Pieces)
        {
            SplitSegment moved = Assert.Single(result, p => p.Segment.SourceStart == original.Segment.SourceStart);
            Assert.Equal((original.Segment.SourceEnd, original.Segment.CharacterId, original.Segment.Kind, original.TextStart),
                (moved.Segment.SourceEnd, moved.Segment.CharacterId, moved.Segment.Kind, moved.TextStart));
        }
    }

    [Fact]
    public void Without_the_mark_flag_no_tag_is_marked_and_the_texts_stay_as_they_were()
    {
        Layout layout = new Layout().Quote("Q", "Hello!").Tag("T.", "said Pim.");
        IReadOnlyList<SpeakerFirst.Move> order = SpeakerFirst.Plan(layout.Pieces, "fa", layout.Text, layout.Sentences(1));

        IReadOnlyList<SplitSegment> unmarked = SpeakerFirst.Apply(layout.Pieces, order, markTags: false);

        Assert.Equal(["T.", "Q"], Texts(unmarked));
        Assert.DoesNotContain(unmarked, p => p.IntroducesQuote);
    }

    [Fact]
    public void Join_reorders_the_translated_pieces_and_never_changes_a_text_so_a_clip_is_still_found_by_it()
    {
        // Slice 280: the colon is only shown; the tag's own text, which its clip is looked up by, is as translated.
        Layout layout = new Layout().Quote("Q1", "A,").Tag("پیم گفت.", "said Pim,").Quote("Q2", "B.");
        IReadOnlyList<SentenceSpan> spans = layout.Sentences(1, 2);

        IReadOnlyList<SplitSegment> joined = SpeakerFirst.Join(layout.Pieces, "fa", "en", layout.Text, spans);

        Assert.Equal(["پیم گفت.", "Q1", "Q2"], Texts(joined));
        Assert.Equal([true, false, false], joined.Select(p => p.IntroducesQuote));
        Assert.Equal("پیم گفت:", SpeakerFirst.Colon(joined[0].Segment.Text));
        // The device voice says the tag as a lead-in (with the colon); the other pieces, and the lookup key of the tag, are as translated.
        Assert.Equal(["پیم گفت:", "Q1", "Q2"], joined.Select(p => p.SpokenText));
    }

    [Fact]
    public void A_flagged_tag_counts_as_pre_made_by_its_plain_text_not_the_colon_form()
    {
        Layout layout = new Layout().Quote("Q1", "A,").Tag("پیم گفت.", "said Pim,").Quote("Q2", "B.");
        IReadOnlyList<SplitSegment> joined = SpeakerFirst.Join(layout.Pieces, "fa", "en", layout.Text, layout.Sentences(1, 2));
        SplitSegment tag = joined[0];

        Assert.True(tag.IntroducesQuote);
        Assert.True(PremadeAudio.IsSpoken(tag.Segment.Text));
        Assert.Equal(joined.Count, PremadeAudio.SpokenCount(joined.Select(p => p.Segment.Text)));
        Assert.NotEqual(PremadeAudio.HashOf(tag.SpokenText), PremadeAudio.HashOf(tag.Segment.Text));
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("fa", "fa")]
    [InlineData("de", "en")]
    public void Join_leaves_a_story_that_is_not_translated_into_a_speaker_first_language_as_it_is(string spoken, string story)
    {
        Layout layout = new Layout().Quote("Q", "Hello!").Tag("T.", "said Pim.");

        Assert.Equal(layout.Pieces, SpeakerFirst.Join(layout.Pieces, spoken, story, layout.Text, layout.Sentences(1)));
    }

    [Fact]
    public void The_page_shows_the_joined_tag_with_a_colon_while_the_piece_text_stays_the_translation()
    {
        Layout layout = new Layout().Quote("Q", "Hello!").Tag("پیم گفت.", "said Pim.");
        IReadOnlyList<SentenceSpan> spans = layout.Sentences(1);
        IReadOnlyList<SplitSegment> joined = SpeakerFirst.Join(layout.Pieces, "fa", "en", layout.Text, spans);

        TranslatedStory shown = TranslatedStoryBuilder.Build([new StoryPage(layout.Text, null)], spans, joined);

        Assert.Equal("پیم گفت: “Q”", shown.Text);
        Assert.Equal("پیم گفت.", joined[0].Segment.Text);
    }

    [Fact]
    public async Task Translating_a_story_joins_it_from_the_pieces_just_translated_and_asks_for_nothing_more()
    {
        Layout layout = new Layout().Quote("Hello!", "Hello!").Tag("said Pim.", "said Pim.");
        var asked = new List<string>();

        IReadOnlyList<SplitSegment> result = await StoryTranslation.TranslateJoinedAsync(
            layout.Pieces, (text, _) => { asked.Add(text); return Task.FromResult(text == "said Pim." ? "پیم گفت." : "سلام!"); },
            "fa", "en", layout.Text, layout.Sentences(1), CancellationToken.None);

        Assert.Equal(["پیم گفت.", "سلام!"], Texts(result));
        Assert.Equal(["Hello!", "said Pim."], asked.Order());
    }

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("ja")]
    [InlineData("zh")]
    [InlineData("ru")]
    [InlineData("fr")]
    public void Other_languages_keep_the_order_they_were_translated_in(string language)
    {
        Layout layout = new Layout().Quote("Q1", "A strong house,").Tag("T.", "said Pim,").Quote("Q2", "takes time.");

        Assert.Equal(["Q1", "T.", "Q2"], Shown(Regroup(layout, language, 1, 2)));
        Assert.False(SpeakerFirst.Applies(language));
    }
}
