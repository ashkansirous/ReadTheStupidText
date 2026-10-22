using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class SpeechTextChunkerTests
{
    // Mirrors SpeechTextChunker.TargetChunkMax (private): no chunk should greatly
    // exceed this, and a single word is the only thing allowed past it.
    private const int TargetChunkMax = 240;

    // Mirrors SpeechTextChunker.FirstChunkMax (private): the first chunk to play is
    // kept short so time-to-first-audio is a fast synthesis.
    private const int FirstChunkMax = 120;

    [Fact]
    public void Short_text_stays_a_single_trimmed_chunk()
    {
        IReadOnlyList<string> chunks = SpeechTextChunker.Split("  Hello there.  ");

        Assert.Single(chunks);
        Assert.Equal("Hello there.", chunks[0]);
    }

    [Fact]
    public void Whitespace_only_returns_a_single_chunk()
    {
        Assert.Single(SpeechTextChunker.Split("   \n  "));
    }

    [Fact]
    public void Long_text_splits_into_multiple_chunks_each_within_target()
    {
        string sentence = "This is a moderately long sentence that carries some weight. ";
        string text = string.Concat(Enumerable.Repeat(sentence, 12)); // ~720 chars

        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text);

        Assert.True(chunks.Count > 1, "long text should split into several chunks");
        Assert.All(chunks, c => Assert.True(c.Length <= TargetChunkMax, $"chunk over target: {c.Length}"));
        Assert.All(chunks, c => Assert.False(string.IsNullOrWhiteSpace(c)));
    }

    [Fact]
    public void Paragraph_boundaries_start_new_chunks()
    {
        string p1 = string.Concat(Enumerable.Repeat("Alpha sentence here. ", 8)).Trim();
        string p2 = string.Concat(Enumerable.Repeat("Beta sentence here. ", 8)).Trim();
        string text = p1 + "\n\n" + p2;

        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text);

        // No chunk should straddle the paragraph break (mix Alpha and Beta).
        Assert.DoesNotContain(chunks, c => c.Contains("Alpha") && c.Contains("Beta"));
    }

    [Fact]
    public void First_chunk_of_a_long_read_is_just_the_leading_sentence()
    {
        // One paragraph of several sentences, over SingleChunkMax so it splits.
        string text = "First short sentence. " +
            string.Concat(Enumerable.Repeat("Then more padding sentence here. ", 8));

        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text);

        Assert.True(chunks.Count > 1, "long text should split");
        Assert.Equal("First short sentence.", chunks[0]);
    }

    [Fact]
    public void Default_head_is_one_sentence_even_when_two_would_fit_the_cached_cap()
    {
        string text = "One short sentence. Two short sentence. " +
            string.Concat(Enumerable.Repeat("Then more padding sentence here. ", 8));

        Assert.Equal("One short sentence.", SpeechTextChunker.Split(text)[0]);
    }

    [Fact]
    public void Cached_head_is_the_first_two_sentences()
    {
        string text = "One short sentence. Two short sentence. Three short sentence. " +
            string.Concat(Enumerable.Repeat("Then more padding sentence here. ", 8));

        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text, head: SpeechTextChunker.HeadPolicy.Cached);

        Assert.Equal("One short sentence. Two short sentence.", chunks[0]);
        Assert.StartsWith("Three short sentence.", chunks[1]);
    }

    [Fact]
    public void Cached_head_is_one_sentence_when_two_would_pass_its_cap()
    {
        string first = string.Concat(Enumerable.Repeat("alpha ", 22)).Trim() + "."; // ~131 chars
        string second = string.Concat(Enumerable.Repeat("beta ", 20)).Trim() + "."; // ~100 chars
        string text = $"{first} {second} " + string.Concat(Enumerable.Repeat("Then more padding sentence here. ", 8));

        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text, head: SpeechTextChunker.HeadPolicy.Cached);

        Assert.Equal(first, chunks[0]);
    }

    [Fact]
    public void A_first_chunk_already_within_the_head_cap_is_left_untouched()
    {
        string first = "One short sentence. Two short sentence."; // 2 sentences, under 120
        string text = first + "\n\n" + string.Concat(Enumerable.Repeat("Then more padding sentence here. ", 6));

        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text);

        Assert.Equal(first, chunks[0]);
    }

    [Fact]
    public void A_single_oversized_sentence_is_hard_wrapped_on_words()
    {
        string longSentence = string.Concat(Enumerable.Repeat("word ", 80)).Trim(); // ~400 chars, no . ! ?

        IReadOnlyList<string> chunks = SpeechTextChunker.Split(longSentence);

        Assert.True(chunks.Count > 1, "an oversized word-only sentence should wrap");
        Assert.All(chunks, c => Assert.True(c.Length <= TargetChunkMax, $"chunk over target: {c.Length}"));
    }

    // A faithful facsimile of read #54 from input-20260630.log — a 707-char clipboard
    // read whose first audio did not start until 5865 ms (the "~5 second" read). The
    // text is a copied diagnostic-log line + PR description: its leading run has no
    // sentence-ending ".!?<space>" for ~240 chars (the ":" / "|" / "·" separators and
    // "11:32.251" don't break, since SentenceBreak needs punctuation *followed by*
    // whitespace), so the first "sentence" is itself oversized and gets hard-wrapped.
    private const string SlowRead54 =
        "a phone number:11:32.251 51 Hotkey Read None Chrome | Slice 21: daily on-disk " +
        "diagnostic logs + open-logs button by ashkansirous Pull Request 123 " +
        "ashkansirous/ReadTheStupidText - Google Chrome 5338 5338 Adds two per-day " +
        "diagnostic files under the package TemporaryFolder logs: a Serilog rolling " +
        "system log carrying every id-correlated action and exception with fixed " +
        "Info Debug Warning Error levels, and an append-only input log the " +
        "ActivityInputLog writer adds one TSV row per activity-state transition to so " +
        "the two files join on the id.";

    [Fact]
    public void Slow_read_54_first_chunk_is_a_short_head_for_fast_first_audio()
    {
        IReadOnlyList<string> chunks = SpeechTextChunker.Split(SlowRead54);

        // Regression for the ~5 s latency: before the fix, chunk[0] was ~235 chars
        // because the leading "sentence" had no early ".!?<space>" boundary and was
        // hard-wrapped near the target, so the first synthesis (which gates
        // time-to-first-audio) was large. The first-chunk bias now peels a short head
        // regardless of punctuation, so the first synthesis is fast.
        Assert.True(chunks.Count > 1, "long text should split");
        Assert.True(
            chunks[0].Length <= FirstChunkMax,
            $"first chunk should be a short head; got {chunks[0].Length} chars");
        Assert.False(string.IsNullOrWhiteSpace(chunks[0]));
    }

    [Fact]
    public void SplitWithRanges_source_span_for_a_single_chunk_is_exact()
    {
        const string text = "  Hello there.  ";

        IReadOnlyList<SpeechTextChunker.TextChunk> chunks = SpeechTextChunker.SplitWithRanges(text);

        SpeechTextChunker.TextChunk chunk = Assert.Single(chunks);
        Assert.Equal("Hello there.", chunk.Text);
        Assert.Equal(text.Substring(chunk.SourceStart, chunk.SourceLength), chunk.Text);
    }

    [Fact]
    public void SplitWithRanges_covers_the_source_text_in_order_without_overlap()
    {
        string sentence = "This is a moderately long sentence that carries some weight. ";
        string text = string.Concat(Enumerable.Repeat(sentence, 12));

        IReadOnlyList<SpeechTextChunker.TextChunk> chunks = SpeechTextChunker.SplitWithRanges(text);

        Assert.True(chunks.Count > 1, "long text should split");
        int previousEnd = 0;
        foreach (SpeechTextChunker.TextChunk chunk in chunks)
        {
            Assert.InRange(chunk.SourceStart, previousEnd, text.Length);
            Assert.InRange(chunk.SourceEnd, chunk.SourceStart, text.Length);
            previousEnd = chunk.SourceEnd;
        }
    }

    [Fact]
    public void SplitWithRanges_word_count_matches_between_chunk_text_and_its_source_span()
    {
        string text = "First short sentence. " +
            string.Concat(Enumerable.Repeat("Then more padding sentence here. ", 8));

        IReadOnlyList<SpeechTextChunker.TextChunk> chunks = SpeechTextChunker.SplitWithRanges(text);

        foreach (SpeechTextChunker.TextChunk chunk in chunks)
        {
            string sourceSpan = text.Substring(chunk.SourceStart, chunk.SourceLength);
            int chunkWords = chunk.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            int sourceWords = sourceSpan.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.Equal(chunkWords, sourceWords);
        }
    }

    [Fact]
    public void SplitWithRanges_of_whitespace_only_text_is_a_degenerate_empty_span()
    {
        IReadOnlyList<SpeechTextChunker.TextChunk> chunks = SpeechTextChunker.SplitWithRanges("   \n  ");

        SpeechTextChunker.TextChunk chunk = Assert.Single(chunks);
        Assert.Equal(0, chunk.SourceLength);
    }

    // Slice 51: Chinese has no spaces, and its sentences end in fullwidth marks with no
    // whitespace after them.
    [Fact]
    public void Unspaced_chinese_splits_after_fullwidth_sentence_marks()
    {
        string sentence = "今天天气很好，我们一起去公园玩耍吧！你觉得怎么样？我觉得非常好。";
        string text = string.Concat(Enumerable.Repeat(sentence, 8));

        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= TargetChunkMax));
        Assert.All(chunks, c => Assert.EndsWith("。", c.Replace("！", "。").Replace("？", "。")));
        Assert.Equal(text, string.Concat(chunks.Select(c => c.Replace(" ", ""))));
    }

    [Fact]
    public void Unspaced_chinese_without_any_punctuation_wraps_by_character_count()
    {
        string text = string.Concat(Enumerable.Repeat("我们一起去公园", 100)); // 700 chars, no marks

        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= TargetChunkMax));
        Assert.Equal(text, string.Concat(chunks.Select(c => c.Replace(" ", ""))));
    }

    [Fact]
    public void Unspaced_chinese_ranges_tile_the_source_in_order()
    {
        string text = string.Concat(Enumerable.Repeat("我们一起去公园玩。", 60));

        IReadOnlyList<SpeechTextChunker.TextChunk> chunks = SpeechTextChunker.SplitWithRanges(text);

        Assert.Equal(0, chunks[0].SourceStart);
        Assert.Equal(text.Length, chunks[^1].SourceEnd);
        for (int i = 1; i < chunks.Count; i++)
        {
            Assert.Equal(chunks[i - 1].SourceEnd, chunks[i].SourceStart);
        }
    }

    [Fact]
    public void Cjk_sentence_split_keeps_closing_quotes_with_the_sentence_they_end()
    {
        string[] sentences = SpeechTextChunker.SplitSentences("他说：「走吧！」然后离开。").ToArray();

        Assert.Equal(["他说：「走吧！」", "然后离开。"], sentences);
    }

    [Fact]
    public void Cjk_sentence_split_trims_whitespace_before_the_next_piece_and_keeps_terminator_runs()
    {
        string[] sentences = SpeechTextChunker.SplitSentences("真的吗？！ 他问（轻声）。 好。").ToArray();

        Assert.Equal(["真的吗？！", "他问（轻声）。", "好。"], sentences);
    }

    [Theory]
    [InlineData("English")]
    [InlineData("Chinese")]
    [InlineData("Mixed")]
    public void SplitWithRanges_slices_the_source_back_to_each_chunks_non_whitespace_content(string kind)
    {
        string text = kind switch
        {
            "English" => string.Concat(Enumerable.Repeat("The quick brown fox jumps over the lazy dog.  ", 30)),
            "Chinese" => string.Concat(Enumerable.Repeat("他说：「我们一起去公园玩！」然后离开了。", 30)),
            _ => string.Concat(Enumerable.Repeat("Hello world. 我们一起去公园玩。\n\nNext paragraph here! ", 20)),
        };

        IReadOnlyList<SpeechTextChunker.TextChunk> chunks = SpeechTextChunker.SplitWithRanges(text);

        Assert.True(chunks.Count > 1);
        foreach (SpeechTextChunker.TextChunk chunk in chunks)
        {
            string slice = text.Substring(chunk.SourceStart, chunk.SourceLength);
            Assert.Equal(StripWhitespace(chunk.Text), StripWhitespace(slice));
        }
    }

    [Fact]
    public void A_wrapped_paragraph_is_one_sentence()
    {
        string text = "Now they have a good view, but\nCarrot wants to get even closer.";

        IReadOnlyList<string> sentences = SpeechTextChunker.SplitSentences(LineReflow.ReflowText(text)).ToList();

        Assert.Equal(["Now they have a good view, but Carrot wants to get even closer."], sentences);
    }

    [Fact]
    public void A_heading_followed_by_a_paragraph_becomes_two_units_with_a_paragraph_break()
    {
        string text = "Where to Find Ebooks\nYou can buy ebooks from large stores such as Amazon and Kobo.";

        string reflowed = LineReflow.ReflowText(text);

        Assert.Equal(["Where to Find Ebooks", "You can buy ebooks from large stores such as Amazon and Kobo."],
            SpeechTextChunker.SplitParagraphs(reflowed));
    }

    [Fact]
    public void Chunk_ranges_survive_trailing_blanks_after_a_wrap_hyphen()
    {
        string text = "They played foot- \nball in the park every single day of the long summer, and then they went home. " +
            string.Concat(Enumerable.Repeat("More words follow here to pass the limit. ", 6));

        IReadOnlyList<SpeechTextChunker.TextChunk> chunks = SpeechTextChunker.SplitWithRanges(text);

        Assert.NotEmpty(chunks);
    }

    [Fact]
    public void Chunk_ranges_survive_a_dropped_wrap_hyphen()
    {
        string text = "They played foot-\nball in the park every single day of the long summer, and then they went home. " +
            string.Concat(Enumerable.Repeat("More words follow here to pass the limit. ", 6));

        IReadOnlyList<SpeechTextChunker.TextChunk> chunks = SpeechTextChunker.SplitWithRanges(text);

        Assert.Contains("football", chunks[0].Text);
        Assert.Equal(0, chunks[0].SourceStart);
    }

    private static string StripWhitespace(string value) => string.Concat(value.Where(c => !char.IsWhiteSpace(c)));

    [Fact]
    public void Chunk_ranges_follow_only_the_hyphens_reflow_dropped()
    {
        const string text = "A foot-\nball and a pre-\n123 code, then Anglo-\nSaxon.";

        IReadOnlyList<SpeechTextChunker.TextChunk> chunks = SpeechTextChunker.SplitWithRanges(text);

        Assert.Equal("A football and a pre- 123 code, then Anglo-Saxon.", chunks[0].Text);
        Assert.Equal(text.Length, chunks[^1].SourceEnd);
    }
}
