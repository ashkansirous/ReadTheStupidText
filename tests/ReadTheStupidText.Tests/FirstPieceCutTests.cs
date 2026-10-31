using System.Globalization;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

// Slice 214: the first piece of a read is cut to about 8 spoken words so the first synthesis is short.
public class FirstPieceCutTests
{
    private static readonly SpeechTextChunker.HeadPolicy Cut = SpeechTextChunker.HeadPolicy.Cached.WithShortFirstPiece();

    [Fact]
    public void Short_sentence_is_left_whole()
    {
        Assert.Equal(["Hello there, how are you today?"], SpeechTextChunker.Split("Hello there, how are you today?", true, Cut));
    }

    [Fact]
    public void Single_word_is_left_whole()
    {
        Assert.Equal(["Hello"], SpeechTextChunker.Split("Hello", true, Cut));
    }

    [Fact]
    public void Long_sentence_is_cut_at_the_last_clause_boundary_within_the_cap()
    {
        const string text = "When the morning finally came to the quiet village, the farmers walked slowly out to their fields and began the long day of work.";
        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text, true, Cut);
        Assert.Equal(2, chunks.Count);
        Assert.Equal("When the morning finally came to the quiet", chunks[0]);
        Assert.Equal(text, string.Join(' ', chunks));
    }

    [Fact]
    public void Long_sentence_without_punctuation_is_cut_at_a_word_boundary_near_the_cap()
    {
        const string text = "one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen";
        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text, true, Cut);
        Assert.Equal(2, chunks.Count);
        Assert.Equal("one two three four five six seven eight", chunks[0]);
        Assert.Equal("nine ten eleven twelve thirteen fourteen fifteen sixteen", chunks[1]);
    }

    [Fact]
    public void A_very_early_comma_is_not_a_cut()
    {
        const string text = "Yes, one two three four five six seven eight nine ten eleven twelve thirteen fourteen.";
        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text, true, Cut);
        Assert.StartsWith("Yes, one two", chunks[0]);
    }

    [Fact]
    public void Two_short_sentences_that_are_too_long_together_are_cut_at_the_first_sentence_end()
    {
        const string text = "The cat sat on the warm mat. Then the dog ran across the whole big green garden to see it.";
        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text, true, Cut);
        Assert.Equal("The cat sat on the warm mat.", chunks[0]);
    }

    [Fact]
    public void Number_with_comma_is_not_a_clause_boundary()
    {
        Assert.Equal(-1, SpeechTextChunker.FirstPieceCut("It cost 1,000 dollars."));
    }

    [Fact]
    public void Unspaced_chinese_is_cut_at_a_clause_mark_by_character_count()
    {
        const string text = "今天早上天气非常好，我们一起去公园里散步，然后在湖边吃了很多好吃的东西，大家都很开心。";
        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text, true, Cut);
        Assert.Equal(2, chunks.Count);
        Assert.Equal("今天早上天气非常好，", chunks[0]);
        Assert.Equal(text, chunks[0] + chunks[1]);
    }

    [Fact]
    public void Unspaced_japanese_without_marks_is_cut_by_character_count()
    {
        const string text = "今日はとてもいい天気でしたので私たちはみんなで公園へ行って楽しく遊びました";
        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text, true, Cut);
        Assert.Equal(2, chunks.Count);
        Assert.Equal(16, chunks[0].Length);
        Assert.Equal(text, chunks[0] + chunks[1]);
    }

    [Fact]
    public void Farsi_uses_the_arabic_comma_as_a_clause_boundary()
    {
        const string text = "صبح که شد، مردم دهکده آرام آرام به سوی مزرعه های خود رفتند و کار طولانی روز را شروع کردند.";
        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text, true, Cut);
        Assert.Equal(3, chunks.Count); // the rest of the sentence is itself cut to a short second piece
        Assert.Equal("صبح که شد،", chunks[0]);
        Assert.Equal(text, string.Join(' ', chunks));
    }

    [Fact]
    public void Abbreviations_and_initials_are_not_clause_ends()
    {
        const string text = "Mr. and Mrs. Smith walked slowly down to the old harbour with J. K. Rowling and Dr. Jones yesterday afternoon.";
        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text, true, Cut);
        Assert.Equal("Mr. and Mrs. Smith walked slowly down to", chunks[0]);
        Assert.Equal(-1, SpeechTextChunker.FirstPieceCut("e.g. etc. St. U.S. and so on are all fine.", 100));
    }

    [Fact]
    public void A_clause_mark_before_a_closing_quote_is_a_clause_end()
    {
        const string text = "\"When the morning finally came,\" she said to her friends, as they walked out to the fields and the farm.";
        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text, true, Cut);
        Assert.Equal("\"When the morning finally came,\"", chunks[0]);
    }

    [Fact]
    public void The_second_piece_is_only_the_rest_of_the_cut_sentence_and_is_bounded()
    {
        const string text = "When the morning finally came to the quiet village, the farmers walked slowly out to their fields and began the long day of work, and the children followed them carrying water, bread, and tools for the whole busy morning ahead. Then it rained.";
        IReadOnlyList<string> chunks = SpeechTextChunker.Split(text, true, Cut);
        Assert.True(chunks.Count >= 3);
        Assert.True(chunks[1].Split(' ').Length <= 16, chunks[1]);
        Assert.Equal(text, string.Join(' ', chunks));
    }

    [Fact]
    public void A_long_hint_is_accepted_and_cut_like_any_first_chunk()
    {
        const string longFirst = "When the morning finally came to the quiet village, the farmers walked slowly out to their fields and began the long day of work.";
        Assert.True(SpeechTextChunker.AcceptsHint(longFirst, longFirst, Cut));
        Assert.True(SpeechTextChunker.AcceptsHint(longFirst, longFirst, SpeechTextChunker.HeadPolicy.Cached));
        Assert.Equal(SpeechTextChunker.Split(longFirst, true, Cut), SpeechTextChunker.Split(longFirst + " Then it rained.", true, Cut, longFirst).Take(2));
        const string shortFirst = "The cat sat on the warm mat.";
        Assert.True(SpeechTextChunker.AcceptsHint(shortFirst + " Then it left.", shortFirst, Cut));
    }

    [Fact]
    public void Prepared_head_units_equal_the_units_the_read_starts_with_for_a_long_first_sentence()
    {
        const string text = "When the morning finally came to the quiet village, the farmers walked slowly out to their fields and began the long day of work, "
            + "and the children followed them carrying water and bread. Then it started to rain very hard over all the fields and the road. "
            + "Everybody ran home. The fire was lit and the soup was warm and the day ended well for the whole village after all.";
        IReadOnlyList<string> prepared = SpeechChunkPlanner.HeadUnits(text[..Math.Min(text.Length, 2000)], Cut, 4);
        var hints = new FirstChunkHints();
        Assert.False(hints.Remember(text, "s", Cut)); // a whole long text is no head: the read plans the pieces itself
        IReadOnlyList<SpeechChunkPlan> read = SpeechChunkPlanner.ForText(text, Cut, firstChunkHint: hints.Take(text, "s"));
        Assert.True(read.Count > 2);
        Assert.Equal(prepared.Take(2), read.Take(2).SelectMany(c => SentenceUnits.Split(c.Text)).Take(2));
        Assert.Equal(prepared[0], SentenceUnits.Split(read[0].Text)[0]);
        Assert.True(prepared[0].Length < 80, prepared[0]);
    }

    [Fact]
    public void A_remembered_long_head_gives_the_read_the_prepared_units()
    {
        const string head = "When the morning finally came to the quiet village, the farmers walked slowly out to their fields and began the long day of work.";
        const string text = head + " Then it started to rain very hard over all the fields and the road. Everybody ran home.";
        var hints = new FirstChunkHints();
        Assert.True(hints.Remember(head, "s", Cut));
        IReadOnlyList<string> prepared = SpeechChunkPlanner.HeadUnits(head, Cut, 4);
        IReadOnlyList<string> read = SpeechChunkPlanner.ForText(text, Cut, firstChunkHint: hints.Take(text, "s"))
            .SelectMany(c => SentenceUnits.Split(c.Text)).ToList();
        Assert.True(prepared.Count >= 2);
        Assert.Equal(prepared, read.Take(prepared.Count));
    }

    [Fact]
    public void The_prepared_head_budget_counts_the_pieces_of_a_cut_sentence_as_one_chunk()
    {
        const string text = "When the morning finally came to the quiet village, the farmers walked slowly out to their fields and began the long day of work. "
            + "Then it rained. Everybody ran home. The soup was warm. The day ended well. Another sentence came after that one. Yet another one followed it.";
        int plainChunks = SpeechChunkPlanner.ForText(text, SpeechTextChunker.HeadPolicy.Cached).Count;
        int cutChunks = SpeechChunkPlanner.ForText(text, Cut).Count;
        Assert.True(cutChunks > plainChunks);
        string covered = string.Join(' ', SpeechChunkPlanner.HeadUnits(text, Cut, 4));
        string before = string.Join(' ', SpeechChunkPlanner.HeadUnits(text, SpeechTextChunker.HeadPolicy.Cached, 4));
        Assert.StartsWith(before, covered);
    }

    [Theory]
    [InlineData("ไปโรงเรียนกับเพื่อนๆทุกวันแล้วกลับบ้านมากินข้าวกับครอบครัวที่รักมากที่สุดในโลกใบนี้")]
    [InlineData("ｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜｦﾝ")]
    public void Unspaced_southeast_asian_and_halfwidth_text_is_cut(string text)
    {
        int cut = SpeechTextChunker.FirstPieceCut(text);
        Assert.InRange(cut, 1, text.Length - 1);
        Assert.NotEqual(UnicodeCategory.NonSpacingMark, char.GetUnicodeCategory(text[cut]));
    }

    [Fact]
    public void An_ellipsis_and_a_sentence_final_I_end_a_clause_but_dotted_acronyms_do_not()
    {
        const string ellipsis = "Wait for me... then we all went home and sat down for a long quiet rest of the evening together";
        Assert.Equal("Wait for me...", ellipsis[..SpeechTextChunker.FirstPieceCut(ellipsis, 5)]);
        const string sentence = "So we said it was so do I. Then we ran home across the whole green field to the old warm house.";
        Assert.Equal("So we said it was so do I.", SpeechTextChunker.Split(sentence, true, Cut)[0]);
        Assert.Equal(-1, SpeechTextChunker.FirstPieceCut("The a.m. and p.m. and U.S. trips are fine.", 100));
    }

    [Fact]
    public void A_remembered_cut_free_head_is_also_the_first_chunk_of_the_read()
    {
        const string text = "The cat sat on the warm mat. Then the dog ran across the whole big green garden to see it and bark at the birds.";
        var hints = new FirstChunkHints();
        Assert.True(hints.Remember("The cat sat on the warm mat.", "s", Cut));
        IReadOnlyList<SpeechChunkPlan> read = SpeechChunkPlanner.ForText(text, Cut, firstChunkHint: hints.Take(text, "s"));
        Assert.Equal(SpeechChunkPlanner.HeadUnits("The cat sat on the warm mat.", Cut, 4)[0], SentenceUnits.Split(read[0].Text)[0]);
    }

    [Fact]
    public void Ranges_after_a_cut_are_contiguous_and_do_not_overlap()
    {
        const string text = "When the morning finally came to the quiet village, the farmers walked slowly out to their fields and began the long day of work, and the children followed them carrying water, bread, and tools for the whole busy morning ahead.  Then it rained.";
        IReadOnlyList<SpeechTextChunker.TextChunk> chunks = SpeechTextChunker.SplitWithRanges(text, true, Cut);
        Assert.True(chunks.Count >= 3);
        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.True(chunks[i].SourceStart < chunks[i].SourceEnd);
            Assert.Equal(chunks[i].Text, text[chunks[i].SourceStart..chunks[i].SourceEnd].Replace("  ", " "));
            if (i > 0)
            {
                Assert.True(chunks[i].SourceStart >= chunks[i - 1].SourceEnd);
                Assert.True(string.IsNullOrWhiteSpace(text[chunks[i - 1].SourceEnd..chunks[i].SourceStart]));
            }
        }
    }

    [Fact]
    public void Without_the_flag_the_split_is_unchanged()
    {
        const string text = "one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen";
        Assert.Single(SpeechTextChunker.Split(text, true, SpeechTextChunker.HeadPolicy.Cached));
    }

    [Fact]
    public void Later_chunks_are_not_cut_and_an_unbiased_split_is_untouched()
    {
        const string text = "one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen";
        Assert.Single(SpeechTextChunker.Split(text, biasFirstChunk: false, Cut));
    }
}
