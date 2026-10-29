using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Application.Translation;

/// <summary>How far a translation pack download for one language pair has got (Slice 75).</summary>
public enum TranslationPackState
{
    Downloading,
    Ready,
    Failed,

    /// <summary>The user cancelled the download from its notification (Slice 183): not an error, nothing to show.</summary>
    Idle,
}

/// <summary>Progress of the packs one pair needs: <see cref="Fraction"/> is 0..1 over all of them.</summary>
public sealed record TranslationPackProgress(string Source, string Target, TranslationPackState State, double Fraction, PackDownloadResult? Failure = null);

/// <summary>
/// Downloads the translation packs a language pair needs through the pack service (Slice 75): one per
/// leg, English as the go-between. Turning the translate toggle on fetches the user's own direction;
/// a read whose detected source needs another leg starts that download in the background while the
/// original is read. One download per pair runs at a time. Progress is reported for the UI note.
/// </summary>
public sealed class TranslationPackInstaller
{
    private readonly IVoicePackService _packs;
    private readonly ITranslator _translator;
    private readonly ISystemLog _log;
    private readonly IPackResume? _resume;
    private readonly object _gate = new();
    private readonly Dictionary<string, Task> _running = new();

    public TranslationPackInstaller(IVoicePackService packs, ITranslator translator, ISystemLog log, IPackResume? resume = null)
    {
        _resume = resume;
        _packs = packs;
        _translator = translator;
        _log = log;
    }

    /// <summary>Raised as a pair's packs download, and when they finish or fail. May fire on any thread.</summary>
    public event EventHandler<TranslationPackProgress>? Progress;

    /// <summary>The ids of the packs for the pair that are not installed yet.</summary>
    public IReadOnlyList<string> MissingPackIds(string source, string target) =>
        _translator.RequiredPackIds(source, target).Where(id => _packs.GetInstalledPath(id) is null).ToList();

    /// <summary>
    /// Starts downloading what the pair is missing, unless that is nothing, the pair has no model, or the
    /// same download is already running. True when a download is running for the pair afterwards.
    /// </summary>
    public bool StartInBackground(string source, string target)
    {
        string key = $"{source}>{target}";
        lock (_gate)
        {
            if (_running.ContainsKey(key))
            {
                return true;
            }
        }

        // Disk I/O (the installed-pack check), so it runs outside the lock.
        if (MissingPackIds(source, target).Count == 0)
        {
            return false;
        }

        lock (_gate)
        {
            if (!_running.ContainsKey(key))
            {
                _running[key] = Task.Run(() => RunAsync(key, source, target));
            }
        }

        return true;
    }

    /// <summary>Downloads every missing pack of the pair, one after another; stops at the first failure.</summary>
    public async Task<PackDownloadResult> EnsureAsync(string source, string target, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> missing = MissingPackIds(source, target);
        for (int i = 0; i < missing.Count; i++)
        {
            int done = i;
            var progress = new InlineProgress(p => Report(source, target, TranslationPackState.Downloading, (done + p) / missing.Count));
            PackDownloadResult result = await _packs.DownloadAsync(missing[i], progress, cancellationToken).ConfigureAwait(false);
            if (result != PackDownloadResult.Ok)
            {
                return result;
            }
        }

        return PackDownloadResult.Ok;
    }

    private async Task RunAsync(string key, string source, string target)
    {
        PackDownloadResult outcome = PackDownloadResult.Failed;
        bool cancelled = false;
        try
        {
            Report(source, target, TranslationPackState.Downloading, 0);
            PackDownloadResult result = await EnsureAsync(source, target, CancellationToken.None).ConfigureAwait(false);
            outcome = result;
            _log.Info($"translation packs {source}>{target}: {result}");
            Progress?.Invoke(this, result == PackDownloadResult.Ok
                ? new TranslationPackProgress(source, target, TranslationPackState.Ready, 1)
                : new TranslationPackProgress(source, target, TranslationPackState.Failed, 0, result));
        }
        catch (PackCancelledByNotificationException)
        {
            // The user's Cancel on the download notification (Slice 183) is final; any other cancellation falls to the generic catch and stays retryable.
            cancelled = true;
            _log.Info($"translation packs {source}>{target}: cancelled");
            Progress?.Invoke(this, new TranslationPackProgress(source, target, TranslationPackState.Idle, 0));
        }
        catch (Exception ex)
        {
            _log.Warning($"translation packs {source}>{target} failed", null, ex);
            Progress?.Invoke(this, new TranslationPackProgress(source, target, TranslationPackState.Failed, 0, PackDownloadResult.Failed));
        }
        finally
        {
            lock (_gate)
            {
                _running.Remove(key);
            }
        }

        if (cancelled)
        {
            _resume?.Report($"translation:{key}", PackRunEnd.Cancelled, () => Task.FromResult(false));
            return;
        }

        ReportEnd(key, source, target, outcome);
    }

    // Slice 182: the run reports its end once; a pair that stopped Offline or Failed starts again by itself when the network is back.
    private void ReportEnd(string key, string source, string target, PackDownloadResult outcome) =>
        _resume?.Report($"translation:{key}", IPackResume.EndOf(outcome), () => Task.FromResult(StartedNewRun(source, target)));

    // False when the same pair already runs (that run reports its own end) or nothing is missing (reported Finished, so the entry leaves).
    private bool StartedNewRun(string source, string target)
    {
        string key = $"{source}>{target}";
        lock (_gate)
        {
            if (_running.ContainsKey(key))
            {
                return false;
            }
        }

        if (MissingPackIds(source, target).Count == 0)
        {
            _resume?.Report($"translation:{key}", PackRunEnd.Finished, () => Task.FromResult(false));
            return false;
        }

        return StartInBackground(source, target);
    }

    private void Report(string source, string target, TranslationPackState state, double fraction) =>
        Progress?.Invoke(this, new TranslationPackProgress(source, target, state, fraction));
}
