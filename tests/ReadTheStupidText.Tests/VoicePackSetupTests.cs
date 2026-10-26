using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Domain.Sanitizing;
using ReadTheStupidText.Infrastructure.Reading;
using ReadTheStupidText.Mobile;
using ReadTheStupidText.Mobile.Shared;

namespace ReadTheStupidText.Tests;

public class VoicePackSetupTests
{
    private static readonly PackInfo EnglishPack =
        new(PackConstants.SupertonicPackId, ["en"], PackKind.Voice, "1", 10, "hash", "Apache-2.0", "https://x/s.zip");

    private static InstalledVoicePack InstalledEnglish() =>
        new("/packs/supertonic-3", new PackManifest(PackConstants.SupertonicPackId, PackKind.Voice, "en", "1", "sherpa-onnx", [], VoiceEngineKind.Supertonic));

    [Fact]
    public async Task An_installed_pack_is_activated_without_downloading_anything()
    {
        var packs = new FakePacks { Installed = [InstalledEnglish()] };
        var model = new FakeModel();
        var setup = new VoicePackSetup(packs, model, new FakeSettings { Language = "en" });

        await setup.StartAsync();

        Assert.Equal(PackSetupState.Ready, setup.State);
        Assert.Equal(0, packs.DownloadCalls);
        Assert.Equal(1, model.Initialized);
    }

    [Fact]
    public async Task An_installed_pack_older_than_the_catalogs_is_downloaded_again_on_windows()
    {
        var oldFarsi = new InstalledVoicePack("/packs/piper-fa", new PackManifest("piper-fa", PackKind.Voice, "fa", "1", "sherpa-onnx", [], VoiceEngineKind.Vits, [new PackVoice("piper:fa-amir", "Amir", 0)]));
        var packs = new FakePacks { Installed = [oldFarsi], Catalog = new PackCatalogResult(PackDownloadResult.Ok, [FarsiPack with { Version = "2" }]) };
        var model = new FakeModel();
        var setup = new VoicePackSetup(packs, model, new FakeSettings { Language = "fa" });

        await setup.StartAsync();
        await setup.UpdateCompleted;

        Assert.Equal("piper-fa", packs.Downloaded);
        Assert.Equal(2, model.Initialized); // the installed pack, then the updated one
        Assert.Equal(PackSetupState.Ready, setup.State);
    }

    [Fact]
    public async Task A_failed_update_download_is_not_tried_again_for_the_same_version()
    {
        var oldFarsi = new InstalledVoicePack("/packs/piper-fa", new PackManifest("piper-fa", PackKind.Voice, "fa", "1", "sherpa-onnx", [], VoiceEngineKind.Vits, [new PackVoice("piper:fa-amir", "Amir", 0)]));
        var packs = new FakePacks { Installed = [oldFarsi], Catalog = new PackCatalogResult(PackDownloadResult.Ok, [FarsiPack with { Version = "2" }]), DownloadResult = PackDownloadResult.HashMismatch };
        var setup = new VoicePackSetup(packs, new FakeModel(), new FakeSettings { Language = "fa" });

        await setup.StartAsync();
        await setup.UpdateCompleted;
        await setup.StartAsync();
        await setup.UpdateCompleted;

        Assert.Equal(1, packs.DownloadCalls);
        Assert.Equal(PackSetupState.Ready, setup.State);
    }

    [Fact]
    public async Task An_offline_update_download_leaves_the_installed_pack_ready_and_is_tried_again_next_time()
    {
        var oldFarsi = new InstalledVoicePack("/packs/piper-fa", new PackManifest("piper-fa", PackKind.Voice, "fa", "1", "sherpa-onnx", [], VoiceEngineKind.Vits, [new PackVoice("piper:fa-amir", "Amir", 0)]));
        var packs = new FakePacks { Installed = [oldFarsi], Catalog = new PackCatalogResult(PackDownloadResult.Ok, [FarsiPack with { Version = "2" }]), DownloadResult = PackDownloadResult.Offline };
        var setup = new VoicePackSetup(packs, new FakeModel(), new FakeSettings { Language = "fa" });

        await setup.StartAsync();
        await setup.UpdateCompleted;
        Assert.Equal(PackSetupState.Ready, setup.State);

        await setup.StartAsync();
        await setup.UpdateCompleted;
        Assert.Equal(2, packs.DownloadCalls); // not remembered as failed: a network blip does not block the version
    }

