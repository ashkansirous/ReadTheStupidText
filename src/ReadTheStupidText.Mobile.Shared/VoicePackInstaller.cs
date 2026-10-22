using ReadTheStupidText.Application;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile;

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
    private readonly IVoicePackService _packs;
    private readonly IVoiceModelService _model;
    private readonly ISpeechReader _reader;
    private readonly ISettingsStore _settings;
    private readonly IPlatformServices _platform;
    private readonly object _gate = new();
    private bool _running;
    private string _runLanguage = SupertonicVoiceTable.EnglishCode;
    private CancellationTokenSource? _runCancel;
    private int _generation;
    private TaskCompletionSource _settled = new();

    public VoicePackInstaller(IVoicePackService packs, IVoiceModelService model, ISpeechReader reader, ISettingsStore settings, IPlatformServices platform, ILanguageChangeNotifier languages)
    {
        _packs = packs;
        _model = model;
        _reader = reader;
        _settings = settings;
        _platform = platform;
        languages.LanguageChanged += (_, _) => OnLanguageChanged();
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
            if (failure is { } state)
            {
                Publish(generation, state);
            }
        }
        finally
        {
            settled.TrySetResult();
        }
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
        // A platform with no neural engine never fetches the catalog or the pack; the
        // phone voice reads, which the Unavailable state already says.
        if (!_platform.SupportsNeuralVoice)
        {
            return PackInstallState.Unavailable;
        }

        // Activates a pack that already serves the language (Slice 51: one pack per
        // language family, so the Supertonic pack no longer stands for every language).
        if (InstalledVoiceCatalog.Resolve(_packs.GetInstalledVoicePacks(), CurrentLanguage) is not null)
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

        PackInstallState? downloadFailure = await DownloadPackAsync(pack, run);
        if (downloadFailure is null)
        {
            await ActivateInstalledAsync(run);
        }

        return downloadFailure;
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

    private async Task<PackInstallState?> DownloadPackAsync(PackInfo pack, Run run)
    {
        Publish(run.Generation, PackInstallState.Downloading, 0);
        var progress = new InlineProgress(value => PublishProgress(run, value));
        PackDownloadResult result = await _packs.DownloadAsync(pack.Id, progress, run.Token);
        return result == PackDownloadResult.Ok ? null : ToState(result);
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
