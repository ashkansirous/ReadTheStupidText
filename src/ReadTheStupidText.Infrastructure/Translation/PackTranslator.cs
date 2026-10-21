using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Infrastructure.Translation;

/// <summary>One loaded translation leg: turns one sentence of its source language into its target language.</summary>
public interface ILegTranslator : IDisposable
{
    string Translate(string sentence, CancellationToken cancellationToken);
}

/// <summary>
/// The Windows <see cref="ITranslator"/> (Slices 57 and 75): translation runs offline from Opus-MT packs
/// installed under the packs folder (<see cref="TranslationPacks"/>), pivoting through English. Text is
/// split into sentences (<see cref="TranslationSentences"/>) and translated in reading order, so the first
/// sentences come out first; a pair whose packs are missing or unreadable raises
/// <see cref="TranslationModelUnavailableException"/> and the read falls back to the original text.
/// The model work runs on the thread pool, one translation at a time, with the two most recently used
/// legs kept loaded.
/// </summary>
public sealed class PackTranslator : ITranslator, IDisposable
{
    private const int LoadedLegs = 2;
    private static readonly TimeSpan DisposeWait = TimeSpan.FromSeconds(5);
    private const string SpacedJoin = " ";
    private const string CompactJoin = "";
    private static readonly HashSet<string> UnspacedLanguages = ["zh", "ja"];

    private readonly Func<string, string?> _installedPath;
    private readonly Func<string?> _targetLanguage;
    private readonly Func<string, ILegTranslator> _loadLeg;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<LoadedLeg> _loaded = [];
    private readonly CancellationTokenSource _closing = new();

    /// <param name="packsRoot">The folder translation packs install into.</param>
    /// <param name="targetLanguage">The user's language, the translation target; null leaves the engine available whenever any pack is installed.</param>
    public PackTranslator(Func<string> packsRoot, Func<string?>? targetLanguage = null)
        : this(packsRoot, targetLanguage, folder => new MarianLegTranslator(MarianEngine.Load(folder)))
    {
    }

    /// <summary>
    /// The app's translator: "installed" is the pack service's validated lookup (pack id to its folder when the
    /// pack is complete, else null), the same one the installer uses, so an incomplete pack is re-downloaded.
    /// </summary>
    public static PackTranslator WithInstalledLookup(Func<string> packsRoot, Func<string?> targetLanguage, Func<string, string?> installedPath) =>
        new(packsRoot, targetLanguage, folder => new MarianLegTranslator(MarianEngine.Load(folder)), installedPath);

    public PackTranslator(
        Func<string> packsRoot, Func<string?>? targetLanguage, Func<string, ILegTranslator> loadLeg, Func<string, string?>? installedPath = null)
    {
        _installedPath = installedPath ?? TranslationPacks.FolderLookup(packsRoot);
        _targetLanguage = targetLanguage ?? (() => null);
        _loadLeg = loadLeg;
    }

    /// <summary>
    /// True when the engine can serve the user's language (Slice 75): some pack leg reaches it, whether or
    /// not that pack is installed yet, since turning translation on downloads it. With no target language
    /// known, true once any pack is installed.
    /// </summary>
    public bool IsAvailable => TranslationPacks.CanServeTarget(TranslationFlow.NormalizeTag(_targetLanguage())) ?? TranslationPacks.AnyInstalled(_installedPath);

    public IReadOnlyList<string> SupportedLanguages => TranslationPacks.Languages;

    public bool SupportsPair(string source, string target) => TranslationPacks.IsPairSupported(source, target);

    public IReadOnlyList<string> RequiredPackIds(string source, string target) => TranslationPacks.RequiredPackIds(source, target);

    public Task<bool> IsModelReadyAsync(string source, string target) =>
        Task.FromResult(SupportsPair(source, target) && TranslationPacks.IsInstalled(_installedPath, source, target));

    public async Task<string> TranslateAsync(string text, string source, string target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!SupportsPair(source, target))
        {
            throw new TranslationModelUnavailableException($"No translation model covers {source} to {target}.");
        }

