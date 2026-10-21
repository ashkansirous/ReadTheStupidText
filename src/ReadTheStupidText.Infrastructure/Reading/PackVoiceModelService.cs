using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Locates the neural voice model for the user's language on Windows (Slice 56, Decision 61): a port
/// of the Android <c>MobileVoiceModelService</c>. No model ships in the package; each arrives as a
/// downloaded voice pack that <see cref="IVoicePackService"/> installs under the packs folder. This
/// service picks the installed pack that serves the stored language (<see cref="InstalledVoiceCatalog"/>:
/// language -> pack -> engine) and raises <see cref="ReadyChanged"/> so the speech reader switches from the
/// Windows voice to the neural one. With no installed pack for the language it stays not-ready and the
/// Windows voice reads. The resolved model is cached: scanning the packs touches the file system, so it
/// runs on a background thread (<see cref="InitializeAsync"/>, or on demand when the stored language no
/// longer matches the cache) and <see cref="Paths"/> only ever returns the cached value, null while a
/// refresh is pending. A language change starts the scan at once.
/// </summary>
public sealed class PackVoiceModelService : IVoiceModelService
{
    private readonly IVoicePackService _packs;
    private readonly ISettingsStore _settings;
    private readonly object _gate = new();
    private string? _resolvedFor;
    private VoiceModelPaths? _paths;
    private int _refreshing;
    private int _rerunRequested;

    public PackVoiceModelService(IVoicePackService packs, ISettingsStore settings, ILanguageChangeNotifier languages)
    {
        _packs = packs;
        _settings = settings;
        languages.LanguageChanged += (_, _) => RefreshInBackground();
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

    // An unset language reads as English (before the first-run window has been answered).
    private string CurrentLanguage => _settings.Language ?? SupertonicVoiceTable.EnglishCode;

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
        VoiceModelPaths? resolved = InstalledVoiceCatalog.Resolve(ScanInstalledPacks(), language);
        bool changed;
        lock (_gate)
        {
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
            return _packs.GetInstalledVoicePacks();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    // Readiness plus the resolved pack folder, version, engine, language and the full voice entries,
    // so a pack replaced in place by a newer version (same folder) still raises ReadyChanged.
    private static bool SameModel(VoiceModelPaths? a, VoiceModelPaths? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return a.RootDir == b.RootDir
            && a.Kind == b.Kind
            && a.Language == b.Language
            && a.PackVersion == b.PackVersion
            && a.AvailableVoices.SequenceEqual(b.AvailableVoices);
    }
}
