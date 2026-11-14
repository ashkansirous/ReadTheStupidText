using DinoReader.Core;

namespace DinoReader.Tests;

public class StoryTranslationTests
{
    private static SplitSegment Piece(string text, int start, int end, string? character = null, SegmentKind kind = SegmentKind.Quote) =>
        new(new Segment(kind, text, start, end, character), start + 1);

    private static Task<string> Upper(string text, CancellationToken ct) => Task.FromResult(text.ToUpperInvariant());

    [Fact]
    public async Task A_translated_piece_keeps_its_character_kind_and_source_range()
    {
        SplitSegment[] pieces = [Piece("hello", 10, 17, "Peter"), Piece("then he left.", 18, 31, null, SegmentKind.Narration)];

        var result = await StoryTranslation.TranslateAsync(pieces, Upper, CancellationToken.None);

        Assert.Equal(new[] { "HELLO", "THEN HE LEFT." }, result.Select(p => p.Segment.Text));
        Assert.Equal(new[] { (10, 17), (18, 31) }, result.Select(p => (p.Segment.SourceStart, p.Segment.SourceEnd)));
        Assert.Equal(new string?[] { "Peter", null }, result.Select(p => p.Segment.CharacterId));
        Assert.Equal(SegmentKind.Narration, result[1].Segment.Kind);
    }

    [Fact]
    public async Task A_piece_without_letters_is_not_sent_to_the_translator()
    {
        int calls = 0;
        Task<string> Count(string text, CancellationToken ct) { calls++; return Task.FromResult(text); }

        var result = await StoryTranslation.TranslateAsync([Piece("...", 0, 5)], Count, CancellationToken.None);

        Assert.Equal(0, calls);
        Assert.Equal("...", result.Single().Segment.Text);
    }

    [Fact]
    public async Task An_empty_translation_keeps_the_source_text()
    {
        var result = await StoryTranslation.TranslateAsync([Piece("hello", 0, 7)], (_, _) => Task.FromResult("  "), CancellationToken.None);

        Assert.Equal("hello", result.Single().Segment.Text);
    }

    [Fact]
    public async Task A_long_translation_is_cut_into_pieces_that_all_keep_the_source_range()
    {
        string sentence = "Word ".Repeat(30).Trim() + ".";
        string translated = string.Join(" ", Enumerable.Repeat(sentence, 3));

        var result = await StoryTranslation.TranslateAsync([Piece("x", 5, 9, "A")], (_, _) => Task.FromResult(translated), CancellationToken.None);

        Assert.True(result.Count > 1);
        Assert.All(result, p => Assert.True(p.Segment.Text.Length <= StoryTranslation.MaxSpokenChars));
        Assert.All(result, p => Assert.Equal((5, 9, "A"), (p.Segment.SourceStart, p.Segment.SourceEnd, p.Segment.CharacterId)));
    }

    [Fact]
    public async Task Cancelling_stops_the_translation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            StoryTranslation.TranslateAsync([Piece("hello", 0, 7)], Upper, cts.Token));
    }

    [Fact]
    public void Spoken_text_is_cut_at_a_space_when_one_sentence_is_too_long()
    {
        string text = string.Join(" ", Enumerable.Repeat("word", 120));

        var runs = StoryTranslation.SplitSpoken(text);

        Assert.True(runs.Count > 1);
        Assert.All(runs, r => Assert.True(r.Length <= StoryTranslation.MaxSpokenChars));
        Assert.Equal(text, string.Join(" ", runs));
    }

    [Fact]
    public void Spoken_text_with_no_spaces_is_cut_anywhere()
    {
        var runs = StoryTranslation.SplitSpoken(new string('好', 450));

        Assert.Equal(3, runs.Count);
        Assert.Equal(450, runs.Sum(r => r.Length));
    }

    [Fact]
    public void Short_text_stays_whole()
    {
        Assert.Equal(new[] { "One. Two! Three?" }, StoryTranslation.SplitSpoken("One. Two! Three?"));
    }

    [Fact]
    public async Task Pieces_translate_in_parallel_but_come_back_in_story_order()
    {
        var pieces = Enumerable.Range(0, 8).Select(i => Piece("p" + i, i * 10, i * 10 + 5)).ToList();
        int running = 0, peak = 0;

        var result = await StoryTranslation.TranslateAsync(pieces, async (text, _) =>
        {
            peak = Math.Max(peak, Interlocked.Increment(ref running));
            await Task.Delay(20);
            Interlocked.Decrement(ref running);
            return text.ToUpperInvariant() + ".";
        }, CancellationToken.None, maxParallel: 3);

        Assert.Equal(pieces.Select(p => p.Segment.Text.ToUpperInvariant() + "."), result.Select(r => r.Segment.Text));
        Assert.InRange(peak, 2, 3);
    }

    [Fact]
    public async Task A_cancelled_read_stops_translating_further_pieces()
    {
        var pieces = Enumerable.Range(0, 20).Select(i => Piece("p" + i, i * 10, i * 10 + 5)).ToList();
        using var cts = new CancellationTokenSource();
        int started = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => StoryTranslation.TranslateAsync(pieces, async (text, token) =>
        {
            if (Interlocked.Increment(ref started) == 2) cts.Cancel();
            await Task.Delay(20, token);
            return text;
        }, cts.Token, maxParallel: 1));

        Assert.True(started < pieces.Count);
    }

    [Fact]
    public async Task The_first_fault_cancels_the_remaining_pieces()
    {
        var pieces = Enumerable.Range(0, 20).Select(i => Piece("p" + i, i * 10, i * 10 + 5)).ToList();
        int started = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => StoryTranslation.TranslateAsync(pieces, async (text, token) =>
        {
            if (Interlocked.Increment(ref started) == 2) throw new InvalidOperationException("boom");
            await Task.Delay(20, token);
            return text;
        }, CancellationToken.None, maxParallel: 1));

        Assert.True(started < pieces.Count);
    }
}

internal static class StringRepeat
{
    public static string Repeat(this string s, int count) => string.Concat(Enumerable.Repeat(s, count));

}