        if (!TranslationPacks.IsInstalled(_installedPath, source, target))
        {
            throw new TranslationModelUnavailableException($"The translation pack for {source} to {target} is not installed.");
        }

        // Closing the translator (app exit) cancels the translation in flight, so its sessions can be released.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _closing.Token);
        CancellationToken token = linked.Token;
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => TranslateThroughLegs(text, source, target, token), token).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Cancels the translation in flight, waits (briefly) for it to leave the model, then releases every
    // loaded session. If it does not leave in time the sessions are left to the process exit rather than
    // disposed under a running inference.
    public void Dispose()
    {
        _closing.Cancel();
        if (!_gate.Wait(DisposeWait))
        {
            return;
        }

        foreach (LoadedLeg loaded in _loaded)
        {
            loaded.Leg.Dispose();
        }

        _loaded.Clear();
        _closing.Dispose();
        _gate.Dispose();
    }

    private string TranslateThroughLegs(string text, string source, string target, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> sentences = TranslationSentences.Split(text);
        foreach ((string legSource, string legTarget) in TranslationPacks.Legs(source, target))
        {
            ILegTranslator leg = LegFor(legSource, legTarget);
            sentences = TranslateAll(leg, sentences, cancellationToken);
        }

        return string.Join(UnspacedLanguages.Contains(target.ToLowerInvariant()) ? CompactJoin : SpacedJoin, sentences);
    }

    private static List<string> TranslateAll(ILegTranslator leg, IReadOnlyList<string> sentences, CancellationToken cancellationToken)
    {
        var translated = new List<string>(sentences.Count);
        foreach (string sentence in sentences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string result = leg.Translate(sentence, cancellationToken).Trim();
            if (result.Length > 0)
            {
                translated.Add(result);
            }
        }

        return translated;
    }

    // Most recently used legs stay loaded (a pivot read needs two); the oldest is released first. A leg
    // whose pack was replaced on disk (an update) is released and loaded again.
    private ILegTranslator LegFor(string source, string target)
    {
        string packId = TranslationPacks.PackId(source, target);
        DateTime stamp = InstalledStamp(packId);
        int index = _loaded.FindIndex(l => l.PackId == packId);
        if (index >= 0)
        {
            LoadedLeg hit = _loaded[index];
            _loaded.RemoveAt(index);
            if (hit.Stamp == stamp)
            {
                _loaded.Add(hit);
                return hit.Leg;
            }

            hit.Leg.Dispose();
        }

        ILegTranslator leg = Load(packId, source, target);
        _loaded.Add(new LoadedLeg(packId, leg, stamp));
        while (_loaded.Count > LoadedLegs)
        {
            _loaded[0].Leg.Dispose();
            _loaded.RemoveAt(0);
        }

        return leg;
    }

    // When the installed pack's spec file was written: a replaced pack has a new one.
    private DateTime InstalledStamp(string packId) =>
        _installedPath(packId) is { } folder ? File.GetLastWriteTimeUtc(Path.Combine(folder, TranslationPackFiles.Spec)) : DateTime.MinValue;

    private sealed record LoadedLeg(string PackId, ILegTranslator Leg, DateTime Stamp);

    private ILegTranslator Load(string packId, string source, string target)
    {
        try
        {
            string folder = _installedPath(packId) ?? throw new IOException($"The translation pack {packId} is not installed.");
            return _loadLeg(folder);
        }
        catch (Exception ex) when (ex is IOException or FormatException or UnauthorizedAccessException or Microsoft.ML.OnnxRuntime.OnnxRuntimeException or DllNotFoundException)
        {
            throw new TranslationModelUnavailableException($"The translation pack {packId} could not be loaded.", ex);
        }
    }
}

/// <summary>Adapts a <see cref="MarianEngine"/> to <see cref="ILegTranslator"/>.</summary>
internal sealed class MarianLegTranslator(MarianEngine engine) : ILegTranslator
{
    public string Translate(string sentence, CancellationToken cancellationToken) => engine.Translate(sentence, cancellationToken);

    public void Dispose() => engine.Dispose();
}
