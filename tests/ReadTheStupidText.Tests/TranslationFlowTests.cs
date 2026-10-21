using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Tests;

public class TranslationFlowTests
{
    [Theory]
    [InlineData(true, "fr", "en", true)]
    [InlineData(false, "fr", "en", false)]
    [InlineData(true, "en", "en", false)]
    [InlineData(true, "EN", "en", false)]
    [InlineData(true, null, "en", false)]
    [InlineData(true, "", "en", false)]
    [InlineData(true, "fr", null, false)]
    public void ShouldTranslate_needs_the_toggle_two_known_languages_and_a_difference(bool enabled, string? source, string? target, bool expected)
    {
        Assert.Equal(expected, TranslationFlow.ShouldTranslate(enabled, source, target));
    }

    [Theory]
    [InlineData("fr", "fr")]
    [InlineData("ZH-Latn", "zh")]
    [InlineData("ja-Latn", "ja")]
    [InlineData("und", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeTag_keeps_the_lowercase_primary_subtag(string? tag, string? expected)
    {
        Assert.Equal(expected, TranslationFlow.NormalizeTag(tag));
    }

    [Fact]
    public void SourceFromDetection_ignores_unsure_and_missing_guesses()
    {
        Assert.Equal("fa", TranslationFlow.SourceFromDetection(new DetectedLanguage("fa", 0.9f)));
        Assert.Null(TranslationFlow.SourceFromDetection(new DetectedLanguage("fa", 0.2f)));
        Assert.Null(TranslationFlow.SourceFromDetection(new DetectedLanguage("und", 0.99f)));
        Assert.Null(TranslationFlow.SourceFromDetection(null));
    }

    [Fact]
    public void IsPairSupported_needs_both_languages()
    {
        string[] supported = ["en", "fa", "fr"];

        Assert.True(TranslationFlow.IsPairSupported(supported, "fa", "EN"));
        Assert.False(TranslationFlow.IsPairSupported(supported, "xx", "en"));
        Assert.False(TranslationFlow.IsPairSupported(supported, "en", "xx"));
    }

    [Fact]
    public async Task TranslateAsync_translates_each_piece_and_keeps_paragraph_breaks()
    {
        var translator = new FakeTranslator();

        string result = await TranslationFlow.TranslateAsync(translator, "Bonjour.\n\nAu revoir.", "fr", "en", CancellationToken.None);

        Assert.Equal("[fr>en Bonjour.]\n\n[fr>en Au revoir.]", result);
    }

    [Fact]
    public async Task TranslateAsync_sends_a_whole_paragraph_in_one_call_even_when_it_has_many_sentences()
    {
        var translator = new FakeTranslator();
        string paragraph = string.Join(" ", Enumerable.Repeat("This is one sentence of the paragraph.", 20));

        string result = await TranslationFlow.TranslateAsync(translator, paragraph, "fr", "en", CancellationToken.None);

        Assert.Single(translator.Calls);
        Assert.Equal($"[fr>en {paragraph}]", result);
    }

    [Fact]
    public async Task TranslateAsync_translates_paragraphs_sequentially_in_order()
    {
        var translator = new FakeTranslator();
        string text = string.Join("\n\n", Enumerable.Range(1, 8).Select(i => $"Para {i}."));

        string result = await TranslationFlow.TranslateAsync(translator, text, "fr", "en", CancellationToken.None);

        Assert.Equal(string.Join("\n\n", Enumerable.Range(1, 8).Select(i => $"[fr>en Para {i}.]")), result);
    }

    [Fact]
    public async Task TranslateAsync_splits_only_a_very_long_paragraph_at_sentence_ends()
    {
        var translator = new FakeTranslator();
        string sentence = string.Concat(Enumerable.Repeat("word ", 100)).Trim() + ".";
        string text = string.Join(" ", Enumerable.Repeat(sentence, 12));

        string result = await TranslationFlow.TranslateAsync(translator, text, "fr", "en", CancellationToken.None);

        Assert.True(translator.Calls.Count > 1);
        Assert.All(translator.Calls, c => Assert.True(c.Length <= TranslationFlow.MaxPieceLength));
        Assert.All(translator.Calls, c => Assert.EndsWith(".", c.TrimEnd()));
        Assert.DoesNotContain("\n", result);
    }

    [Theory]
    [InlineData("جمله اول است؟ جمله دوم است۔ جمله سوم! ", "؟", "۔")]
    [InlineData("第一句。第二句！第三句？", "。", "！")]
    public void SplitLongParagraph_recognises_persian_and_chinese_sentence_ends(string unit, string firstEnd, string secondEnd)
    {
        string text = string.Concat(Enumerable.Repeat(unit, 400));

        string[] pieces = TranslationFlow.SplitLongParagraph(text).ToArray();

        Assert.True(pieces.Length > 1);
        Assert.Equal(text, string.Concat(pieces));
        Assert.All(pieces, p => Assert.True(p.Length <= TranslationFlow.MaxPieceLength));
        Assert.All(pieces, p => Assert.Contains(p.TrimEnd()[^1].ToString(), new[] { firstEnd, secondEnd, "!", "？" }));
    }

    [Fact]
    public async Task TranslateAsync_translates_a_short_chinese_paragraph_whole()
    {
        var translator = new FakeTranslator();

        string result = await TranslationFlow.TranslateAsync(translator, "你好。再见！", "zh", "en", CancellationToken.None);

        Assert.Equal(["你好。再见！"], translator.Calls);
        Assert.Equal("[zh>en 你好。再见！]", result);
    }

    [Fact]
    public void Prefix_bounds_the_text_without_cutting_a_surrogate_pair()
    {
        Assert.Equal("abc", TranslationFlow.Prefix("abc", 10));
        Assert.Equal("ab", TranslationFlow.Prefix("abcd", 2));
        Assert.Equal("a", TranslationFlow.Prefix("a😀b", 2));
        Assert.Equal(TranslationFlow.DetectionSampleLength, TranslationFlow.DetectionSample(new string('x', 5000)).Length);
    }

    [Fact]
    public async Task TranslateAsync_skips_blank_pieces()
    {
        var translator = new FakeTranslator();

        string result = await TranslationFlow.TranslateAsync(translator, "   ", "fr", "en", CancellationToken.None);

        Assert.Equal(string.Empty, result);
        Assert.Empty(translator.Calls);
    }

    [Fact]
    public async Task TranslateAsync_stops_when_cancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            TranslationFlow.TranslateAsync(new FakeTranslator(), "Bonjour.", "fr", "en", cts.Token));
    }