    [Fact]
    public async Task The_update_runs_in_the_background_so_start_returns_while_it_downloads()
    {
        var oldFarsi = new InstalledVoicePack("/packs/piper-fa", new PackManifest("piper-fa", PackKind.Voice, "fa", "1", "sherpa-onnx", [], VoiceEngineKind.Vits, [new PackVoice("piper:fa-amir", "Amir", 0)]));
        var packs = new FakePacks { Installed = [oldFarsi], Catalog = new PackCatalogResult(PackDownloadResult.Ok, [FarsiPack with { Version = "2" }]) };
        Task<bool> cancelled = packs.BlockFirstDownload();
        var setup = new VoicePackSetup(packs, new FakeModel(), new FakeSettings { Language = "fa" });

        await setup.StartAsync();
        await packs.FirstDownloadStarted;

        Assert.False(setup.UpdateCompleted.IsCompleted); // the update is still downloading; StartAsync did not wait for it
        packs.ReleaseFirstDownload();
        await setup.UpdateCompleted;
        Assert.Equal(PackSetupState.Ready, setup.State);
        Assert.False(cancelled.IsCompleted); // it ended because it was released, not because it was cancelled
    }

    [Fact]
    public async Task A_second_start_during_an_update_does_not_start_a_second_download()
    {
        var oldFarsi = new InstalledVoicePack("/packs/piper-fa", new PackManifest("piper-fa", PackKind.Voice, "fa", "1", "sherpa-onnx", [], VoiceEngineKind.Vits, [new PackVoice("piper:fa-amir", "Amir", 0)]));
        var packs = new FakePacks { Installed = [oldFarsi], Catalog = new PackCatalogResult(PackDownloadResult.Ok, [FarsiPack with { Version = "2" }]) };
        packs.BlockFirstDownload();
        var setup = new VoicePackSetup(packs, new FakeModel(), new FakeSettings { Language = "fa" });
        await setup.StartAsync();
        await packs.FirstDownloadStarted;
        Task first = setup.UpdateCompleted;

        await setup.StartAsync(); // the second update finds one for the language registered and ends at once

        Assert.False(setup.UpdateCompleted.IsCompleted); // still holds the first
        Assert.Equal(1, packs.DownloadCalls);
        packs.ReleaseFirstDownload();
        await first;
        await setup.UpdateCompleted;
    }

    [Fact]
    public async Task A_run_for_another_language_cancels_the_update_in_flight()
    {
        var settings = new FakeSettings { Language = "fa" };
        var oldFarsi = new InstalledVoicePack("/packs/piper-fa", new PackManifest("piper-fa", PackKind.Voice, "fa", "1", "sherpa-onnx", [], VoiceEngineKind.Vits, [new PackVoice("piper:fa-amir", "Amir", 0)]));
        var packs = new FakePacks { Installed = [oldFarsi, InstalledEnglish()], Catalog = new PackCatalogResult(PackDownloadResult.Ok, [FarsiPack with { Version = "2" }]) };
        Task<bool> cancelled = packs.BlockFirstDownload();
        var setup = new VoicePackSetup(packs, new FakeModel(), settings);
        await setup.StartAsync();
        await packs.FirstDownloadStarted;

        settings.Language = "en";
        await setup.StartAsync();

        Assert.True(await cancelled);
        await setup.UpdateCompleted;
        Assert.Equal(PackSetupState.Ready, setup.State);
    }

    [Fact]
    public async Task An_offline_or_throwing_catalog_leaves_the_installed_pack_ready_on_windows()
    {
        var packs = new FakePacks { Installed = [InstalledEnglish()], CatalogThrows = true };
        var setup = new VoicePackSetup(packs, new FakeModel(), new FakeSettings { Language = "en" });

        await setup.StartAsync();

        Assert.Equal(PackSetupState.Ready, setup.State);
    }

