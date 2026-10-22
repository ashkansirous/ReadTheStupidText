using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>What the first-run language window and the control panel show about the voice pack.</summary>
public enum PackSetupState
{
    Idle,
    Resolving,
    Downloading,
    Ready,
    Unavailable,
    Offline,
    HashMismatch,
    InvalidPack,
    Failed,
}

/// <summary>
/// Runs "chosen language -> catalog -> voice pack -> natural voice" once at a time and publishes
/// its state, so the language window and the control panel show the same progress while the
/// Windows voice keeps reading (Slice 56, Decision 61). A singleton: the download outlives the
/// window that started it. <see cref="Changed"/> can fire on any thread; the UI marshals itself.
/// Warm-up of the engine follows <see cref="IVoiceModelService.ReadyChanged"/>, handled by
/// <see cref="ReadAloudService"/>, so this class never touches the reader.
/// </summary>
public sealed class VoicePackSetup
{
    private readonly IVoicePackService _packs;
    private readonly object _stateLock = new();
    private readonly IVoiceModelService _model;
    private readonly ISettingsStore _settings;
    private int _running;

    public VoicePackSetup(IVoicePackService packs, IVoiceModelService model, ISettingsStore settings)
    {
        _packs = packs;
        _model = model;
        _settings = settings;
    }

    public PackSetupState State { get; private set; } = PackSetupState.Idle;

    /// <summary>Download progress 0..1 while <see cref="State"/> is Downloading.</summary>
    public double Progress { get; private set; }

    public event EventHandler? Changed;

    public bool CanRetry => State is PackSetupState.Offline or PackSetupState.HashMismatch or PackSetupState.Failed;

    /// <summary>
    /// An installed pack serving the language is activated with no network request; a missing one
    /// is downloaded. A call while a run is in flight re-publishes the current state, and the
    /// running call then loops until the stored language is the one it served, so a language
    /// chosen mid-run is never dropped.
    /// </summary>
    public async Task StartAsync()
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            Publish(State, Progress);
            return;
        }

        PackSetupState? failure;
        string served;
        try
        {
            (failure, served) = await RunUntilCurrentAsync();
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }

        // Published after _running is cleared, so a Retry tapped the moment a failure shows
        // can never be swallowed by the run that just ended.
        if (failure is { } state)
        {
            Publish(state);
        }

        // A language chosen between the last check and the flag reset would otherwise be lost.
        if (!SameLanguage(served, CurrentLanguage))
        {
            await StartAsync();
        }
    }

    private string CurrentLanguage => _settings.Language ?? SupertonicVoiceTable.EnglishCode;

    private static bool SameLanguage(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private async Task<(PackSetupState? Failure, string Language)> RunUntilCurrentAsync()
    {
        while (true)
        {
            string language = CurrentLanguage;
            PackSetupState? failure = await RunAsync(language);
            if (SameLanguage(language, CurrentLanguage))
            {
                return (failure, language);
            }
        }
    }

    private async Task<PackSetupState?> RunAsync(string language)
    {
        if (_model.Paths is { } ready && string.Equals(ready.Language, language, StringComparison.OrdinalIgnoreCase))
        {
            Publish(PackSetupState.Ready); // the model already scanned and activated it: no second scan
            return null;
        }

        if (await IsInstalledAsync(language))
        {
            await ActivateInstalledAsync();
            return null;
        }

        if (_settings.Language is null)
        {
            return null;
        }

        Publish(PackSetupState.Resolving);
        (PackInfo? pack, PackSetupState? resolveFailure) = await ResolvePackAsync(language);
        if (pack is null)
        {
            return resolveFailure;
        }

        PackSetupState? downloadFailure = await DownloadPackAsync(pack);
        if (downloadFailure is null)
        {
            await ActivateInstalledAsync();
        }

        return downloadFailure;
    }

    // Scanning the packs folder touches the disk, so it never runs on the caller's (UI) thread.
    private Task<bool> IsInstalledAsync(string language) =>
        Task.Run(() => InstalledVoiceCatalog.Resolve(_packs.GetInstalledVoicePacks(), language) is not null);

    private async Task<(PackInfo? Pack, PackSetupState? Failure)> ResolvePackAsync(string language)
    {
        PackCatalogResult catalog = await _packs.GetCatalogAsync(CancellationToken.None);
        if (catalog.Status != PackDownloadResult.Ok)
        {
            return (null, ToState(catalog.Status));
        }

        PackInfo? pack = VoicePackSelector.Select(catalog.Packs, language);
        return pack is null ? (null, PackSetupState.Unavailable) : (pack, null);
    }

    private async Task<PackSetupState?> DownloadPackAsync(PackInfo pack)
    {
        Publish(PackSetupState.Downloading, 0);
        var progress = new InlineProgress(PublishProgress);
        PackDownloadResult result = await _packs.DownloadAsync(pack.Id, progress, CancellationToken.None);
        return result == PackDownloadResult.Ok ? null : ToState(result);
    }

    private async Task ActivateInstalledAsync()
    {
        await _model.InitializeAsync();
        Publish(PackSetupState.Ready);
    }

    private static PackSetupState ToState(PackDownloadResult result) => result switch
    {
        PackDownloadResult.Offline => PackSetupState.Offline,
        PackDownloadResult.HashMismatch => PackSetupState.HashMismatch,
        PackDownloadResult.InvalidPack => PackSetupState.InvalidPack,
        _ => PackSetupState.Failed,
    };

    // A late progress tick must not drag a finished or failed state back to Downloading.
    private void PublishProgress(double progress)
    {
        lock (_stateLock)
        {
            if (State != PackSetupState.Downloading)
            {
                return;
            }

            Progress = progress;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Publish(PackSetupState state, double progress = 0)
    {
        lock (_stateLock)
        {
            State = state;
            Progress = progress;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
