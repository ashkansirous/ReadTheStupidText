using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Locates the neural voice model for the user's language on Android (Slice 50, Decision 61;
/// per-language engines in Slice 51, Decision 63). No model ships in the package: each
/// arrives as a downloaded voice pack that <see cref="IVoicePackService"/> installs under
/// app-local storage. This service picks the installed pack that serves the stored language
/// (<see cref="InstalledVoiceCatalog"/>: language -> pack -> engine) and raises
/// <see cref="ReadyChanged"/> so the speech reader switches from the phone voice to the
/// neural one. With no installed pack for the language it stays not-ready and the phone voice
/// reads. The resolved model is cached: scanning the packs touches the file system, so it runs
/// on a background thread (<see cref="InitializeAsync"/>, or on demand when the stored language
/// no longer matches the cache) and <see cref="Paths"/> only ever returns the cached value —
/// null while a refresh is pending, so a read in that gap uses the phone voice (and one that
/// chose the neural reader just before the model changed falls back to it, see
/// <see cref="NeuralEngineUnavailableException"/>). A language change starts the scan at once. Every change of
/// the resolved model (readiness, pack or language) raises <see cref="ReadyChanged"/>. <see cref="ModelForAsync"/>
/// (Slice 90) resolves another language's model for one read from the cached scan and returns it, leaving this
/// service on the app language.
/// </summary>
public sealed class MobileVoiceModelService : IVoiceModelService, IReadingModelResolver
{
    private readonly IVoicePackService _packs;
    private readonly ISettingsStore _settings;
    private readonly object _gate = new();
    private string? _resolvedFor;
    private VoiceModelPaths? _paths;
    private IReadOnlyList<InstalledVoicePack>? _installed;
    private readonly Dictionary<string, VoiceModelPaths?> _byLanguage = [];
    private int _refreshing;
    private int _rerunRequested;

    private readonly bool _storyteller;

    // storyteller is registered by Dino Reader only (Slice 114): with it the storyteller packs take part in the scan.
    public MobileVoiceModelService(IVoicePackService packs, ISettingsStore settings, ILanguageChangeNotifier languages, StorytellerVoices? storyteller = null)
    {
        _storyteller = storyteller is not null;
        _packs = packs;
        _settings = settings;
        languages.LanguageChanged += (_, _) => RefreshInBackground();

        // Slice 90: an install or replacement drops the per-language cache with the app-language model.
        packs.InstalledPacksChanged += (_, _) => RefreshInBackground();
    }

    public bool IsReady => Paths is not null;

    public VoiceModelPaths? Paths
    {
        get
        {
            lock (_gate)
            {
                if (_resolvedFor == CurrentLanguage)
                {
                    return _paths;
                }
            }

            RefreshInBackground();
            return null;
        }
    }

    public event EventHandler? ReadyChanged;

    // Safe to call again after a pack finishes downloading: re-scans the installed packs off
    // the calling thread and raises ReadyChanged if the resolved model changed.
    public Task InitializeAsync(IProgress<double>? progress = null) => Task.Run(Refresh);

    // An unset language reads as English: the first-run page always sets one, and Dino
    // Reader sets English at startup.
    private string CurrentLanguage => _settings.Language ?? SupertonicVoiceTable.EnglishCode;

    // Slice 90: the model for the language of one read, returned to the caller. Nothing shared changes
    // (Paths, IsReady and ReadyChanged stay on the app language). It resolves against the pack scan the last
    // refresh cached, so a read costs no disk scan; the first call scans once.
    public Task<VoiceModelPaths?> ModelForAsync(string language, CancellationToken cancellationToken = default) =>
        Task.Run(() => ModelFor(VoiceAvailability.Primary(language)), cancellationToken);

    private VoiceModelPaths? ModelFor(string language)
    {
        lock (_gate)
        {
            if (_installed is { } cached)
            {
                return ResolveCached(cached, language);
            }
        }

        IReadOnlyList<InstalledVoicePack> scanned = ScanInstalledPacks();
        lock (_gate)
        {
            _installed ??= scanned;
            return ResolveCached(_installed, language);
        }
    }

    // Called under _gate. One instance per language until the next scan, so an engine keyed on the model
    // is not rebuilt for the same pack.
    private VoiceModelPaths? ResolveCached(IReadOnlyList<InstalledVoicePack> installed, string language)
    {
        if (!_byLanguage.TryGetValue(language, out VoiceModelPaths? model))
        {
            model = InstalledVoiceCatalog.Resolve(installed, language);
            _byLanguage[language] = model;
        }

        return model;
    }

    // One refresh at a time. A request that arrives while one runs (a language change) is
    // never dropped: the running refresh loops until the cache matches the current language.
    private void RefreshInBackground()
    {
        if (Interlocked.Exchange(ref _refreshing, 1) == 1)
        {
            Volatile.Write(ref _rerunRequested, 1);
            return;
        }

        _ = Task.Run(RefreshUntilCurrent);
    }

    private void RefreshUntilCurrent()
    {
        try
        {
            do
            {
                Volatile.Write(ref _rerunRequested, 0);
                Refresh();
            }
            while (!IsCurrent() || Volatile.Read(ref _rerunRequested) == 1);
        }
        finally
        {
            Volatile.Write(ref _refreshing, 0);
        }

        // A request between the last check and the flag reset would otherwise be lost.
        if (!IsCurrent())
        {
            RefreshInBackground();
        }
    }

    private bool IsCurrent()
    {
        lock (_gate)
        {
            return _resolvedFor == CurrentLanguage;
        }
    }

    private void Refresh()
    {
        string language = CurrentLanguage;
        IReadOnlyList<InstalledVoicePack> installed = ScanInstalledPacks();
        VoiceModelPaths? resolved = InstalledVoiceCatalog.Resolve(installed, language);
        bool changed;
        lock (_gate)
        {
            _installed = installed;
            _byLanguage.Clear();
            changed = !SameModel(_paths, resolved);
            _resolvedFor = language;
            _paths = changed ? resolved : _paths;
        }

        if (changed)
        {
            ReadyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private IReadOnlyList<InstalledVoicePack> ScanInstalledPacks()
    {
        try
        {
            return _storyteller ? _packs.GetInstalledVoiceAndStorytellerPacks() : _packs.GetInstalledVoicePacks();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    // Readiness plus the resolved pack folder, engine, language and voices.
    private static bool SameModel(VoiceModelPaths? a, VoiceModelPaths? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return a.RootDir == b.RootDir
            && a.Kind == b.Kind
            && a.Language == b.Language
            && a.AvailableVoices.Select(v => v.Id).SequenceEqual(b.AvailableVoices.Select(v => v.Id));
    }
}
