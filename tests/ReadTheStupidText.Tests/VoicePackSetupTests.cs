using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Domain.Sanitizing;
using ReadTheStupidText.Infrastructure.Reading;

namespace ReadTheStupidText.Tests;

public class VoicePackSetupTests
{
    private static readonly PackInfo EnglishPack =
        new(PackConstants.SupertonicPackId, ["en"], PackKind.Voice, "1", 10, "hash", "Apache-2.0", "https://x/s.zip");

    private static InstalledVoicePack InstalledEnglish() =>
        new("/packs/supertonic-3", new PackManifest(PackConstants.SupertonicPackId, PackKind.Voice, "en", "1", "sherpa-onnx", [], VoiceEngineKind.Supertonic));

    [Fact]
    public async Task An_installed_pack_is_activated_without_touching_the_network()
    {
        var packs = new FakePacks { Installed = [InstalledEnglish()] };
        var model = new FakeModel();
        var setup = new VoicePackSetup(packs, model, new FakeSettings { Language = "en" });

        await setup.StartAsync();

        Assert.Equal(PackSetupState.Ready, setup.State);
        Assert.Equal(0, packs.CatalogCalls);
        Assert.Equal(1, model.Initialized);
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

    private sealed class FakePacks : IVoicePackService
    {
        public Action? OnDownload { get; set; }
        public IReadOnlyList<InstalledVoicePack> Installed { get; set; } = [];
        public PackCatalogResult Catalog { get; set; } = PackCatalogResult.Failure(PackDownloadResult.Failed);
        public PackDownloadResult DownloadResult { get; set; } = PackDownloadResult.Ok;
        public int CatalogCalls { get; private set; }
        public string? Downloaded { get; private set; }

        public Task<PackCatalogResult> GetCatalogAsync(CancellationToken cancellationToken)
        {
            CatalogCalls++;
            return Task.FromResult(Catalog);
        }

        public string? GetInstalledPath(string packId) => null;

        public IReadOnlyList<InstalledVoicePack> GetInstalledVoicePacks() => Installed;

        public Task<PackDownloadResult> DownloadAsync(string packId, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            Downloaded = packId;
            OnDownload?.Invoke();
            progress?.Report(0.5);
            return Task.FromResult(DownloadResult);
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
        public string? VoiceId { get; set; }
        public SanitizerCategory EnabledSanitizers { get; set; }
        public PanelPosition? PanelPosition { get; set; }
        public string? Language { get; set; }
    }
}
