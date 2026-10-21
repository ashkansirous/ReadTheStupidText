using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Infrastructure.Translation;

namespace ReadTheStupidText.Tests;

public class TranslationStreamingTests
{
    [Fact]
    public void The_first_block_is_one_or_two_short_sentences()
    {
        IReadOnlyList<string> blocks = TranslationBlocks.Plan("One. Two. Three. Four.");

        Assert.Equal("One. Two.", blocks[0]);
        Assert.Equal("Three. Four.", blocks[1]);
    }

    [Fact]
    public void A_long_first_sentence_stands_alone()
    {
        string longSentence = new string('a', 195) + ".";

        IReadOnlyList<string> blocks = TranslationBlocks.Plan(longSentence + " Short one.");

        Assert.Equal(longSentence, blocks[0]);
        Assert.Equal("Short one.", blocks[1]);
    }

    [Fact]
    public void Later_blocks_stay_near_the_block_size_and_keep_every_sentence()
    {
        string text = string.Join(" ", Enumerable.Range(1, 100).Select(i => $"Sentence number {i} is here."));

        IReadOnlyList<string> blocks = TranslationBlocks.Plan(text);

        Assert.All(blocks.Skip(1), b => Assert.True(b.Length <= TranslationBlocks.BlockChars));
        Assert.Equal(text, string.Join(" ", blocks));
    }

    [Fact]
    public void Blank_text_has_no_blocks() => Assert.Empty(TranslationBlocks.Plan("  \n\n "));

    [Fact]
    public void Paragraph_breaks_do_not_hide_sentences()
    {
        IReadOnlyList<string> blocks = TranslationBlocks.Plan("One.\n\nTwo.\n\nThree.");

        Assert.Equal("One. Two.", blocks[0]);
        Assert.Equal("Three.", blocks[1]);
    }

    [Fact]
    public void The_feed_plans_appended_blocks_against_the_whole_text()
    {
        const string first = "Hello there.";
        var feed = new ChunkFeed(SpeechChunkPlanner.ForText(first), first.Length, complete: false);

        int added = feed.Append("Second block.");

        Assert.Equal(1, added);
        SpeechChunkPlan chunk = feed[1];
        string whole = first + ChunkFeed.BlockJoin + "Second block.";
        Assert.Equal("Second block.", whole[chunk.SourceStart..chunk.SourceEnd]);
    }

    [Fact]
    public void An_appended_block_is_not_biased_to_a_short_head()
    {
        var feed = new ChunkFeed([], 0, complete: false);

        feed.Append("One short. Two short. Three short.");

        Assert.Equal(1, feed.Count);
    }

    [Fact]
    public void Blocks_that_arrive_before_the_feed_exists_are_applied_in_order_and_completion_is_kept()
    {
        var pending = new PendingStreamBlocks();
        pending.Add("Second block.");
        pending.Add("Third block.");
        pending.MarkComplete();
        var feed = new ChunkFeed(SpeechChunkPlanner.ForText("First."), 6, complete: false);
        var counts = new List<int>();

        pending.DrainInto(feed, counts.Add);

        Assert.Equal(3, feed.Count);
        Assert.Equal([2, 3], counts);
        Assert.Equal("Second block.", feed[1].Text);
        Assert.Equal("Third block.", feed[2].Text);
        Assert.True(feed.IsComplete);
    }

    [Fact]
    public void Draining_without_completion_leaves_the_feed_open()
    {
        var pending = new PendingStreamBlocks();
        pending.Add("More.");
        var feed = new ChunkFeed([], 0, complete: false);

        pending.DrainInto(feed);

        Assert.Equal(1, feed.Count);
        Assert.False(feed.IsComplete);
    }

    [Fact]
    public void A_completed_feed_takes_no_more_text()
    {
        var feed = new ChunkFeed([], 0, complete: false);
        feed.Complete();

        Assert.Equal(0, feed.Append("Late."));
        Assert.True(feed.IsComplete);
    }