    [Fact]
    public async Task A_missing_pack_is_downloaded_then_activated()
    {
        var packs = new FakePacks { Catalog = new PackCatalogResult(PackDownloadResult.Ok, [EnglishPack]) };
        var model = new FakeModel();
        var setup = new VoicePackSetup(packs, model, new FakeSettings { Language = "en" });
        var states = new List<PackSetupState>();
        setup.Changed += (_, _) => states.Add(setup.State);

        await setup.StartAsync();

        Assert.Equal([PackSetupState.Resolving, PackSetupState.Downloading, PackSetupState.Ready], states.Distinct());
        Assert.Equal(PackConstants.SupertonicPackId, packs.Downloaded);
        Assert.Equal(1, model.Initialized);
    }

    [Theory]
    [InlineData(PackDownloadResult.Offline, PackSetupState.Offline, true)]
    [InlineData(PackDownloadResult.HashMismatch, PackSetupState.HashMismatch, true)]
    [InlineData(PackDownloadResult.Failed, PackSetupState.Failed, true)]
    [InlineData(PackDownloadResult.InvalidPack, PackSetupState.InvalidPack, false)]
    public async Task A_failed_download_publishes_its_state_and_whether_retry_helps(
        PackDownloadResult outcome, PackSetupState expected, bool canRetry)
    {
        var packs = new FakePacks { Catalog = new PackCatalogResult(PackDownloadResult.Ok, [EnglishPack]), DownloadResult = outcome };
        var setup = new VoicePackSetup(packs, new FakeModel(), new FakeSettings { Language = "en" });

        await setup.StartAsync();

        Assert.Equal(expected, setup.State);
        Assert.Equal(canRetry, setup.CanRetry);
    }

    [Fact]
    public async Task An_offline_catalog_fetch_publishes_offline_and_retry_then_succeeds()
    {
        var packs = new FakePacks { Catalog = PackCatalogResult.Failure(PackDownloadResult.Offline) };
        var setup = new VoicePackSetup(packs, new FakeModel(), new FakeSettings { Language = "en" });
        await setup.StartAsync();
        Assert.Equal(PackSetupState.Offline, setup.State);

        packs.Catalog = new PackCatalogResult(PackDownloadResult.Ok, [EnglishPack]);
        await setup.StartAsync();

        Assert.Equal(PackSetupState.Ready, setup.State);
    }

    [Fact]
    public async Task A_progress_tick_after_the_download_failed_does_not_revive_downloading()
    {
        var packs = new FakePacks
        {
            Catalog = new PackCatalogResult(PackDownloadResult.Ok, [EnglishPack]),
            DownloadResult = PackDownloadResult.Offline,
        };
        var setup = new VoicePackSetup(packs, new FakeModel(), new FakeSettings { Language = "en" });
        await setup.StartAsync();

        packs.LastProgress!.Report(0.9);

        Assert.Equal(PackSetupState.Offline, setup.State);
    }

    [Fact]
    public async Task A_progress_tick_is_published_on_the_reporting_thread()
    {
        int reportThread = -1;
        var changedThreads = new List<int>();
        var packs = new FakePacks { Catalog = new PackCatalogResult(PackDownloadResult.Ok, [EnglishPack]) };
        packs.AfterProgressReport = () => reportThread = Environment.CurrentManagedThreadId;
        var setup = new VoicePackSetup(packs, new FakeModel(), new FakeSettings { Language = "en" });
        setup.Changed += (_, _) =>
        {
            if (setup.Progress > 0)
            {
                changedThreads.Add(Environment.CurrentManagedThreadId);
            }
        };

        await setup.StartAsync();

        Assert.Equal([reportThread], changedThreads);
    }

    [Fact]
    public async Task A_language_with_no_pack_is_unavailable_so_the_windows_voice_reads()
    {
        var packs = new FakePacks { Catalog = new PackCatalogResult(PackDownloadResult.Ok, [EnglishPack]) };
        var setup = new VoicePackSetup(packs, new FakeModel(), new FakeSettings { Language = "fa" });

        await setup.StartAsync();

        Assert.Equal(PackSetupState.Unavailable, setup.State);
        Assert.False(setup.CanRetry);
    }

    [Fact]
    public async Task With_no_language_chosen_and_nothing_installed_nothing_happens()
    {
        var packs = new FakePacks();
        var setup = new VoicePackSetup(packs, new FakeModel(), new FakeSettings());

        await setup.StartAsync();

        Assert.Equal(PackSetupState.Idle, setup.State);
        Assert.Equal(0, packs.CatalogCalls);
    }

