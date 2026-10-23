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
    private string? _failedUpdateKey;

    // One pack update at a time, cancellable (a run for another language stops it). The update runs in the
    // background, so a Retry or a language change never waits behind the catalog fetch or the download.
    private readonly SemaphoreSlim _updateGate = new(1, 1);
    private CancellationTokenSource? _updateCancel;
    private string _updateLanguage = string.Empty;

    // How long the update check waits for the catalog; offline or slow, the installed pack stays as it is.
    private static readonly TimeSpan UpdateCatalogTimeout = TimeSpan.FromSeconds(15);

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

    /// <summary>Completes when every update <see cref="StartAsync"/> started in the background has ended (completed when none ran); for tests.</summary>
    internal Task UpdateCompleted { get; private set; } = Task.CompletedTask;

    public bool CanRetry => State is PackSetupState.Offline or PackSetupState.HashMismatch or PackSetupState.Failed;

    /// <summary>
    /// An installed pack serving the language is activated at once and then checked, in the background, against the catalog for a newer version (Slice 89); a missing one
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
            return;
        }

        // After the run is over and off this call, so a Retry or a language change never waits behind a
        // catalog fetch or an 80 MB download.
        if (failure is null && await IsInstalledAsync(served))
        {
            UpdateCompleted = Task.WhenAll(UpdateCompleted, Task.Run(() => UpdateInstalledAsync(served)));
        }
    }

    private string CurrentLanguage => _settings.Language ?? SupertonicVoiceTable.EnglishCode;

    private static bool SameLanguage(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private async Task<(PackSetupState? Failure, string Language)> RunUntilCurrentAsync()
    {
        while (true)
        {
            string language = CurrentLanguage;
            CancelUpdateForOtherLanguage(language);
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

        PackDownloadResult downloaded = await DownloadPackAsync(pack, CancellationToken.None);
        if (downloaded != PackDownloadResult.Ok)
        {
            return ToState(downloaded);
        }

        await ActivateInstalledAsync();
        return null;
    }

    // Scanning the packs folder touches the disk, so it never runs on the caller's (UI) thread.
    private Task<bool> IsInstalledAsync(string language) =>
        Task.Run(() => InstalledVoiceCatalog.Resolve(_packs.GetInstalledVoicePacks(), language) is not null);

    // Slice 89: an installed pack the catalog lists in a newer version (the Amir pack becoming the
    // four-voice one) is downloaded into a folder of its own, next to the old one, and activated: the
    // installed pack keeps reading meanwhile and readers pick the new folder up when they next build an
    // engine. Any failure here (offline, slow catalog, bad download) leaves the old pack in place and Ready.
    // One update at a time (a second call while one runs does nothing), stopped by a run for another
    // language, and its result dropped if the language moved on meanwhile.
    private async Task UpdateInstalledAsync(string language)
    {
        using var cancel = new CancellationTokenSource();
        if (!TryRegisterUpdate(cancel, language))
        {
            return;
        }

        bool holdsGate = false;
        try
        {
            await _updateGate.WaitAsync(cancel.Token); // the update this one cancelled unwinds first
            holdsGate = true;
            await CheckAndApplyUpdateAsync(language, cancel.Token);
        }
        catch (OperationCanceledException)
        {
            // A run for another language took over; it publishes its own state.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"VoicePackSetup update check: {ex}");
            RestoreReady(language);
        }
        finally
        {
            ClearUpdate(cancel);
            if (holdsGate)
            {
                _updateGate.Release();
            }
        }
    }

    private async Task CheckAndApplyUpdateAsync(string language, CancellationToken cancellationToken)
    {
        PackInfo? newer = await FindNewerPackAsync(language, cancellationToken);
        if (newer is not null && SameLanguage(language, CurrentLanguage) && !cancellationToken.IsCancellationRequested)
        {
            await DownloadAndActivateAsync(newer, language, cancellationToken);
        }
    }

    // False when an update for the same language is already queued or running: it does the work.
    private bool TryRegisterUpdate(CancellationTokenSource cancel, string language)
    {
        lock (_stateLock)
        {
            if (_updateCancel is not null && SameLanguage(_updateLanguage, language))
            {
                return false;
            }

            _updateCancel = cancel;
            _updateLanguage = language;
            return true;
        }
    }

    private void ClearUpdate(CancellationTokenSource cancel)
    {
        lock (_stateLock)
        {
            if (ReferenceEquals(_updateCancel, cancel))
            {
                _updateCancel = null;
                _updateLanguage = string.Empty;
            }
        }
    }

    private void CancelUpdateForOtherLanguage(string language)
    {
        CancellationTokenSource? stale = null;
        lock (_stateLock)
        {
            if (_updateCancel is not null && !SameLanguage(_updateLanguage, language))
            {
                stale = _updateCancel;
            }
        }

        try
        {
            stale?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The update just ended: nothing to stop.
        }
    }

    private async Task<PackInfo?> FindNewerPackAsync(string language, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(UpdateCatalogTimeout);
        try
        {
            PackCatalogResult catalog = await _packs.GetCatalogAsync(timeout.Token);
            return catalog.Status == PackDownloadResult.Ok ? await NewerThanInstalledAsync(catalog.Packs, language) : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null; // the catalog was too slow
        }
    }

    // A version whose download failed for good (a bad hash, a broken zip) is not tried again until the app
    // restarts, so the status does not flicker to Downloading on every call and the bandwidth is not burnt
    // again. Offline and transient failures are not remembered: the next call tries again.
    private async Task DownloadAndActivateAsync(PackInfo pack, string language, CancellationToken cancellationToken)
    {
        string key = PackUpdateKey.For(pack);
        if (key == Volatile.Read(ref _failedUpdateKey))
        {
            return;
        }

        PackDownloadResult result = await DownloadPackAsync(pack, cancellationToken, background: true);
        if (result == PackDownloadResult.Ok)
        {
            await ActivateInstalledAsync(background: true);
            return;
        }

        if (PackUpdateKey.IsPermanentFailure(result) && !cancellationToken.IsCancellationRequested)
        {
            Volatile.Write(ref _failedUpdateKey, key);
        }

        RestoreReady(language);
    }

    // The old pack still reads, so a failed update shows Ready again (unless another language took over).
    private void RestoreReady(string language)
    {
        if (SameLanguage(language, CurrentLanguage))
        {
            PublishFromUpdate(PackSetupState.Ready);
        }
    }

    private Task<PackInfo?> NewerThanInstalledAsync(IReadOnlyList<PackInfo> catalog, string language) =>
        Task.Run(() =>
        {
            InstalledVoicePack? installed = PackSelection.Serving(_packs.GetInstalledVoicePacks(), PackKind.Voice, language).FirstOrDefault();
            PackInfo? listed = installed is null ? null : catalog.FirstOrDefault(p => p.Id == installed.Manifest.Id);
            return listed is not null && PackVersions.IsNewer(listed.Version, installed!.Manifest.Version) ? listed : null;
        });

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

    private async Task<PackDownloadResult> DownloadPackAsync(PackInfo pack, CancellationToken cancellationToken, bool background = false)
    {
        PublishFor(background, PackSetupState.Downloading);
        var progress = new InlineProgress(value => PublishProgress(value, cancellationToken, background));
        return await _packs.DownloadAsync(pack.Id, progress, cancellationToken);
    }

    private async Task ActivateInstalledAsync(bool background = false)
    {
        await _model.InitializeAsync();
        PublishFor(background, PackSetupState.Ready);
    }

    private static PackSetupState ToState(PackDownloadResult result) => result switch
    {
        PackDownloadResult.Offline => PackSetupState.Offline,
        PackDownloadResult.HashMismatch => PackSetupState.HashMismatch,
        PackDownloadResult.InvalidPack => PackSetupState.InvalidPack,
        _ => PackSetupState.Failed,
    };

    // A late progress tick must not drag a finished or failed state back to Downloading, nor show the
    // fraction of a download that was stopped.
    private void PublishProgress(double progress, CancellationToken cancellationToken, bool background)
    {
        lock (_stateLock)
        {
            if (State != PackSetupState.Downloading || cancellationToken.IsCancellationRequested || (background && ForegroundRunning))
            {
                return;
            }

            Progress = progress;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool ForegroundRunning => Volatile.Read(ref _running) == 1;

    private void PublishFor(bool background, PackSetupState state)
    {
        if (background)
        {
            PublishFromUpdate(state);
            return;
        }

        Publish(state);
    }

    // The background update never overwrites what a foreground run (a first install, a Retry) shows.
    private void PublishFromUpdate(PackSetupState state)
    {
        if (!ForegroundRunning)
        {
            Publish(state);
        }
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
