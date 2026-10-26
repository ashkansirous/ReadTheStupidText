using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Tests;

// Slice 142: the Windows read reuses the early translation of its first block and starts on the very units the early voice stored.
public class EarlyStartWindowsTests
{
    private static readonly SpeechTextChunker.HeadPolicy Cached = SpeechTextChunker.HeadPolicy.Cached;

    private const string French =
        "Je suis allee a Paris hier soir avec ma soeur. Oui! Nous avons mange des crepes tres bonnes. " +
        "Puis nous sommes rentrees a la maison tres tard dans la nuit. Fin de la journee.";

    private sealed class FakeNetwork(bool allows) : IPackUpdateNetwork
    {
        public bool AllowsAutomaticUpdate => allows;
    }

    private sealed class CountingPacks : IVoicePackService
    {
        private int _downloads;

        public int Downloads => Volatile.Read(ref _downloads);

        public Task<PackCatalogResult> GetCatalogAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PackCatalogResult> GetCatalogCachedAsync(TimeSpan maxAge, CancellationToken cancellationToken) => throw new NotSupportedException();
        public string? GetInstalledPath(string packId) => null;
        public IReadOnlyList<InstalledVoicePack> GetInstalledVoicePacks() => [];
        public IReadOnlyList<InstalledVoicePack> GetInstalledOcrPacks() => [];

        public Task<PackDownloadResult> DownloadAsync(string packId, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _downloads);
            return Task.FromResult(PackDownloadResult.Failed);
        }
    }

    private static List<string> Units(string text) =>
        SpeechChunkPlanner.ForText(text, Cached).Take(4)
            .SelectMany(c => SentenceUnits.Split(c.Text)).Select(SentenceCacheKey.Normalize).ToList();

    [Theory]
    [InlineData("The first sentence is here. The second one follows it. A third closes the span. And a fourth is the rest.")]
    [InlineData("Bonjour tout le monde. Comment allez-vous? Tres bien merci! Et vous alors, mon ami? Je vais bien.")]
    public void The_early_voice_units_are_the_first_units_of_the_translated_read(string translated)
    {
        // The early work voices the first two sentences of the translated span; the read's first block is that whole span.
        EarlySplit span = EarlyStartPlan.TranslationSpan(translated);
        string voiced = EarlyStartPlan.VoicedTranslation(span.Head);

        List<string> early = Units(voiced);
        var earlyTranslation = new EarlyTranslation(translated, "fr", "en", span, translated);
        List<string> read = Units(TranslationBlocks.Plan(translated, earlyTranslation)[0]).Take(early.Count).ToList();

        Assert.Equal(early, read);
    }

    [Fact]
    public async Task The_early_head_is_not_translated_again_and_the_rest_is()
    {
        var translator = new FakeTranslator("rest translated");
        ReadTranslator subject = Subject(translator);
        EarlySplit split = EarlyStartPlan.TranslationSpan(French);
        var early = new EarlyTranslation(French, "fr", "en", split, "Head translated.");

        ReadTranslationResult result = await subject.PrepareAsync(French, 1, CancellationToken.None, early);

        Assert.Equal(1, translator.Calls);
        Assert.StartsWith("Head translated.", result.Text);
        Assert.EndsWith("rest translated", result.Text);
    }

    [Fact]
    public async Task A_blank_translation_of_the_rest_never_leaves_only_the_head()
    {
        var translator = new FakeTranslator("   ");
        ReadTranslator subject = Subject(translator);
        var early = new EarlyTranslation(French, "fr", "en", EarlyStartPlan.TranslationSpan(French), "Head translated.");
        Assert.NotEmpty(early.Split.Remainder);

        ReadTranslationResult result = await subject.PrepareAsync(French, 1, CancellationToken.None, early);

        Assert.Equal(2, translator.Calls); // the rest, then the whole text
        Assert.Equal(TranslationOutcome.Failed, result.Outcome);
        Assert.Equal(French, result.Text);
    }

    [Fact]
    public async Task An_early_translation_of_another_text_is_ignored()
    {
        var translator = new FakeTranslator("whole");
        ReadTranslator subject = Subject(translator);
        var early = new EarlyTranslation("Other text.", "fr", "en", EarlyStartPlan.TranslationSpan("Other text."), "Other.");

        ReadTranslationResult result = await subject.PrepareAsync(French, 1, CancellationToken.None, early);

        Assert.Equal("whole", result.Text);
    }

    [Fact]
    public async Task The_stream_speaks_the_early_head_as_its_first_block_without_translating_it()
    {
        var translator = new FakeTranslator("later block");
        ReadTranslator subject = Subject(translator);
        EarlySplit split = EarlyStartPlan.TranslationSpan(French);
        var early = new EarlyTranslation(French, "fr", "en", split, "Head translated.");

        TranslationStreamStart? start = await subject.PlanStreamAsync(French, 1, CancellationToken.None, early);
        var spoken = new List<string>();
        await foreach (string block in start!.Stream!.BlocksAsync(CancellationToken.None))
        {
            spoken.Add(block);
        }

        Assert.Equal("Head translated.", spoken[0]);
        Assert.Equal(spoken.Count - 1, translator.Calls);
    }

    [Fact]
    public async Task Planning_finds_the_pair_only_when_the_text_differs_and_the_model_is_ready()
    {
        Assert.Equal(("fr", "en"), await Subject(new FakeTranslator("x")).PlanEarlyAsync(French, CancellationToken.None));
        Assert.Null(await Subject(new FakeTranslator("x") { ModelReady = false }).PlanEarlyAsync(French, CancellationToken.None));
        Assert.Null(await Subject(new FakeTranslator("x"), enabled: false).PlanEarlyAsync(French, CancellationToken.None));
        Assert.Null(await Subject(new FakeTranslator("x"), detected: "en").PlanEarlyAsync("Hello there my friend.", CancellationToken.None));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task A_missing_pack_downloads_early_only_on_an_unmetered_network(bool unmetered, bool downloadStarts)
    {
        var packs = new CountingPacks();
        var settings = new TestSettings { TranslateEnabled = true, Language = "en" };
        var installer = new TranslationPackInstaller(packs, new FakeTranslator("x") { ModelReady = false, Packs = ["pack-fr-en"] }, new SilentLog());
        var subject = new ReadTranslator(
            new FakeTranslator("x") { ModelReady = false }, new FakeDetector("fr"), settings, new SilentLog(), installer, new FakeNetwork(unmetered));

        Assert.Null(await subject.PlanEarlyAsync(French, CancellationToken.None));
        await Task.Delay(100);

        Assert.Equal(downloadStarts, packs.Downloads > 0);
    }

    private static ReadTranslator Subject(FakeTranslator translator, bool enabled = true, string detected = "fr")
    {
        var settings = new TestSettings { TranslateEnabled = enabled, Language = "en" };
        return new ReadTranslator(translator, new FakeDetector(detected), settings, new SilentLog());
    }
}