    [Fact]
    public async Task A_language_chosen_while_a_run_is_in_flight_is_served_before_start_returns()
    {
        var settings = new FakeSettings { Language = "en" };
        var packs = new FakePacks { Catalog = new PackCatalogResult(PackDownloadResult.Ok, [EnglishPack]) };
        Task? dropped = null;
        var setup = new VoicePackSetup(packs, new FakeModel(), settings);
        packs.OnDownload = () =>
        {
            settings.Language = "fa";
            dropped = setup.StartAsync(); // arrives mid-run: re-publishes, and the running call serves "fa"
        };

        await setup.StartAsync();

        Assert.NotNull(dropped);
        await dropped;
        Assert.Equal(2, packs.CatalogCalls); // "en" resolved, then "fa" resolved: nothing was dropped
        Assert.Equal(PackSetupState.Unavailable, setup.State);
    }

    [Fact]
    public async Task A_language_change_while_idle_starts_a_resolve_for_the_new_language()
    {
        var settings = new FakeSettings { Language = "en" };
        var packs = new FakePacks { Catalog = new PackCatalogResult(PackDownloadResult.Ok, [EnglishPack]) };
        var notifier = new FakeNotifier();
        var installer = MobileInstaller(packs, settings, notifier);

        settings.Language = "fa";
        notifier.Raise();
        await WaitForStateAsync(installer, PackInstallState.Unavailable);

        Assert.Equal(1, packs.CatalogCalls);
    }

    [Fact]
    public async Task A_language_change_during_a_run_re_runs_once_after_it()
    {
        var settings = new FakeSettings { Language = "en" };
        var packs = new FakePacks { Catalog = new PackCatalogResult(PackDownloadResult.Ok, [EnglishPack]) };
        var notifier = new FakeNotifier();
        var installer = MobileInstaller(packs, settings, notifier);
        packs.OnDownload = () =>
        {
            settings.Language = "fa";
            notifier.Raise(); // arrives mid-run: the running call serves "fa" afterwards
        };

        await installer.StartAsync();

        Assert.Equal(2, packs.CatalogCalls);
        Assert.Equal(PackInstallState.Unavailable, installer.State);
    }

    private static readonly PackInfo FarsiPack =
        new("piper-fa", ["fa"], PackKind.Voice, "1", 10, "hash", "MIT", "https://x/fa.zip");

    [Fact]
    public async Task An_installed_pack_older_than_the_catalogs_is_downloaded_again()
    {
        var settings = new FakeSettings { Language = "fa" };
        var oldFarsi = new InstalledVoicePack("/packs/piper-fa", new PackManifest("piper-fa", PackKind.Voice, "fa", "1", "sherpa-onnx", [], VoiceEngineKind.Vits, [new PackVoice("piper:fa-amir", "Amir", 0)]));
        var packs = new FakePacks
        {
            Installed = [oldFarsi],
            Catalog = new PackCatalogResult(PackDownloadResult.Ok, [FarsiPack with { Version = "2" }]),
        };
        var model = new FakeModel();
        var installer = new VoicePackInstaller(packs, model, new NoopReader(), settings, new NeuralPlatform(), new FakeNetwork(), new FakeNotifier());

        await installer.StartAsync();
        await WaitUntilAsync(() => model.Initialized == 2); // the installed pack, then the updated one

        Assert.Equal("piper-fa", packs.Downloaded);
        await WaitForStateAsync(installer, PackInstallState.Ready);
    }

    [Fact]
    public async Task An_installed_pack_is_not_updated_over_a_metered_connection()
    {
        var settings = new FakeSettings { Language = "fa" };
        var oldFarsi = new InstalledVoicePack("/packs/piper-fa", new PackManifest("piper-fa", PackKind.Voice, "fa", "1", "sherpa-onnx", [], VoiceEngineKind.Vits, [new PackVoice("piper:fa-amir", "Amir", 0)]));
        var packs = new FakePacks { Installed = [oldFarsi], Catalog = new PackCatalogResult(PackDownloadResult.Ok, [FarsiPack with { Version = "2" }]) };
        var installer = new VoicePackInstaller(packs, new FakeModel(), new NoopReader(), settings, new NeuralPlatform(), new FakeNetwork { Allows = false }, new FakeNotifier());

        await installer.StartAsync();
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Equal(0, packs.DownloadCalls);
        Assert.Equal(PackInstallState.Ready, installer.State);
    }