    [Fact]
    public async Task TranslateAsync_lets_a_missing_model_surface()
    {
        var translator = new FakeTranslator { Throw = new TranslationModelUnavailableException("offline") };

        await Assert.ThrowsAsync<TranslationModelUnavailableException>(() =>
            TranslationFlow.TranslateAsync(translator, "Bonjour.", "fr", "en", CancellationToken.None));
    }

    [Fact]
    public async Task TranslateAsync_surfaces_any_failure_as_itself_and_stops()
    {
        var translator = new FakeTranslator { ThrowOnCall = 2, Throw = new InvalidOperationException("boom") };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TranslationFlow.TranslateAsync(translator, "One.\n\nTwo.\n\nThree.", "fr", "en", CancellationToken.None));

        Assert.Equal(["One."], translator.Calls);
    }

    [Fact]
    public async Task TranslateAsync_surfaces_a_missing_model_from_a_later_paragraph()
    {
        var translator = new FakeTranslator { ThrowOnCall = 2, Throw = new TranslationModelUnavailableException("offline") };

        await Assert.ThrowsAsync<TranslationModelUnavailableException>(() =>
            TranslationFlow.TranslateAsync(translator, "One.\n\nTwo.", "fr", "en", CancellationToken.None));
    }

    private sealed class FakeTranslator : ITranslator
    {
        private readonly object _lock = new();
        private readonly List<string> _calls = [];
        private int _attempts;

        public IReadOnlyList<string> Calls { get { lock (_lock) { return _calls.ToList(); } } }

        public Exception? Throw { get; init; }

        // 1-based call number the exception is thrown on; 0 means every call.
        public int ThrowOnCall { get; init; }

        public IReadOnlyList<string> SupportedLanguages { get; } = ["en", "fr"];

        public Task<bool> IsModelReadyAsync(string source, string target) => Task.FromResult(true);

        public Task<string> TranslateAsync(string text, string source, string target, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                _attempts++;
                bool fails = Throw is not null && (ThrowOnCall == 0 || ThrowOnCall == _attempts);
                if (fails)
                {
                    throw Throw!;
                }

                _calls.Add(text);
            }

            return Task.FromResult($"[{source}>{target} {text}]");
        }
    }
}
