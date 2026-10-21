using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Settings;

namespace ReadTheStupidText.Application.Translation;

/// <summary>
/// The translate-before-read step shared by every Windows read path (Slice 57): resolves the source
/// language (the user's pick, else detection), checks the pair and the pack, and translates within a
/// time budget. Any problem returns the original text with the reason, so a read is never blocked by
/// translation. The text never leaves the device. With an installer (Slice 75) a missing pack is
/// downloaded in the background and the original is read this time.
/// </summary>
public sealed class ReadTranslator
{
    /// <summary>How long a read waits for its translation before the original is spoken instead.</summary>
    public static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(20);

    private const string EnglishCode = "en";

    private readonly ITranslator _translator;
    private readonly ILanguageDetector _detector;
    private readonly ISettingsStore _settings;
    private readonly ISystemLog _log;
    private readonly TranslationPackInstaller? _packs;

    public ReadTranslator(
        ITranslator translator, ILanguageDetector detector, ISettingsStore settings, ISystemLog log,
        TranslationPackInstaller? packs = null)
    {
        _translator = translator;
        _detector = detector;
        _settings = settings;
        _log = log;
        _packs = packs;
    }

    /// <summary>Whether the underlying engine can translate at all in this build.</summary>
    public bool IsAvailable => _translator.IsAvailable;

    /// <summary>The time budget in effect (per block when streaming); tests shorten it.</summary>
    public TimeSpan Budget { get; set; } = DefaultBudget;

    /// <summary>Raised as translation packs download (Slice 75), for the panel's note.</summary>
    public event EventHandler<TranslationPackProgress>? PackProgress
    {
        add
        {
            if (_packs is not null)
            {
                _packs.Progress += value;
            }
        }
        remove
        {
            if (_packs is not null)
            {
                _packs.Progress -= value;
            }
        }
    }

    /// <summary>
    /// Starts fetching the pack(s) that translate English into the user's language, when the translate
    /// toggle is turned on (Slice 75). Nothing happens for an English target (the packs depend on the source
    /// of each read) or a language with no model.
    /// </summary>
    public void StartTargetPackDownload()
    {
        string? target = TranslationFlow.NormalizeTag(_settings.Language);
        if (_packs is not null && target is not null && target != EnglishCode && _translator.SupportsPair(EnglishCode, target))
        {
            _ = Task.Run(() => _packs.StartInBackground(EnglishCode, target)); // the installed-pack check reads the disk: not on the UI thread
        }
    }

