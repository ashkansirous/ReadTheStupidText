using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Domain.Sanitizing;
using ReadTheStupidText.Infrastructure.Reading;

namespace ReadTheStupidText.Tests;

public class PackVoiceModelServiceTests
{
    private static InstalledVoicePack Pack(string id, string languages, VoiceEngineKind kind, params PackVoice[] voices) =>
        PackVersion(id, languages, kind, "1", voices);

    private static InstalledVoicePack PackVersion(string id, string languages, VoiceEngineKind kind, string version, params PackVoice[] voices) =>
        new($"/packs/{id}", new PackManifest(id, PackKind.Voice, languages, version, "sherpa-onnx", [], kind, voices));

    private static readonly InstalledVoicePack Supertonic =
        Pack(PackConstants.SupertonicPackId, "en,fr", VoiceEngineKind.Supertonic);

    private static readonly InstalledVoicePack Persian =
        Pack(PackConstants.PersianPackId, "fa", VoiceEngineKind.Vits, new PackVoice("piper:fa-ganji-adabi", "Ganji Adabi", 0));

    [Fact]
    public async Task Initialize_resolves_the_pack_for_the_stored_language_and_raises_ready()
    {
        var settings = new FakeSettings { Language = "fr" };
        var service = new PackVoiceModelService(new FakePacks([Supertonic, Persian]), settings, settings);
        int raised = 0;
        service.ReadyChanged += (_, _) => raised++;

        await service.InitializeAsync();

        Assert.True(service.IsReady);
        Assert.Equal("fr", service.Paths!.Language);
        Assert.Equal(VoiceEngineKind.Supertonic, service.Paths.Kind);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task A_language_with_no_installed_pack_stays_not_ready_so_the_windows_voice_reads()
    {
        var settings = new FakeSettings { Language = "fa" };
        var service = new PackVoiceModelService(new FakePacks([Supertonic]), settings, settings);

        await service.InitializeAsync();

        Assert.False(service.IsReady);
        Assert.Null(service.Paths);
    }

    [Fact]
    public async Task Changing_the_language_moves_to_the_other_pack()
    {
        var settings = new FakeSettings { Language = "en" };
        var service = new PackVoiceModelService(new FakePacks([Supertonic, Persian]), settings, settings);
        await service.InitializeAsync();
        var changed = new TaskCompletionSource();
        service.ReadyChanged += (_, _) => changed.TrySetResult();

        settings.Language = "fa";

        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(VoiceEngineKind.Vits, service.Paths!.Kind);
    }

    [Fact]
    public async Task An_unset_language_reads_as_english()
    {
        var settings = new FakeSettings();
        var service = new PackVoiceModelService(new FakePacks([Supertonic]), settings, settings);

        await service.InitializeAsync();

        Assert.Equal("en", service.Paths!.Language);
    }

    [Fact]
    public async Task The_catalog_lists_the_pack_voices_then_the_windows_voice()
    {
        var settings = new FakeSettings { Language = "fa" };
        var service = new PackVoiceModelService(new FakePacks([Persian]), settings, settings);
        await service.InitializeAsync();
        var catalog = new PackVoiceCatalog(service);

        Assert.Equal(["piper:fa-ganji-adabi", SystemVoice.Id], catalog.InstalledVoices.Select(v => v.Id));
        Assert.Equal("piper:fa-ganji-adabi", catalog.DefaultVoice!.Id);
    }

    [Fact]
    public async Task A_pack_replaced_in_place_by_a_newer_version_raises_ready_changed()
    {
        var settings = new FakeSettings { Language = "fa" };
        var packs = new FakePacks([Persian]);
        var service = new PackVoiceModelService(packs, settings, settings);
        await service.InitializeAsync();
        int raised = 0;
        service.ReadyChanged += (_, _) => raised++;

        packs.Installed = [PackVersion(PackConstants.PersianPackId, "fa", VoiceEngineKind.Vits, "2", new PackVoice("piper:fa-ganji-adabi", "Ganji Adabi", 0))];
        await service.InitializeAsync();

        Assert.Equal(1, raised);
        Assert.Equal("2", service.Paths!.PackVersion);
    }

    [Fact]
    public void Before_any_pack_the_windows_voice_is_the_only_and_default_voice()
    {
        var settings = new FakeSettings { Language = "en" };
        var catalog = new PackVoiceCatalog(new PackVoiceModelService(new FakePacks([]), settings, settings));

        VoiceInfo only = Assert.Single(catalog.InstalledVoices);
        Assert.Equal(SystemVoice.Id, only.Id);
        Assert.Equal(PackVoiceCatalog.WindowsVoiceName, only.DisplayName);
        Assert.Equal(SystemVoice.Id, catalog.DefaultVoice!.Id);
    }

    private sealed class FakePacks(IReadOnlyList<InstalledVoicePack> installed) : IVoicePackService
    {
        public IReadOnlyList<InstalledVoicePack> Installed { get; set; } = installed;

        public Task<PackCatalogResult> GetCatalogAsync(CancellationToken cancellationToken) =>
            Task.FromResult(PackCatalogResult.Failure(PackDownloadResult.Failed));

        public string? GetInstalledPath(string packId) => null;

        public IReadOnlyList<InstalledVoicePack> GetInstalledVoicePacks() => Installed;
        public IReadOnlyList<InstalledVoicePack> GetInstalledOcrPacks() => [];
        public Task<PackCatalogResult> GetCatalogCachedAsync(TimeSpan maxAge, CancellationToken cancellationToken) => GetCatalogAsync(cancellationToken);

        public Task<PackDownloadResult> DownloadAsync(string packId, IProgress<double>? progress, CancellationToken cancellationToken) =>
            Task.FromResult(PackDownloadResult.Failed);
    }

    private sealed class FakeSettings : ISettingsStore, ILanguageChangeNotifier
    {
        private string? _language;

        public event EventHandler? LanguageChanged;

        public string? Language
        {
            get => _language;
            set
            {
                _language = value;
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public PlaybackRate Speed { get; set; } = PlaybackRate.Default;
        public bool AutoReadOnSelection { get; set; }
        public bool AutoReadOnCopy { get; set; }
        public bool TranslateEnabled { get; set; }
        public string? TranslateSourceLanguage { get; set; }
        public string? VoiceId { get; set; }
        public SanitizerCategory EnabledSanitizers { get; set; }
        public PanelPosition? PanelPosition { get; set; }
    }
}
