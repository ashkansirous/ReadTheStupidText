using ReadTheStupidText.Application;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>What the UI shows about the first-run voice pack download.</summary>
public enum PackInstallState
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
/// Runs the "chosen language -> catalog -> voice pack -> neural voice" flow once at a
/// time and publishes its state, so the language page and the Reader show the same
/// progress while the phone voice keeps reading (Slice 50, Decision 61). A singleton:
/// the download outlives the page that started it.
/// </summary>
public sealed class VoicePackInstaller
{
    private const string ResumeKey = "voice";
    private static readonly TimeSpan UpdateCatalogTimeout = TimeSpan.FromSeconds(15);

    private readonly IVoicePackService _packs;
    private readonly IVoiceModelService _model;
    private readonly ISpeechReader _reader;
    private readonly ISettingsStore _settings;
    private readonly IPlatformServices _platform;
    private readonly IPackUpdateNetwork _network;
    private readonly IPackResume? _resume;
    private string? _resolvedPackId;
    private readonly object _gate = new();
    private CancellationTokenSource? _updateCancel;
    private bool _freshInstall; // the last run downloaded its pack, so the update check would only refetch the catalog

    // One pack update at a time: a newer update waits for the one it cancelled to unwind, so two never
    // touch the same scratch or target folder.
    private readonly SemaphoreSlim _updateGate = new(1, 1);
    private string? _failedUpdateKey;
    private bool _running;
    private bool _userCancelled; // the user tapped Cancel on the notification during this run; reset at the start of each run, guarded by _gate
    private bool _runReady; // this run ended with the voice installed and published Ready: a late Cancel never overwrites it; guarded by _gate
    private string _runLanguage = SupertonicVoiceTable.EnglishCode;
    private CancellationTokenSource? _runCancel;
    private int _generation;
    private TaskCompletionSource _settled = new();

    public VoicePackInstaller(IVoicePackService packs, IVoiceModelService model, ISpeechReader reader, ISettingsStore settings, IPlatformServices platform, IPackUpdateNetwork network, ILanguageChangeNotifier languages, IPackResume? resume = null)
    {
        _resume = resume;
        _packs = packs;
        _model = model;
        _reader = reader;
        _settings = settings;
        _platform = platform;
        _network = network;
        languages.LanguageChanged += (_, _) => OnLanguageChanged();
        PackDownloadHost.CancelRequested += OnNotificationCancel;
    }

    // Slice 183: Cancel on the download notification. Final like the in-app Cancel: the run ends, reports Cancelled (the
    // coordinator forgets it) and nothing restarts by itself; the partial file stays for a later start.
    private void OnNotificationCancel(string packId)
    {
        CancellationTokenSource? cancel;
        bool updateCancelled = false;
        lock (_gate)
        {
            if (packId != Volatile.Read(ref _resolvedPackId))
            {
                return;
            }

            if (_running)
            {
                _userCancelled = true;
                cancel = _runCancel;
            }
            else
            {
                cancel = _updateCancel; // a pack update downloading in the background honours Cancel too
                updateCancelled = cancel is not null;
            }
        }

        CancelQuietly(cancel);
        if (updateCancelled)
        {
            Publish(Volatile.Read(ref _generation), PackInstallState.Ready); // the old pack still reads
        }
    }

    public PackInstallState State { get; private set; } = PackInstallState.Idle;

    /// <summary>Download progress 0..1 while <see cref="State"/> is Downloading.</summary>
    public double Progress { get; private set; }

    public event EventHandler? Changed;

    public bool CanRetry => State is PackInstallState.Offline or PackInstallState.HashMismatch or PackInstallState.Failed;

    // One pass of the flow: its generation orders publishes, its token stops it.
    private readonly record struct Run(int Generation, CancellationToken Token);

    // A language change stops the in-flight run for the old language (its partial download is
    // kept for resuming, never installed) and starts one for the new language off the UI thread:
    // the first steps scan the installed packs on disk. Cancel runs outside the lock because it
    // invokes registered callbacks inline.
    private void OnLanguageChanged()
    {
        CancellationTokenSource? stale = null;
        bool running;
        lock (_gate)
        {
            running = _running;
            if (running && !SameLanguage(_runLanguage, CurrentLanguage))
            {
                stale = _runCancel;
            }
        }

        CancelQuietly(stale);

        // A run in flight re-checks the language when it ends (RunUntilSettledAsync), so it serves
        // the change itself; a second start queued behind it could land after it and run again.
        if (!running)
        {
            _ = Task.Run(StartAsync);
        }
    }

