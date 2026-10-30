using ReadTheStupidText.Application;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>Slice 204: why a voice pack start was asked for. Required on every call, so no caller can forget to say.</summary>
public enum PackStartReason
{
    /// <summary>The user's own Download/Retry tap or a choice they made: lifts a Cancel hold.</summary>
    User,

    /// <summary>App start, network or foreground resume, a language change: never restarts a download the user stopped.</summary>
    Automatic,
}

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
    /// <summary>Slice 204: the user stopped the download; it starts again only when they ask.</summary>
    Stopped,
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
    private const string HeldMarker = "VOICEPACK held-user-cancelled";
    private static readonly TimeSpan UpdateCatalogTimeout = TimeSpan.FromSeconds(15);

    private readonly IVoicePackService _packs;
    private readonly IVoiceModelService _model;
    private readonly ISpeechReader _reader;
    private readonly ISettingsStore _settings;
    private readonly IPlatformServices _platform;
    private readonly IPackUpdateNetwork _network;
    private readonly IPackResume? _resume;
    private readonly VoiceCancelHold _hold;
    private string? _resolvedPackId; // the pack downloading now (a run's or a pack update's): the notification's Cancel matches on it
    private readonly object _gate = new();
    private CancellationTokenSource? _updateCancel;

    // One pack update at a time: a newer update waits for the one it cancelled to unwind, so two never
    // touch the same scratch or target folder.
    private readonly SemaphoreSlim _updateGate = new(1, 1);
    private string? _failedUpdateKey;
    private VoiceRun? _current; // the run in flight or unwinding, if any; guarded by _gate
    private int _latestId;      // the id of the newest run ever created: the only id whose publishes are applied

    // Slice 204: one object per run. Everything a run decides lives here, never in an installer-wide flag, so a
    // newer run is never touched by an older one. Fields marked "gate" are read and written under _gate.
    private sealed class VoiceRun(int id, string language)
    {
        public int Id { get; } = id;
        public string Language = language;                // gate: the language of the pass in flight
        public bool Cancelled;                            // gate: the user cancelled this run (never reset)
        public bool Ready;                                // gate: this run installed the voice and published Ready: a late Cancel never undoes it
        public bool Started;                              // gate: its first pass began (a run chained after a cancelled one waits)
        public CancellationTokenSource? Cancel;           // gate: the pass in flight
        public string? PackId;                            // the pack this run downloaded
        public bool FreshInstall;                         // this run downloaded its pack, so the update check would only refetch the catalog
        public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    // What the locked end step decided, reported and published after the lock is released.
    private sealed record RunEnd(int Id, PackInstallState? Failure, bool Cancelled, bool Held, bool IsCurrent, bool Fresh, bool LanguageChosen, string? PackId, bool LanguageMoved);

    public VoicePackInstaller(IVoicePackService packs, IVoiceModelService model, ISpeechReader reader, ISettingsStore settings, IPlatformServices platform, IPackUpdateNetwork network, ILanguageChangeNotifier languages, IPackResume? resume = null, IVoiceCancelStore? cancelStore = null)
    {
        _resume = resume;
        _hold = new VoiceCancelHold(cancelStore ?? new PreferencesVoiceCancelStore());
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

            if (_current is { Started: true } run)
            {
                CancelRun(run);
                cancel = run.Cancel;
            }
            else if (_current is null)
            {
                cancel = _updateCancel; // a pack update downloading in the background honours Cancel too
                updateCancelled = cancel is not null;
            }
            else
            {
                return; // a run chained after a cancelled one has not started: this Cancel was for the run it waits on
            }
        }

        CancelQuietly(cancel);
        if (updateCancelled)
        {
            Publish(Volatile.Read(ref _latestId), PackInstallState.Ready); // the old pack still reads
        }
    }

    // Marks this run cancelled, and holds its language when that is still the chosen one (Slice 186: a stale run, the
    // language changed under it, leaves no hold). A cancel targets one run: it is a no-op when that run already installed the
    // voice, is already cancelled, or is no longer the current run, so a late cancel never re-sets a hold the user released.
    // Guarded by _gate.
    private void CancelRun(VoiceRun run)
    {
        if (run.Ready || run.Cancelled || !ReferenceEquals(_current, run))
        {
            return;
        }

        run.Cancelled = true;
        if (SameLanguage(run.Language, CurrentLanguage))
        {
            _hold.Hold(run.Language);
        }
    }

    /// <summary>Debug builds route the held marker here (the app wires it to the test-hook log); null in release and in unit tests.</summary>
    public Action<string>? DebugMarker { get; set; }

    private void LogHeld(string language)
    {
        string line = $"{HeldMarker} {language}";
        System.Diagnostics.Debug.WriteLine(line);
        DebugMarker?.Invoke(line);
    }

    /// <summary>Slice 186: the user cancelled the voice pack download for this language and has not got it since; other packs do not start by themselves.</summary>
    public bool IsUserCancelled(string language) => _hold.IsHeld(language);

    public PackInstallState State { get; private set; } = PackInstallState.Idle;

    /// <summary>Download progress 0..1 while <see cref="State"/> is Downloading.</summary>
    public double Progress { get; private set; }

    public event EventHandler? Changed;

    public bool CanRetry => State is PackInstallState.Offline or PackInstallState.HashMismatch or PackInstallState.Failed or PackInstallState.Stopped;

    // One pass of the flow: its id orders publishes, its token stops it. Voice is null for a pack update.
    private readonly record struct Run(int Generation, CancellationToken Token, VoiceRun? Voice = null);

    // A language change stops the in-flight run for the old language (its partial download is
    // kept for resuming, never installed) and starts one for the new language off the UI thread:
    // the first steps scan the installed packs on disk. Cancel runs outside the lock because it
    // invokes registered callbacks inline.
    private void OnLanguageChanged()
    {
        CancellationTokenSource? stale = null;
        bool serving;
        lock (_gate)
        {
            _hold.ReleaseUnlessFor(CurrentLanguage); // inside the gate, so a Cancel's hold is never cleared half-way
            serving = _current is { Cancelled: false };
            if (_current is { Started: true, Cancelled: false } run && !SameLanguage(run.Language, CurrentLanguage))
            {
                stale = run.Cancel;
            }
        }

        CancelQuietly(stale);

        // A run in flight re-checks the language when it ends (RunUntilSettledAsync), so it serves
        // the change itself; a second start queued behind it could land after it and run again.
        // A cancelled run does not serve it: the start chains after that run.
        if (!serving)
        {
            _ = Task.Run(() => StartAsync(PackStartReason.Automatic));
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
    //
    // Slice 204: every caller states why it starts (no default, so a new caller cannot forget): User is a Download/Retry tap,
    // Automatic is app start, network or foreground resume, a language change. All of it is decided in one locked step:
    //   user                      -> release the hold first
    //   live run                  -> join it (before the held check)
    //   automatic + language held -> publish Stopped, report nothing, start nothing
    //   cancelled run unwinding   -> a new run chained after it (never its stale task)
    //   no run                    -> a new run
    public Task StartAsync(PackStartReason reason)
    {
        bool automatic = reason == PackStartReason.Automatic;
        bool installed = automatic && HasInstalledVoice(); // an installed pack is activated even for a held language
        VoiceRun? next = null;
        VoiceRun? previous = null;
        Task? joined = null;
        bool held = false;
        int latest;
        lock (_gate)
        {
            latest = _latestId;
            if (!automatic)
            {
                _hold.Release(); // first, so the run the user cancelled ends without leaving a hold behind
            }

            if (_current is { Cancelled: false } live)
            {
                joined = live.Done.Task; // a live run is joined before the held check: a stale hold never stops it
            }
            else if (automatic && !installed && _hold.IsHeld(CurrentLanguage))
            {
                held = true;
            }
            else
            {
                previous = _current;
                next = _current = new VoiceRun(latest = ++_latestId, CurrentLanguage);
            }
        }

        if (held)
        {
            LogHeld(CurrentLanguage);
            Publish(latest, PackInstallState.Stopped);
            return Task.CompletedTask;
        }

        if (next is null)
        {
            Publish(latest, State, Progress);
            return joined!;
        }

        return RunToCompletionAsync(next, previous?.Done.Task);
    }

    private bool HasInstalledVoice() =>
        _platform.SupportsNeuralVoice && InstalledVoiceCatalog.Resolve(_packs.GetInstalledVoiceAndStorytellerPacks(), CurrentLanguage) is not null;

    // Any error ends in the Failed state, so a run started by a language change (nobody awaits it)
    // can never leave the spinner up. Waiters are released even if a Changed handler throws.
    private async Task RunToCompletionAsync(VoiceRun run, Task? after)
    {
        try
        {
            if (after is not null)
            {
                await after.ConfigureAwait(false); // chained after the cancelled run: never two runs on the same files
            }

            RunEnd end = await RunUntilSettledAsync(run);
            ReportAndPublish(end);
            if (end.IsCurrent && end.LanguageMoved)
            {
                _ = Task.Run(() => StartAsync(PackStartReason.Automatic)); // the old language's run was cancelled: the new language still starts
            }
        }
        finally
        {
            run.Done.TrySetResult();
        }
    }

    // Slice 182: every run reports its end once; the coordinator decides what is kept. Only Offline and Failed with a
    // chosen language can restart; any other end (Ready) is reported as finished and a run the user stopped as Cancelled.
    // Runs after the end step released the lock, tagged with the run's id: a run that is no longer current (a newer one
    // was started in the gap) reports and publishes nothing, so it cannot overwrite the newer run.
    private void ReportAndPublish(RunEnd end)
    {
        if (!end.IsCurrent)
        {
            return;
        }

        if (end.Cancelled)
        {
            _resume?.Report(ResumeKey, PackRunEnd.Cancelled, () => Task.FromResult(false));
            Publish(end.Id, end.Held ? PackInstallState.Stopped : PackInstallState.Idle);
            return;
        }

        PackRunEnd kind = end.Failure switch
        {
            PackInstallState.Offline when end.LanguageChosen => PackRunEnd.Offline,
            PackInstallState.Failed when end.LanguageChosen => PackRunEnd.Failed,
            _ => PackRunEnd.Finished,
        };
        _resume?.Report(ResumeKey, kind, ResumeAsync, end.PackId);
        if (end.Failure is { } state)
        {
            Publish(end.Id, state);
        }
        else if (!end.Fresh)
        {
            StartUpdateInBackground(end.Id); // a pack just downloaded is already the newest catalog entry
        }
    }

    // The restart the coordinator calls: false when a run is in flight (that run reports its own end).
    private async Task<bool> ResumeAsync()
    {
        lock (_gate)
        {
            if (_current is not null)
            {
                return false;
            }
        }

        await StartAsync(PackStartReason.Automatic).ConfigureAwait(false);
        return true;
    }

    // An unset language reads as English, as everywhere else on Mobile.
    private string CurrentLanguage => _settings.Language ?? SupertonicVoiceTable.EnglishCode;

    private static bool SameLanguage(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // A language chosen while a run is in flight (Slice 81) is served by the same run once the pass
    // for the old language stops, so no request is dropped. The decision to stop is taken in the same
    // lock as the final language check, so a change can never fall between the two; that holds
    // for a pass that threw as well.
    private async Task<RunEnd> RunUntilSettledAsync(VoiceRun run)
    {
        while (true)
        {
            CancelQuietly(Interlocked.Exchange(ref _updateCancel, null)); // a new run supersedes a pack update
            var cancel = new CancellationTokenSource();
            string language = BeginPass(run, cancel);
            PackInstallState? failure = await RunGuardedAsync(new Run(run.Id, cancel.Token, run));
            RunEnd? end = EndPass(run, language, cancel, failure);
            cancel.Dispose();
            if (end is not null)
            {
                return end;
            }
        }
    }

    private string BeginPass(VoiceRun run, CancellationTokenSource cancel)
    {
        lock (_gate)
        {
            run.Cancel = cancel;
            run.Started = true;
            run.PackId = null;
            Volatile.Write(ref _resolvedPackId, null);
            run.Language = CurrentLanguage;
            if (run.Cancelled)
            {
                cancel.Cancel(); // a Cancel landed between two passes
            }

            return run.Language;
        }
    }

    // Null when another pass must follow (the language changed under this one). Otherwise the run's single locked end step:
    // decide the end state, hold the language when the user cancelled it (and it is still current), and clear current if it
    // is still this run. A Download tap right after finds no run and starts a new one; this run's id is no longer the newest then.
    private RunEnd? EndPass(VoiceRun run, string language, CancellationTokenSource cancel, PackInstallState? failure)
    {
        lock (_gate)
        {
            run.Cancel = null;
            if (!run.Cancelled && (cancel.IsCancellationRequested || !SameLanguage(language, CurrentLanguage)))
            {
                return null;
            }

            bool cancelled = run.Cancelled && !run.Ready;
            bool isCurrent = ReferenceEquals(_current, run);
            if (cancelled && isCurrent && SameLanguage(run.Language, CurrentLanguage))
            {
                _hold.Hold(run.Language);
            }

            if (isCurrent)
            {
                _current = null;
            }

            return new RunEnd(run.Id, failure, cancelled, _hold.IsHeld(CurrentLanguage), isCurrent, run.FreshInstall, _settings.Language is not null, run.PackId, cancelled && !SameLanguage(run.Language, CurrentLanguage));
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
                CancelRun(run.Voice!); // the notification's Cancel reached a run whose pack id was not matched: still final, no error state
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
        Publish(Volatile.Read(ref _latestId), PackInstallState.Failed);
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
        if (InstalledVoiceCatalog.Resolve(_packs.GetInstalledVoiceAndStorytellerPacks(), CurrentLanguage) is not null)
        {
            await ActivateInstalledAsync(run, endsRun: true);
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

        PackDownloadResult downloaded = await DownloadPackAsync(pack, run);
        if (downloaded != PackDownloadResult.Ok)
        {
            return ToState(downloaded);
        }

        run.Voice!.FreshInstall = true;
        await ActivateInstalledAsync(run, endsRun: true);
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
        if (run.Voice is { } voice)
        {
            Volatile.Write(ref voice.PackId, pack.Id);
        }

        Publish(run.Generation, PackInstallState.Downloading, 0);
        var progress = new InlineProgress(value => PublishProgress(run, value));
        return await _packs.DownloadAsync(pack.Id, progress, run.Token);
    }

    // endsRun: the main run ends Ready here, so a Cancel landing after this point is ignored (Slice 186); the hold
    // itself is lifted by every Ready publish (Apply).
    private async Task ActivateInstalledAsync(Run run, bool endsRun = false)
    {
        await _model.InitializeAsync();
        if (endsRun)
        {
            lock (_gate)
            {
                run.Voice!.Ready = true;
            }
        }

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
        if (generation != Volatile.Read(ref _latestId))
        {
            return;
        }

        if (state == PackInstallState.Ready)
        {
            lock (_gate)
            {
                _hold.Release(); // the voice pack is there, so a scan pack held back for it may follow
            }
        }

        State = state;
        Progress = progress;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