    [Fact]
    public async Task A_first_install_goes_ahead_over_a_metered_connection()
    {
        var settings = new FakeSettings { Language = "en" };
        var packs = new FakePacks { Catalog = new PackCatalogResult(PackDownloadResult.Ok, [EnglishPack]) };
        var installer = new VoicePackInstaller(packs, new FakeModel(), new NoopReader(), settings, new NeuralPlatform(), new FakeNetwork { Allows = false }, new FakeNotifier());

        await installer.StartAsync();

        Assert.Equal(1, packs.DownloadCalls);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    private sealed class FakeNetwork : IPackUpdateNetwork
    {
        public bool Allows { get; init; } = true;

        public bool AllowsAutomaticUpdate => Allows;
    }

    [Fact]
    public async Task An_installed_pack_that_is_current_or_unlisted_is_not_downloaded_again()
    {
        var settings = new FakeSettings { Language = "en" };
        var packs = new FakePacks { Installed = [InstalledEnglish()], Catalog = new PackCatalogResult(PackDownloadResult.Ok, [EnglishPack]) };
        var installer = new VoicePackInstaller(packs, new FakeModel(), new NoopReader(), settings, new NeuralPlatform(), new FakeNetwork(), new FakeNotifier());

        await installer.StartAsync();

        Assert.Equal(0, packs.DownloadCalls);
        Assert.Equal(PackInstallState.Ready, installer.State);
    }

    [Fact]
    public async Task An_offline_update_check_leaves_the_installed_pack_ready()
    {
        var settings = new FakeSettings { Language = "en" };
        var packs = new FakePacks { Installed = [InstalledEnglish()], CatalogThrows = true };
        var installer = new VoicePackInstaller(packs, new FakeModel(), new NoopReader(), settings, new NeuralPlatform(), new FakeNetwork(), new FakeNotifier());

        await installer.StartAsync();

        Assert.Equal(PackInstallState.Ready, installer.State);
    }

    [Fact]
    public async Task An_error_in_an_event_started_run_ends_in_the_failed_state()
    {
        var settings = new FakeSettings { Language = "en" };
        var packs = new FakePacks { CatalogThrows = true };
        var notifier = new FakeNotifier();
        var installer = MobileInstaller(packs, settings, notifier);

        settings.Language = "fa";
        notifier.Raise();
        await WaitForStateAsync(installer, PackInstallState.Failed);

        Assert.True(installer.CanRetry);
    }

    [Fact]
    public async Task A_change_to_an_installed_language_cancels_the_download_in_flight()
    {
        var settings = new FakeSettings { Language = "fa" };
        var packs = new FakePacks { Catalog = new PackCatalogResult(PackDownloadResult.Ok, [FarsiPack]) };
        var model = new FakeModel();
        var notifier = new FakeNotifier();
        var installer = new VoicePackInstaller(packs, model, new NoopReader(), settings, new NeuralPlatform(), new FakeNetwork(), notifier);
        Task<bool> cancelled = packs.BlockFirstDownload();

        Task run = installer.StartAsync();
        await packs.FirstDownloadStarted;
        packs.Installed = [InstalledEnglish()];
        settings.Language = "en";
        notifier.Raise();
        await run;

        Assert.True(await cancelled);
        Assert.Equal(PackInstallState.Ready, installer.State);
        Assert.Equal(1, model.Initialized);
        Assert.Equal(1, packs.DownloadCalls);
    }

    [Fact]
    public async Task Flipping_back_to_the_original_language_after_a_cancel_downloads_it_again()
    {
        var settings = new FakeSettings { Language = "fa" };
        var packs = new FakePacks { Catalog = new PackCatalogResult(PackDownloadResult.Ok, [EnglishPack, FarsiPack]) };
        var notifier = new FakeNotifier();
        var installer = MobileInstaller(packs, settings, notifier);
        Task<bool> cancelled = packs.BlockFirstDownload();

        Task run = installer.StartAsync();
        await packs.FirstDownloadStarted;
        settings.Language = "en";
        notifier.Raise();
        settings.Language = "fa";
        notifier.Raise();
        await run;

        Assert.True(await cancelled);
        Assert.Equal("piper-fa", packs.Downloaded);
        Assert.Equal(PackInstallState.Ready, installer.State);
    }

    [Fact]
    public async Task A_language_change_while_a_run_throws_is_still_served()
    {
        var settings = new FakeSettings { Language = "fa" };
        var packs = new FakePacks { CatalogThrows = true };
        var notifier = new FakeNotifier();
        var installer = MobileInstaller(packs, settings, notifier);
        packs.OnCatalog = () =>
        {
            packs.Installed = [InstalledEnglish()];
            settings.Language = "en";
            notifier.Raise();
        };

        await installer.StartAsync();

        Assert.Equal(PackInstallState.Ready, installer.State);
    }

    [Fact]
    public async Task Clearing_the_language_during_a_download_ends_in_idle()
    {
        var settings = new FakeSettings { Language = "fa" };
        var packs = new FakePacks { Catalog = new PackCatalogResult(PackDownloadResult.Ok, [FarsiPack]) };
        var notifier = new FakeNotifier();
        var installer = MobileInstaller(packs, settings, notifier);
        Task<bool> cancelled = packs.BlockFirstDownload();

        Task run = installer.StartAsync();
        await packs.FirstDownloadStarted;
        settings.Language = null;
        notifier.Raise();
        await run;

        Assert.True(await cancelled);
        Assert.Equal(PackInstallState.Idle, installer.State);
    }

    [Fact]
    public async Task Rapid_language_flips_never_throw_and_settle_on_the_last_language()
    {
        var settings = new FakeSettings { Language = "fa" };
        var packs = new FakePacks { Catalog = new PackCatalogResult(PackDownloadResult.Ok, [EnglishPack, FarsiPack]) };
        var notifier = new FakeNotifier();
        var installer = MobileInstaller(packs, settings, notifier);
        Task<bool> cancelled = packs.BlockFirstDownload();

        Task run = installer.StartAsync();
        await packs.FirstDownloadStarted;
        for (int i = 0; i < 200; i++)
        {
            settings.Language = i % 2 == 0 ? "en" : "fa";
            notifier.Raise();
        }

        settings.Language = "fa";
        notifier.Raise();
        await run;

        Assert.True(await cancelled);
        await WaitForStateAsync(installer, PackInstallState.Ready);
    }

    private static VoicePackInstaller MobileInstaller(FakePacks packs, FakeSettings settings, FakeNotifier notifier) =>
        new(packs, new FakeModel(), new NoopReader(), settings, new NeuralPlatform(), new FakeNetwork(), notifier);

    // Completes on the Changed event that reaches the state, so no clock decides the outcome.
    private static Task WaitForStateAsync(VoicePackInstaller installer, PackInstallState expected)
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        installer.Changed += (_, _) =>
        {
            if (installer.State == expected)
            {
                reached.TrySetResult();
            }
        };

        if (installer.State == expected)
        {
            reached.TrySetResult();
        }

        return AwaitWithinLimitAsync(installer, expected, reached.Task);
    }

