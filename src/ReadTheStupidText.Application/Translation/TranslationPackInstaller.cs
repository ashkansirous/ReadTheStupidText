using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Application.Translation;

/// <summary>How far a translation pack download for one language pair has got (Slice 75).</summary>
public enum TranslationPackState
{
    Downloading,
    Ready,
    Failed,
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
    private readonly object _gate = new();
    private readonly Dictionary<string, Task> _running = new();

    public TranslationPackInstaller(IVoicePackService packs, ITranslator translator, ISystemLog log)
    {
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
        try
        {
            Report(source, target, TranslationPackState.Downloading, 0);
            PackDownloadResult result = await EnsureAsync(source, target, CancellationToken.None).ConfigureAwait(false);
            _log.Info($"translation packs {source}>{target}: {result}");
            Progress?.Invoke(this, result == PackDownloadResult.Ok
                ? new TranslationPackProgress(source, target, TranslationPackState.Ready, 1)
                : new TranslationPackProgress(source, target, TranslationPackState.Failed, 0, result));
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
    }

    private void Report(string source, string target, TranslationPackState state, double fraction) =>
        Progress?.Invoke(this, new TranslationPackProgress(source, target, state, fraction));
}