    [Fact]
    public async Task Appending_extends_the_timing_before_the_playback_loop_can_see_the_new_chunk()
    {
        var timing = new ReadTimingTracker();
        timing.Start(1);
        var feed = new ChunkFeed(SpeechChunkPlanner.ForText("First."), 6, complete: false);
        Task waiting = feed.WaitForMoreAsync(1, CancellationToken.None);

        feed.Append("Second block.", count => timing.Extend(count));
        await waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        timing.RecordChunkDuration(1, TimeSpan.FromSeconds(1)); // would throw if the timing had not grown
        Assert.Equal(2, feed.Count);
    }

    [Fact]
    public async Task Waiting_for_more_wakes_on_an_append_and_on_completion()
    {
        var feed = new ChunkFeed([], 0, complete: false);
        Task waiting = feed.WaitForMoreAsync(0, CancellationToken.None);
        Assert.False(waiting.IsCompleted);

        feed.Append("New text.");
        await waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Task next = feed.WaitForMoreAsync(1, CancellationToken.None);
        feed.Complete();
        await next.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Waiting_for_more_honours_cancellation()
    {
        var feed = new ChunkFeed([], 0, complete: false);
        using var cts = new CancellationTokenSource();
        Task waiting = feed.WaitForMoreAsync(0, cts.Token);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    [Fact]
    public void An_open_read_has_no_known_total_until_it_closes()
    {
        var tracker = new ReadTimingTracker();
        tracker.Start(1);
        tracker.SetOpen(true);
        tracker.RecordChunkDuration(0, TimeSpan.FromSeconds(4));

        Assert.Null(tracker.CurrentTiming(TimeSpan.Zero).Total);

        tracker.Extend(2);
        tracker.RecordChunkDuration(1, TimeSpan.FromSeconds(6));
        tracker.SetOpen(false);

        Assert.Equal(TimeSpan.FromSeconds(10), tracker.CurrentTiming(TimeSpan.Zero).Total);
    }

    [Fact]
    public void Extending_keeps_recorded_durations_and_elapsed()
    {
        var tracker = new ReadTimingTracker();
        tracker.Start(1);
        tracker.RecordChunkDuration(0, TimeSpan.FromSeconds(4));
        tracker.AdvancePastChunk(0);

        tracker.Extend(3);
        tracker.Extend(2);

        Assert.Equal(TimeSpan.FromSeconds(4), tracker.CurrentTiming(TimeSpan.Zero).Elapsed);
        Assert.Null(tracker.CurrentTiming(TimeSpan.Zero).Total);
    }

    [Fact]
    public async Task A_stream_translates_blocks_in_order_with_a_budget_each()
    {
        var translator = new BlockTranslator();
        TranslationStream stream = Plan(translator, "One. Two. Three. Four.");

        List<string> blocks = await Collect(stream);

        Assert.Equal(["[One. Two.]", "[Three. Four.]"], blocks);
        Assert.Equal(TranslationOutcome.Translated, stream.Outcome);
    }

    [Fact]
    public async Task A_timed_out_block_and_the_rest_fall_back_to_the_original()
    {
        var translator = new BlockTranslator { HangOnCall = 1 };
        TranslationStream stream = Plan(translator, "One. Two. " + new string('b', 700) + ". Three. " + new string('c', 700) + ".", TimeSpan.FromMilliseconds(50));

        List<string> blocks = await Collect(stream);

        Assert.True(blocks.Count >= 3);
        Assert.StartsWith("[", blocks[0]);
        Assert.DoesNotContain(blocks.Skip(1), b => b.StartsWith('['));
        Assert.Equal(TranslationOutcome.TimedOut, stream.Outcome);
        Assert.Equal(2, translator.Calls); // later blocks were not even attempted
    }

    [Fact]
    public async Task Cancelling_the_callers_token_stops_the_stream()
    {
        var translator = new BlockTranslator { HangOnCall = 0 };
        TranslationStream stream = Plan(translator, "One. Two. Three.");
        using var cts = new CancellationTokenSource();

        Task<List<string>> run = Collect(stream, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public void Turning_translation_on_for_an_english_target_downloads_nothing()
    {
        var packs = new FakePacks();
        ReadTranslator subject = NewTranslator(new BlockTranslator(), packs, language: "en");

        subject.StartTargetPackDownload();

        Assert.Empty(packs.Requested);
    }

    [Fact]
    public void Turning_translation_on_for_a_language_with_no_model_downloads_nothing()
    {
        var packs = new FakePacks();
        ReadTranslator subject = NewTranslator(new BlockTranslator(), packs, language: "fa");

        subject.StartTargetPackDownload();

        Assert.Empty(packs.Requested);
    }

    [Fact]
    public async Task The_installer_downloads_only_what_is_missing_once()
    {
        var packs = new FakePacks { Installed = { "translation-fr-en" } };
        var installer = new TranslationPackInstaller(packs, new BlockTranslator(), new SilentLog());
        var states = new List<TranslationPackState>();
        installer.Progress += (_, p) => states.Add(p.State);

        Assert.Equal(["translation-en-ru"], installer.MissingPackIds("fr", "ru"));
        Assert.False(installer.StartInBackground("fr", "en"));
        Assert.True(installer.StartInBackground("fr", "ru"));
        Assert.True(installer.StartInBackground("fr", "ru"));
        await packs.Downloaded.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await WaitUntil(() => states.Contains(TranslationPackState.Ready));

        Assert.Equal(["translation-en-ru"], packs.Requested);
        Assert.Equal(TranslationPackState.Downloading, states[0]);
    }

    [Fact]
    public async Task A_failed_download_is_reported_and_can_be_retried()
    {
        var packs = new FakePacks { Result = PackDownloadResult.Offline };
        var installer = new TranslationPackInstaller(packs, new BlockTranslator(), new SilentLog());
        TranslationPackProgress? last = null;
        installer.Progress += (_, p) => last = p;

        Assert.True(installer.StartInBackground("en", "ru"));
        await WaitUntil(() => last?.State == TranslationPackState.Failed);

        Assert.Equal(PackDownloadResult.Offline, last!.Failure);
        await WaitUntil(() =>
        {
            installer.StartInBackground("en", "ru"); // refused while the failed run is still winding down
            return packs.Requested.Count >= 2;
        });
    }

    [Fact]
    public void The_translate_toggle_is_available_for_languages_a_pack_reaches_installed_or_not()
    {
        using var empty = new PackTranslator(() => Path.Combine(Path.GetTempPath(), "rtst-none-" + Guid.NewGuid().ToString("N")), () => "fr");

        Assert.True(empty.IsAvailable);
        Assert.True(new PackTranslator(() => "x", () => "en").IsAvailable);
        Assert.False(new PackTranslator(() => "x", () => "fa").IsAvailable);
        Assert.False(new PackTranslator(() => "x", () => "ja").IsAvailable);
    }

    [Fact]
    public void Translation_packs_parse_in_the_catalogue_and_the_manifest()
    {
        const string catalogue = """
            [{"id":"translation-fr-en","language":"fr-en","kind":"translation","version":"1","sizeBytes":100,"sha256":"ab","license":"CC-BY-4.0","url":"https://x/y.zip"},
             {"id":"future","language":"x","kind":"hologram","version":"1","sizeBytes":1,"sha256":"ab","license":"","url":"https://x/z.zip"}]
            """;

        IReadOnlyList<PackInfo> packs = PackCatalogParser.Parse(catalogue);

        PackInfo pack = Assert.Single(packs);
        Assert.Equal(PackKind.Translation, pack.Kind);
        Assert.Equal(["fr-en"], pack.Languages);

        PackManifest manifest = PackManifest.Parse("""{"id":"translation-fr-en","kind":"translation","language":"fr-en","version":"1","engine":"onnxruntime","files":["encoder.onnx"]}""");
        Assert.Equal(PackKind.Translation, manifest.Kind);
    }

    [Fact]
    public void A_translation_pack_never_serves_voice_or_ocr_selection()
    {
        var catalogue = new List<PackInfo>
        {
            new("translation-fr-en", ["fr-en"], PackKind.Translation, "1", 1, "ab", "", "u"),
            new("ocr-latin", ["fr"], PackKind.Ocr, "1", 1, "ab", "", "u"),
        };

        Assert.Null(PackSelection.FromCatalog(catalogue, PackKind.Voice, "fr-en"));
        Assert.Equal("translation-fr-en", PackSelection.FromCatalog(catalogue, PackKind.Translation, "fr-en")?.Id);
        Assert.Equal("ocr-latin", PackSelection.FromCatalog(catalogue, PackKind.Ocr, "fr")?.Id);
        Assert.Null(PackSelection.FromCatalog(catalogue, PackKind.Translation, "fr"));
    }

    [Fact]
    public void Installed_packs_are_selected_by_kind_with_the_fewest_languages_first()
    {
        InstalledVoicePack Installed(string id, PackKind kind, string languages) =>
            new($"/packs/{id}", new PackManifest(id, kind, languages, "1", "onnxruntime", ["a"]));
        var installed = new List<InstalledVoicePack>
        {
            Installed("translation-fr-en", PackKind.Translation, "fr-en"),
            Installed("ocr-latin", PackKind.Ocr, "en,fr"),
        };

        Assert.Equal("translation-fr-en", Assert.Single(PackSelection.Serving(installed, PackKind.Translation, "fr-en")).Manifest.Id);
        Assert.Empty(PackSelection.Serving(installed, PackKind.Voice, "fr-en"));
    }

    private static TranslationStream Plan(BlockTranslator translator, string text, TimeSpan? budget = null)
    {
        ReadTranslator subject = NewTranslator(translator, new FakePacks(), "en");
        if (budget is { } b)
        {
            subject.Budget = b;
        }

        TranslationStreamStart start = subject.PlanStreamAsync(text, 1, CancellationToken.None).GetAwaiter().GetResult()!;
        return start.Stream!;
    }

    private static ReadTranslator NewTranslator(BlockTranslator translator, FakePacks packs, string language)
    {
        var settings = new TestSettings { TranslateEnabled = true, Language = language };
        var installer = new TranslationPackInstaller(packs, translator, new SilentLog());
        return new ReadTranslator(translator, new FakeDetector("fr"), settings, new SilentLog(), installer);
    }

    private static async Task<List<string>> Collect(TranslationStream stream, CancellationToken token = default)
    {
        var blocks = new List<string>();
        await foreach (string block in stream.BlocksAsync(token))
        {
            blocks.Add(block);
        }

        return blocks;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    private sealed class BlockTranslator : ITranslator
    {
        private int _calls;

        public int Calls => _calls;

        public int? HangOnCall { get; set; }

        public IReadOnlyList<string> SupportedLanguages => ["en", "fr", "ru"];

        public IReadOnlyList<string> RequiredPackIds(string source, string target) =>
            source == "en" || target == "en" ? [$"translation-{source}-{target}"] : [$"translation-{source}-en", $"translation-en-{target}"];

        public Task<bool> IsModelReadyAsync(string source, string target) => Task.FromResult(true);

        public async Task<string> TranslateAsync(string text, string source, string target, CancellationToken cancellationToken)
        {
            int call = Interlocked.Increment(ref _calls) - 1;
            if (HangOnCall == call)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return $"[{text}]";
        }
    }

    private sealed class FakePacks : IVoicePackService
    {
        private readonly TaskCompletionSource _downloaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public HashSet<string> Installed { get; } = [];

        public List<string> Requested { get; } = [];

        public PackDownloadResult Result { get; set; } = PackDownloadResult.Ok;

        public Task Downloaded => _downloaded.Task;

        public Task<PackCatalogResult> GetCatalogAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PackCatalogResult> GetCatalogCachedAsync(TimeSpan maxAge, CancellationToken cancellationToken) => throw new NotSupportedException();

        public string? GetInstalledPath(string packId) => Installed.Contains(packId) ? "/packs/" + packId : null;

        public IReadOnlyList<InstalledVoicePack> GetInstalledVoicePacks() => [];

        public IReadOnlyList<InstalledVoicePack> GetInstalledOcrPacks() => [];

        public Task<PackDownloadResult> DownloadAsync(string packId, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            lock (Requested)
            {
                Requested.Add(packId);
            }

            progress?.Report(0.5);
            _downloaded.TrySetResult();
            if (Result == PackDownloadResult.Ok)
            {
                Installed.Add(packId);
            }

            return Task.FromResult(Result);
        }
    }
}