    private static readonly TimeSpan StateWaitLimit = TimeSpan.FromSeconds(30);

    private static async Task AwaitWithinLimitAsync(VoicePackInstaller installer, PackInstallState expected, Task reached)
    {
        if (await Task.WhenAny(reached, Task.Delay(StateWaitLimit)) != reached)
        {
            Assert.Fail($"Expected installer state {expected} within {StateWaitLimit.TotalSeconds}s, last seen {installer.State}.");
        }
    }

    private sealed class FakeNotifier : ILanguageChangeNotifier
    {
        public event EventHandler? LanguageChanged;

        public void Raise() => LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class NeuralPlatform : IPlatformServices
    {
        public bool SupportsNeuralVoice => true;
        public bool SupportsTextScan => false;
        public ISpeechReader CreateNeuralReader() => new NoopReader();
        public ISpeechReader CreateFallbackReader() => new NoopReader();
    }

    private sealed class NoopReader : ISpeechReader
    {
        public event EventHandler<PlaybackState>? StateChanged { add { } remove { } }
        public event EventHandler? Completed { add { } remove { } }
        public event EventHandler<double>? ProgressChanged { add { } remove { } }
        public event EventHandler<ReadTiming>? TimingChanged { add { } remove { } }
        public event EventHandler<ReadChunk>? ChunkChanged { add { } remove { } }
        public PlaybackState State => PlaybackState.Idle;
        public Task SpeakAsync(string text, int? activityId = null) => Task.CompletedTask;
        public Task WarmUpAsync() => Task.CompletedTask;
        public void Pause() { }
        public void Resume() { }
        public void Stop() { }
        public void SetSpeed(PlaybackRate speed) { }
        public void SetVoice(string voiceId) { }
        public Task SkipForward() => Task.CompletedTask;
        public Task SkipBackward() => Task.CompletedTask;
    }