    // The run may have just ended and disposed its source; there is then nothing to cancel.
    private static void CancelQuietly(CancellationTokenSource? source)
    {
        try
        {
            source?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    // An installed pack is activated whatever the stored language (it needs no network
    // and no language); only a missing pack needs the language to choose one. A call
    // while a run is in flight re-publishes the current state so the caller's UI keeps
    // showing the in-flight progress, and completes when that run settles.
    public Task StartAsync()
    {
        Task? inFlight = null;
        TaskCompletionSource? settled = null;
        lock (_gate)
        {
            if (_running)
            {
                inFlight = _settled.Task;
            }
            else
            {
                _running = true;
                settled = _settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        if (inFlight is not null)
        {
            Publish(Volatile.Read(ref _generation), State, Progress);
            return inFlight;
        }

        return RunToCompletionAsync(settled!);
    }

    // Any error ends in the Failed state, so a run started by a language change (nobody awaits it)
    // can never leave the spinner up. Waiters are released even if a Changed handler throws.
    private async Task RunToCompletionAsync(TaskCompletionSource settled)
    {
        try
        {
            (PackInstallState? failure, int generation) = await RunUntilSettledAsync();

            // Published after _running is cleared, so a Retry tapped the moment a failure shows
            // is never swallowed; a newer run's generation makes this publish a no-op.
            ReportEnd(failure);
            if (failure is { } state)
            {
                Publish(generation, state);
            }
            else if (!Volatile.Read(ref _freshInstall))
            {
                StartUpdateInBackground(generation); // a pack just downloaded is already the newest catalog entry
            }
        }
        finally
        {
            settled.TrySetResult();
        }
    }


    // Slice 182: every run reports its end once; the coordinator decides what is kept. Only Offline and Failed with a
    // chosen language can restart; any other end (Ready, a run the user stopped) is reported as finished.
    private void ReportEnd(PackInstallState? failure)
    {
        bool cancelled;
        lock (_gate)
        {
            cancelled = _userCancelled && !_runReady; // a Cancel that arrives as the run finishes does not undo a finished install
            _userCancelled = false;
        }

        if (cancelled)
        {
            _resume?.Report(ResumeKey, PackRunEnd.Cancelled, () => Task.FromResult(false));
            Publish(Volatile.Read(ref _generation), PackInstallState.Idle);
            return;
        }

        PackRunEnd end = failure switch
        {
            PackInstallState.Offline when _settings.Language is not null => PackRunEnd.Offline,
            PackInstallState.Failed when _settings.Language is not null => PackRunEnd.Failed,
            _ => PackRunEnd.Finished,
        };
        _resume?.Report(ResumeKey, end, ResumeAsync, Volatile.Read(ref _resolvedPackId));
    }

    // The restart the coordinator calls: false when a run is in flight (that run reports its own end).
    private async Task<bool> ResumeAsync()
    {
        lock (_gate)
        {
            if (_running)
            {
                return false;
            }
        }

        await StartAsync().ConfigureAwait(false);
        return true;
    }

    // An unset language reads as English, as everywhere else on Mobile.
    private string CurrentLanguage => _settings.Language ?? SupertonicVoiceTable.EnglishCode;

    private static bool SameLanguage(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // A language chosen while a run is in flight (Slice 81) is served by the same call once the
    // run for the old language stops, so no request is dropped. The flag is cleared in the same
    // lock as the final language check, so a change can never fall between the two; that holds
    // for a run that threw as well.
    private async Task<(PackInstallState? Failure, int Generation)> RunUntilSettledAsync()
    {
        while (true)
        {
            CancelQuietly(Interlocked.Exchange(ref _updateCancel, null)); // a new run supersedes a pack update
            var cancel = new CancellationTokenSource();
            (Run run, string language) = BeginRun(cancel);
            PackInstallState? failure = await RunGuardedAsync(run);
            bool settled = EndRun(language, cancel);
            cancel.Dispose();
            if (settled)
            {
                return (failure, run.Generation);
            }
        }
    }

    private (Run Run, string Language) BeginRun(CancellationTokenSource cancel)
    {
        lock (_gate)
        {
            _runCancel = cancel;
            _userCancelled = false;
            _runReady = false;
            Volatile.Write(ref _resolvedPackId, null);
            _runLanguage = CurrentLanguage;
            return (new Run(++_generation, cancel.Token), _runLanguage);
        }
    }

    // Always drops the source so a later cancel never reaches a disposed one; the run is over
    // only when it was neither cancelled nor overtaken by a language change.
    private bool EndRun(string language, CancellationTokenSource cancel)
    {
        lock (_gate)
        {
            _runCancel = null;
            if (_userCancelled)
            {
                _running = false;
                return true; // final: no restart for a Cancel
            }

            if (cancel.IsCancellationRequested || !SameLanguage(language, CurrentLanguage))
            {
                return false;
            }

            _running = false;
            return true;
        }
    }

    private async Task<PackInstallState?> RunGuardedAsync(Run run)
    {
        try
        {
            return await RunAsync(run);
        }
        catch (PackCancelledByNotificationException)
        {
            lock (_gate)
            {
                _userCancelled = true; // the notification's Cancel reached a run whose pack id was not matched: still final, no error state
            }

            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"VoicePackInstaller: {ex}");
            return PackInstallState.Failed;
        }
    }

    /// <summary>Publishes the terminal Failed state for an error the caller caught outside a run.</summary>
    public void ReportFailed()
    {
        Publish(Volatile.Read(ref _generation), PackInstallState.Failed);
    }

    // Returns the failure state to publish, or null when nothing failed or the run was cancelled.
    private async Task<PackInstallState?> RunAsync(Run run)
    {
        try
        {
            return await RunCoreAsync(run);
        }
        catch (OperationCanceledException) when (run.Token.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<PackInstallState?> RunCoreAsync(Run run)
    {
        Volatile.Write(ref _freshInstall, false);
        // A platform with no neural engine never fetches the catalog or the pack; the
        // phone voice reads, which the Unavailable state already says.
        if (!_platform.SupportsNeuralVoice)
        {
            return PackInstallState.Unavailable;
        }

        // Activates a pack that already serves the language (Slice 51: one pack per
        // language family, so the Supertonic pack no longer stands for every language).
        if (InstalledVoiceCatalog.Resolve(_packs.GetInstalledVoiceAndStorytellerPacks(), CurrentLanguage) is not null)
        {
            await ActivateInstalledAsync(run);
            return null;
        }

        if (_settings.Language is not { } language)
        {
            // No language and nothing installed: clear any spinner a cancelled run left behind.
            Publish(run.Generation, PackInstallState.Idle);
            return null;
        }

        Publish(run.Generation, PackInstallState.Resolving);
        (PackInfo? pack, PackInstallState? resolveFailure) = await ResolvePackAsync(language, run.Token);
        if (pack is null)
        {
            return resolveFailure;
        }

        Volatile.Write(ref _resolvedPackId, pack.Id);
        PackDownloadResult downloaded = await DownloadPackAsync(pack, run);
        if (downloaded != PackDownloadResult.Ok)
        {
            return ToState(downloaded);
        }

        Volatile.Write(ref _freshInstall, true);
        await ActivateInstalledAsync(run);
        MarkRunReady();
        return null;
    }

    // Slice 89: an installed pack the catalog lists in a newer version (Amir's pack becoming the
    // four-voice one) is downloaded into a folder of its own, next to the old one, and activated again.
    // It runs after the install run has ended, so a Retry or a language change never waits behind the
    // catalog fetch or the download (a new run cancels it). Only on an unmetered connection: on cellular
    // the installed pack stays Ready and the check happens again next launch. A first install is not an
    // update and is never held back.
    private void StartUpdateInBackground(int generation)
    {
        if (!_network.AllowsAutomaticUpdate || !_platform.SupportsNeuralVoice)
        {
            return;
        }

        _ = Task.Run(() => UpdateInstalledAsync(generation));
    }

    private async Task UpdateInstalledAsync(int generation)
    {
        var cancel = new CancellationTokenSource();
        CancelQuietly(Interlocked.Exchange(ref _updateCancel, cancel));
        var run = new Run(generation, cancel.Token);
        bool holdsGate = false;
        try
        {
            await _updateGate.WaitAsync(cancel.Token); // the update this one cancelled unwinds first
            holdsGate = true;
            await CheckAndApplyUpdateAsync(run);
        }
        catch (OperationCanceledException)
        {
            // A newer run took over; it publishes its own state.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"VoicePackInstaller update check: {ex}");
            if (!run.Token.IsCancellationRequested)
            {
                Publish(run.Generation, PackInstallState.Ready);
            }
        }
        finally
        {
            if (holdsGate)
            {
                _updateGate.Release();
            }

            Interlocked.CompareExchange(ref _updateCancel, null, cancel);
            cancel.Dispose();
        }
    }

    private async Task CheckAndApplyUpdateAsync(Run run)
    {
        string language = CurrentLanguage;
        if (InstalledVoiceCatalog.Resolve(_packs.GetInstalledVoicePacks(), language) is null)
        {
            return;
        }

        PackInfo? newer = await FindNewerPackAsync(run);
        if (newer is not null && SameLanguage(language, CurrentLanguage) && !run.Token.IsCancellationRequested)
        {
            await DownloadAndActivateAsync(newer, run);
        }
    }

    // The catalog fetch is bounded, so a slow network cannot hold the run (and a Retry behind it).
    private async Task<PackInfo?> FindNewerPackAsync(Run run)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(run.Token);
        timeout.CancelAfter(UpdateCatalogTimeout);
        try
        {
            PackCatalogResult catalog = await _packs.GetCatalogAsync(timeout.Token);
            return catalog.Status == PackDownloadResult.Ok ? NewerThanInstalled(catalog.Packs) : null;
        }
        catch (OperationCanceledException) when (!run.Token.IsCancellationRequested)
        {
            return null;
        }
    }

    // Only a download that was fetched and rejected (a bad hash, a broken zip) is remembered; being offline
    // or a transient failure is retried at the next check.
    private async Task DownloadAndActivateAsync(PackInfo pack, Run run)
    {
        string key = PackUpdateKey.For(pack);
        if (key == Volatile.Read(ref _failedUpdateKey))
        {
            return; // this exact version was rejected: not again until the app restarts
        }

        PackDownloadResult result = await DownloadPackAsync(pack, run);
        if (result == PackDownloadResult.Ok)
        {
            await ActivateInstalledAsync(run);
            return;
        }

        if (PackUpdateKey.IsPermanentFailure(result) && !run.Token.IsCancellationRequested)
        {
            Volatile.Write(ref _failedUpdateKey, key);
        }

        Publish(run.Generation, PackInstallState.Ready);
    }

    private PackInfo? NewerThanInstalled(IReadOnlyList<PackInfo> catalog)
    {
        InstalledVoicePack? installed = PackSelection.Serving(_packs.GetInstalledVoicePacks(), PackKind.Voice, CurrentLanguage).FirstOrDefault();
        PackInfo? listed = installed is null ? null : catalog.FirstOrDefault(p => p.Id == installed.Manifest.Id);
        return listed is not null && PackVersions.IsNewer(listed.Version, installed!.Manifest.Version) ? listed : null;
    }

    private async Task<(PackInfo? Pack, PackInstallState? Failure)> ResolvePackAsync(string language, CancellationToken cancellationToken)
    {
        PackCatalogResult catalog = await _packs.GetCatalogAsync(cancellationToken);
        if (catalog.Status != PackDownloadResult.Ok)
        {
            return (null, ToState(catalog.Status));
        }

        PackInfo? pack = VoicePackSelector.Select(catalog.Packs, language);
        return pack is null ? (null, PackInstallState.Unavailable) : (pack, null);
    }

    private async Task<PackDownloadResult> DownloadPackAsync(PackInfo pack, Run run)
    {
        Volatile.Write(ref _resolvedPackId, pack.Id); // also for an update download, so the notification's Cancel finds it
        Publish(run.Generation, PackInstallState.Downloading, 0);
        var progress = new InlineProgress(value => PublishProgress(run, value));
        return await _packs.DownloadAsync(pack.Id, progress, run.Token);
    }

    private void MarkRunReady()
    {
        lock (_gate)
        {
            _runReady = true;
        }
    }

    private async Task ActivateInstalledAsync(Run run)
    {
        await _model.InitializeAsync();
        Publish(run.Generation, PackInstallState.Ready);
        await _reader.WarmUpAsync();
    }

    private static PackInstallState ToState(PackDownloadResult result) => result switch
    {
        PackDownloadResult.Offline => PackInstallState.Offline,
        PackDownloadResult.HashMismatch => PackInstallState.HashMismatch,
        PackDownloadResult.InvalidPack => PackInstallState.InvalidPack,
        _ => PackInstallState.Failed,
    };

    // Called from the download pipeline's background thread too: state and the
    // Changed event always land on the main thread, so UI handlers can touch views.
    // A publish from a run that is no longer the latest is dropped.
    private void Publish(int generation, PackInstallState state, double progress = 0)
    {
        if (MainThread.IsMainThread)
        {
            Apply(generation, state, progress);
            return;
        }

        MainThread.BeginInvokeOnMainThread(() => Apply(generation, state, progress));
    }

    // A late progress tick must not drag a finished or failed state back to Downloading, nor
    // show a cancelled download's fraction.
    private void PublishProgress(Run run, double progress)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (State == PackInstallState.Downloading && !run.Token.IsCancellationRequested)
            {
                Apply(run.Generation, PackInstallState.Downloading, progress);
            }
        });
    }

    private void Apply(int generation, PackInstallState state, double progress)
    {
        if (generation != Volatile.Read(ref _generation))
        {
            return;
        }

        State = state;
        Progress = progress;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
