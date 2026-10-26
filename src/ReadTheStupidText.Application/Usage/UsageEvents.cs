using System.Globalization;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Activity;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Usage;

/// <summary>What every count carries besides its own values: where it came from, when, and at which sample rate.</summary>
public sealed record UsageContext(string Version, string Platform, double SampleRate, DateTimeOffset Now);

/// <summary>
/// Builds the phone's counts (Slice 137). A factory returns null when a language it needs is not a plain language code (or the OS version is out of range), so a
/// count is either complete and closed-list or not made at all.
/// </summary>
public static class UsageEvents
{
    private const string DayFormat = "yyyy-MM-dd";
    private const int MinLanguageLetters = 2;
    private const int MaxLanguageLetters = 3;
    private const int MinOsMajor = 1;
    private const int MaxOsMajor = 99;
    private static readonly char[] RegionSeparators = ['-', '_'];

    /// <summary>The app was opened; <paramref name="uiLanguage"/> is the language of its words.</summary>
    public static UsageEvent? AppOpened(UsageContext context, string? uiLanguage, int osMajor) =>
        Language(uiLanguage) is { } language && osMajor is >= MinOsMajor and <= MaxOsMajor
            ? Make(context, UsageEventName.AppOpened) with { Language = language, OsMajor = osMajor }
            : null;

    /// <summary>A read started: the text's detected language (null when unsure), the language it is read in, and which voice reads it.</summary>
    public static UsageEvent? ReadStarted(UsageContext context, string? sourceLanguage, string? readLanguage, bool translated, string engine, bool voiceDefault, int textLength, string? source = null) =>
        Language(readLanguage) is { } language
            ? Make(context, UsageEventName.ReadStarted) with
            {
                Language = language,
                SourceLanguage = Language(sourceLanguage),
                Mode = translated ? UsageMode.Translation : UsageMode.Original,
                Engine = engine,
                VoiceDefault = voiceDefault,
                LengthBucket = UsageBuckets.TextLength(textLength),
                Source = source,
            }
            : null;

    /// <summary>The review page offered to translate a text of <paramref name="source"/> into <paramref name="target"/>.</summary>
    public static UsageEvent? TranslateOffered(UsageContext context, string? source, string? target) => Pair(context, UsageEventName.TranslateOffered, source, target);

    /// <summary>The offer was taken: the checkbox was ticked when Read was tapped.</summary>
    public static UsageEvent? TranslateChosen(UsageContext context, string? source, string? target) => Pair(context, UsageEventName.TranslateChosen, source, target);

    /// <summary>
    /// The translate switch was turned on (Windows, Slice 140), into <paramref name="target"/>. The text's language is not known yet, so
    /// <paramref name="source"/> is only the one the person picked, else null.
    /// </summary>
    public static UsageEvent? TranslateSwitchedOn(UsageContext context, string? source, string? target) =>
        Language(target) is { } to
            ? Make(context, UsageEventName.TranslateChosen) with { SourceLanguage = Language(source), Language = to }
            : null;

    /// <summary>A voice was picked for <paramref name="language"/>.</summary>
    public static UsageEvent? VoiceChanged(UsageContext context, string? language, string engine) =>
        Language(language) is { } code
            ? Make(context, UsageEventName.VoiceChanged) with { Language = code, Engine = engine }
            : null;

    /// <summary>A timed wait. The language pair is optional (a model download or the first sound may not know both).</summary>
    public static UsageEvent Timing(UsageContext context, string stage, string outcome, TimeSpan elapsed, string? sourceLanguage = null, string? language = null, bool early = false) =>
        Make(context, UsageEventName.Timing) with
        {
            Stage = stage,
            Outcome = outcome,
            DurationBucket = UsageBuckets.Duration(elapsed),
            Early = early ? true : null,
            SourceLanguage = Language(sourceLanguage),
            Language = Language(language),
        };

    /// <summary>The primary language code in lower case ("fa" for "fa-IR"), or null when <paramref name="code"/> is not a 2-3 letter language.</summary>
    public static string? Language(string? code)
    {
        string primary = (code ?? string.Empty).Trim().Split(RegionSeparators)[0].ToLowerInvariant();
        bool valid = primary.Length is >= MinLanguageLetters and <= MaxLanguageLetters && primary.All(c => c is >= 'a' and <= 'z');
        return valid ? primary : null;
    }

    /// <summary>The count's engine name for a read or a voice choice: the phone's own voice, else the pack's engine.</summary>
    public static string EngineFor(bool phoneVoice, VoiceEngineKind? kind) => phoneVoice ? UsageEngine.Phone : kind switch
    {
        VoiceEngineKind.Supertonic => UsageEngine.Supertonic,
        VoiceEngineKind.Vits => UsageEngine.Piper,
        VoiceEngineKind.Melo => UsageEngine.Melo,
        VoiceEngineKind.Kokoro => UsageEngine.Kokoro,
        _ => UsageEngine.Phone,
    };

    /// <summary>The count's engine name for a Windows voice: the Windows speech voice (null, or the "system" entry), else the pack's engine.</summary>
    public static string EngineOf(VoiceInfo? voice) =>
        voice is null || voice.Id == SystemVoice.Id ? UsageEngine.Windows : EngineFor(false, voice.Engine);

    /// <summary>What started a read, as the closed <see cref="UsageSource"/> value.</summary>
    public static string SourceOf(ActivityTrigger trigger) => trigger switch
    {
        ActivityTrigger.AutoRead => UsageSource.Selection,
        ActivityTrigger.Clipboard => UsageSource.Clipboard,
        ActivityTrigger.Hotkey => UsageSource.Hotkey,
        ActivityTrigger.Manual => UsageSource.Panel,
        ActivityTrigger.FileUpload => UsageSource.Panel, // the closed set has no "file": a picked file is read from the panel's or tray's file picker
        _ => UsageSource.Panel,
    };

    /// <summary>True while the person never picked a voice (nothing stored) or picked the one the model starts with.</summary>
    public static bool VoiceIsDefault(string? storedVoiceId, string? modelDefaultVoiceId) =>
        storedVoiceId is null || storedVoiceId == modelDefaultVoiceId;

    private static UsageEvent? Pair(UsageContext context, string name, string? source, string? target) =>
        Language(source) is { } from && Language(target) is { } to
            ? Make(context, name) with { SourceLanguage = from, Language = to }
            : null;

    private static UsageEvent Make(UsageContext context, string name) =>
        new(name, context.Version, context.Platform, context.Now.UtcDateTime.ToString(DayFormat, CultureInfo.InvariantCulture), context.SampleRate);
}