    private sealed class FakePacks : IVoicePackService
    {
        public Action? OnDownload { get; set; }
        public Action? OnCatalog { get; set; }
        public Action? AfterProgressReport { get; set; }
        public IReadOnlyList<InstalledVoicePack> Installed { get; set; } = [];
        public PackCatalogResult Catalog { get; set; } = PackCatalogResult.Failure(PackDownloadResult.Failed);
        public PackDownloadResult DownloadResult { get; set; } = PackDownloadResult.Ok;
        public int CatalogCalls { get; private set; }
        public int DownloadCalls { get; private set; }
        public bool CatalogThrows { get; set; }
        public Task FirstDownloadStarted => _firstStarted.Task;
        private readonly TaskCompletionSource _firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _blockFirst;
        private TaskCompletionSource<bool>? _firstCancelled;

        /// <summary>The first download waits for cancellation; the result says whether it was cancelled.</summary>
        public Task<bool> BlockFirstDownload()
        {
            _blockFirst = true;
            _firstCancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _firstCancelled.Task;
        }
        public void ReleaseFirstDownload() => _releaseFirst.Cancel();

        private readonly CancellationTokenSource _releaseFirst = new();
        public string? Downloaded { get; private set; }
        public IProgress<double>? LastProgress { get; private set; }

        public Task<PackCatalogResult> GetCatalogAsync(CancellationToken cancellationToken)
        {
            CatalogCalls++;
            OnCatalog?.Invoke();
            return CatalogThrows ? throw new InvalidOperationException("catalog") : Task.FromResult(Catalog);
        }

        public string? GetInstalledPath(string packId) => null;

        public IReadOnlyList<InstalledVoicePack> GetInstalledVoicePacks() => Installed;
        public IReadOnlyList<InstalledVoicePack> GetInstalledOcrPacks() => [];
        public Task<PackCatalogResult> GetCatalogCachedAsync(TimeSpan maxAge, CancellationToken cancellationToken) => GetCatalogAsync(cancellationToken);

        public Task<PackDownloadResult> DownloadAsync(string packId, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            Downloaded = packId;
            DownloadCalls++;
            if (_blockFirst)
            {
                _blockFirst = false;
                return WaitForCancelAsync(cancellationToken);
            }

            OnDownload?.Invoke();
            LastProgress = progress;
            Task.Run(() =>
            {
                progress?.Report(0.5);
                AfterProgressReport?.Invoke();
            }).GetAwaiter().GetResult();
            return Task.FromResult(DownloadResult);
        }

        private async Task<PackDownloadResult> WaitForCancelAsync(CancellationToken cancellationToken)
        {
            _firstStarted.TrySetResult();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _releaseFirst.Token);
            try
            {
                await Task.Delay(Timeout.Infinite, linked.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _firstCancelled!.TrySetResult(true);
                throw;
            }
            catch (OperationCanceledException)
            {
                // Released by the test: the download ends on its own.
            }

            return PackDownloadResult.Offline;
        }
    }

    private sealed class FakeModel : IVoiceModelService
    {
        public int Initialized { get; private set; }
        public bool IsReady => Initialized > 0;
        public VoiceModelPaths? Paths => null;
        public event EventHandler? ReadyChanged { add { } remove { } }

        public Task InitializeAsync(IProgress<double>? progress = null)
        {
            Initialized++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettings : ISettingsStore
    {
        public PlaybackRate Speed { get; set; } = PlaybackRate.Default;
        public bool AutoReadOnSelection { get; set; }
        public bool AutoReadOnCopy { get; set; }
        public bool TranslateEnabled { get; set; }
        public string? TranslateSourceLanguage { get; set; }
        public string? VoiceId { get; set; }
        public SanitizerCategory EnabledSanitizers { get; set; }
        public PanelPosition? PanelPosition { get; set; }
        public string? Language { get; set; }
    }
}