    /// <summary>
    /// The text to speak for <paramref name="text"/>. Only the caller's own token being cancelled
    /// throws; a timeout or failure is an outcome.
    /// </summary>
    public async Task<ReadTranslationResult> PrepareAsync(string text, int activityId, CancellationToken cancellationToken)
    {
        string? target = TranslationFlow.NormalizeTag(_settings.Language);
        if (!_settings.TranslateEnabled || target is null)
        {
            return Original(text, TranslationOutcome.NotRequested, null, target);
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(Budget);
        try
        {
            return await TranslateAsync(text, target, activityId, budget.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _log.Warning("translation timed out; reading the original", activityId);
            return Original(text, TranslationOutcome.TimedOut, null, target);
        }
        catch (TranslationModelUnavailableException ex)
        {
            _log.Warning("translation pack unavailable; reading the original", activityId, ex);
            return Original(text, TranslationOutcome.ModelMissing, null, target);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Warning("translation failed; reading the original", activityId, ex);
            return Original(text, TranslationOutcome.Failed, null, target);
        }
    }

    /// <summary>
    /// Starts a streamed translation (Slice 75): null when the read should use <see cref="PrepareAsync"/>
    /// instead (toggle off, or a text of a single block); otherwise either a terminal result (nothing to
    /// translate: read the original) or a stream of translated blocks to speak as they arrive.
    /// </summary>
    public async Task<TranslationStreamStart?> PlanStreamAsync(string text, int activityId, CancellationToken cancellationToken)
    {
        string? target = TranslationFlow.NormalizeTag(_settings.Language);
        if (!_settings.TranslateEnabled || target is null)
        {
            return null;
        }

        IReadOnlyList<string> blocks = TranslationBlocks.Plan(text);
        if (blocks.Count < 2)
        {
            return null;
        }

        (ReadTranslationResult? terminal, string? source) = await ResolveAsync(text, target, cancellationToken);
        if (terminal is not null)
        {
            return new TranslationStreamStart(terminal, null);
        }

        _log.Info($"translating {source} -> {target} in {blocks.Count} blocks", activityId);
        return new TranslationStreamStart(null, new TranslationStream(_translator, _log, source!, target, blocks, Budget, activityId));
    }

    private async Task<ReadTranslationResult> TranslateAsync(string text, string target, int activityId, CancellationToken token)
    {
        (ReadTranslationResult? terminal, string? source) = await ResolveAsync(text, target, token);
        if (terminal is not null)
        {
            return terminal;
        }

        string translated = await TranslationFlow.TranslateAsync(_translator, text, source!, target, token);
        if (string.IsNullOrWhiteSpace(translated))
        {
            return Original(text, TranslationOutcome.Failed, source, target);
        }

        _log.Info($"translated {source} -> {target}, {text.Length} -> {translated.Length} chars", activityId);
        return new ReadTranslationResult(translated, TranslationOutcome.Translated, source, target);
    }

    // The source language and whether the pair can be translated right now. A terminal result means
    // "read the original" and says why; otherwise the source is set.
    private async Task<(ReadTranslationResult? Terminal, string? Source)> ResolveAsync(string text, string target, CancellationToken token)
    {
        string? source = await ResolveSourceAsync(text, token);
        if (source is null)
        {
            return (Original(text, TranslationOutcome.SourceUnknown, null, target), null);
        }

        if (!TranslationFlow.ShouldTranslate(true, source, target))
        {
            return (Original(text, TranslationOutcome.SameLanguage, source, target), null);
        }

        if (!_translator.SupportsPair(source, target))
        {
            return (Original(text, TranslationOutcome.PairUnsupported, source, target), null);
        }

        if (!await _translator.IsModelReadyAsync(source, target))
        {
            bool downloading = _packs?.StartInBackground(source, target) == true;
            TranslationOutcome outcome = downloading ? TranslationOutcome.PackDownloading : TranslationOutcome.ModelMissing;
            return (Original(text, outcome, source, target), null);
        }

        return (null, source);
    }

    private async Task<string?> ResolveSourceAsync(string text, CancellationToken token)
    {
        string? chosen = TranslationFlow.NormalizeTag(_settings.TranslateSourceLanguage);
        if (chosen is not null)
        {
            return chosen;
        }

        DetectedLanguage? detected = await _detector.DetectAsync(TranslationFlow.DetectionSample(text), token);
        return TranslationFlow.SourceFromDetection(detected);
    }

    private static ReadTranslationResult Original(string text, TranslationOutcome outcome, string? source, string? target) =>
        new(text, outcome, source, target);
}

/// <summary>What <see cref="ReadTranslator.PlanStreamAsync"/> decided: read the original (<see cref="Terminal"/>) or speak <see cref="Stream"/>.</summary>
public sealed record TranslationStreamStart(ReadTranslationResult? Terminal, TranslationStream? Stream);

/// <summary>
/// The blocks of one read, translated one after another with a time budget for each (Slice 75). When a
/// block fails or times out, that block and every later one are returned as the original text, so the
/// rest of the read is spoken untranslated rather than dropped; <see cref="Outcome"/> says why.
/// </summary>
public sealed class TranslationStream
{
    private readonly ITranslator _translator;
    private readonly ISystemLog _log;
    private readonly IReadOnlyList<string> _blocks;
    private readonly TimeSpan _budget;
    private readonly int _activityId;

    internal TranslationStream(
        ITranslator translator, ISystemLog log, string source, string target,
        IReadOnlyList<string> blocks, TimeSpan budget, int activityId)
    {
        _translator = translator;
        _log = log;
        Source = source;
        Target = target;
        _blocks = blocks;
        _budget = budget;
        _activityId = activityId;
    }

    public string Source { get; }

    public string Target { get; }

    /// <summary>The number of blocks the text was split into.</summary>
    public int BlockCount => _blocks.Count;

    /// <summary><see cref="TranslationOutcome.Translated"/> while every block so far translated, else why the rest is original.</summary>
    public TranslationOutcome Outcome { get; private set; } = TranslationOutcome.Translated;

    /// <summary>The text to speak for each block, in order. Only the caller's token being cancelled throws.</summary>
    public async IAsyncEnumerable<string> BlocksAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (string block in _blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? translated = Outcome == TranslationOutcome.Translated
                ? await TryTranslateAsync(block, cancellationToken)
                : null;
            yield return translated ?? block;
        }
    }

    private async Task<string?> TryTranslateAsync(string block, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_budget);
        try
        {
            string translated = (await _translator.TranslateAsync(block, Source, Target, budget.Token)).Trim();
            return translated.Length > 0 ? translated : Fail(TranslationOutcome.Failed, "returned nothing", null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(TranslationOutcome.TimedOut, "timed out", null);
        }
        catch (TranslationModelUnavailableException ex)
        {
            return Fail(TranslationOutcome.ModelMissing, "pack unavailable", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Fail(TranslationOutcome.Failed, "failed", ex);
        }
    }

    private string? Fail(TranslationOutcome outcome, string why, Exception? error)
    {
        Outcome = outcome;
        _log.Warning($"translation block {why}; reading the rest in the original", _activityId, error);
        return null;
    }
}
