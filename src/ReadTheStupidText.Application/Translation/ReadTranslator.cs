using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Settings;

namespace ReadTheStupidText.Application.Translation;

/// <summary>
/// The translate-before-read step shared by every Windows read path (Slice 57): resolves the source
/// language (the user's pick, else detection), checks the pair and the pack, and translates within a
/// time budget. Any problem returns the original text with the reason, so a read is never blocked by
/// translation. The text never leaves the device.
/// </summary>
public sealed class ReadTranslator
{
    /// <summary>How long a read waits for its translation before the original is spoken instead.</summary>
    public static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(20);

    private readonly ITranslator _translator;
    private readonly ILanguageDetector _detector;
    private readonly ISettingsStore _settings;
    private readonly ISystemLog _log;

    public ReadTranslator(ITranslator translator, ILanguageDetector detector, ISettingsStore settings, ISystemLog log)
    {
        _translator = translator;
        _detector = detector;
        _settings = settings;
        _log = log;
    }

    /// <summary>Whether the underlying engine can translate at all in this build.</summary>
    public bool IsAvailable => _translator.IsAvailable;

    /// <summary>The time budget in effect; tests shorten it.</summary>
    public TimeSpan Budget { get; set; } = DefaultBudget;

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

    private async Task<ReadTranslationResult> TranslateAsync(string text, string target, int activityId, CancellationToken token)
    {
        string? source = await ResolveSourceAsync(text, token);
        if (source is null)
        {
            return Original(text, TranslationOutcome.SourceUnknown, null, target);
        }

        if (!TranslationFlow.ShouldTranslate(true, source, target))
        {
            return Original(text, TranslationOutcome.SameLanguage, source, target);
        }

        if (!TranslationFlow.IsPairSupported(_translator.SupportedLanguages.ToArray(), source, target))
        {
            return Original(text, TranslationOutcome.PairUnsupported, source, target);
        }

        if (!await _translator.IsModelReadyAsync(source, target))
        {
            return Original(text, TranslationOutcome.ModelMissing, source, target);
        }

        string translated = await TranslationFlow.TranslateAsync(_translator, text, source, target, token);
        if (string.IsNullOrWhiteSpace(translated))
        {
            return Original(text, TranslationOutcome.Failed, source, target);
        }

        _log.Info($"translated {source} -> {target}, {text.Length} -> {translated.Length} chars", activityId);
        return new ReadTranslationResult(translated, TranslationOutcome.Translated, source, target);
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
