using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Application.Reading;

/// <summary>The voice-picker row that is highlighted.</summary>
public enum PickerSelection
{
    None,
    Phone,
    Natural,
}

/// <summary>What Play does about the voice for the text it is about to read (Slice 82).</summary>
public enum VoiceRoute
{
    Read,
    Downloading,
    NoVoice,
}

/// <summary>
/// The pure routing rules of Slice 82 (Decision 89): which language a read is in, which voice
/// reads it, and what to tell the user when none can. An unknown phone (null) never blocks.
/// </summary>
public static class VoiceRouting
{
    // The main language for a script the app language does not write in (Slice 90). The app language wins
    // when it uses the script itself, so Arabic script reads as Farsi in a Farsi app and Arabic otherwise.
    private const string ChineseCode = "zh";
    private const string JapaneseCode = "ja";
    private const string EnglishCode = "en";
    private const char KanaFrom = '぀';
    private const char KanaTo = 'ヿ';

    private static readonly IReadOnlyDictionary<TextScript, string> MainLanguageByScript = new Dictionary<TextScript, string>
    {
        [TextScript.Latin] = "en",
        [TextScript.Cyrillic] = "ru",
        [TextScript.Arabic] = "ar",
        [TextScript.Cjk] = ChineseCode,
        [TextScript.Hangul] = "ko",
        [TextScript.Hebrew] = "he",
        [TextScript.Devanagari] = "hi",
        [TextScript.Greek] = "el",
    };

    /// <summary>
    /// The language a read is spoken in (Slice 90, replacing Slice 82's app-language rule): the
    /// translation target (the app language) when the text was translated; otherwise the text's own
    /// language (the source picker's choice, else the detected one); when that is unsure, a guess from
    /// the text's script (<see cref="LanguageForScript"/>). Mixed text is read in one language.
    /// </summary>
    public static string ReadingLanguage(bool translated, string targetLanguage, string? sourceChoice, string? detectedLanguage, string text)
    {
        if (translated)
        {
            return targetLanguage;
        }

        string? own = FirstKnown(sourceChoice, detectedLanguage);
        return own is null ? LanguageForScript(text, targetLanguage) : VoiceAvailability.Primary(own);
    }

    /// <summary>
    /// The language for text whose language is unsure: the app language when the text's script is the
    /// app language's script (or has no letters, such as numbers and URLs); otherwise the main language
    /// of that script.
    /// </summary>
    public static string LanguageForScript(string text, string appLanguage)
    {
        string sample = TranslationFlow.DetectionSample(text);
        if (ScriptDetector.Analyze([sample]).Dominant is not { } found)
        {
            return appLanguage;
        }

        return found == TextScript.Cjk ? CjkLanguage(sample, appLanguage) : MainLanguage(found, appLanguage);
    }

    // Kana means Japanese; Han alone is Chinese, or the app language when that is Japanese (kanji-only text).
    private static string CjkLanguage(string sample, string appLanguage)
    {
        if (sample.Any(c => c >= KanaFrom && c <= KanaTo))
        {
            return JapaneseCode;
        }

        string app = VoiceAvailability.Primary(appLanguage);
        return app is ChineseCode or JapaneseCode ? app : ChineseCode;
    }

    // The app language when it writes in the script; else the script's main language; a script the table
    // lacks falls back to English (Latin) because the app language does not write in it.
    private static string MainLanguage(TextScript script, string appLanguage) =>
        ScriptDetector.ScriptOf(VoiceAvailability.Primary(appLanguage)) == script
            ? appLanguage
            : MainLanguageByScript.GetValueOrDefault(script, EnglishCode);

    /// <summary>
    /// Whether a read needs its own voice scope (Slice 90): only a read in a language other than the app's.
    /// An app-language read takes the ordinary neural path, head start included.
    /// </summary>
    public static bool UsesScope(bool appLanguage) => !appLanguage;

    /// <summary>
    /// Whether an installed natural pack can read this language: the app language's pack always (the
    /// ordinary path), another language's only where the platform's neural reader takes a pack per read.
    /// </summary>
    public static bool NaturalPackUsable(bool appLanguage, bool packInstalled, bool scopedReadsSupported) =>
        packInstalled && (appLanguage || scopedReadsSupported);

    /// <summary>
    /// Whether the pack being downloaded is the reading language's: the installer fetches the app
    /// language's pack only, so a download never promises a voice for another language.
    /// </summary>
    public static bool DownloadingFor(bool installerBusy, string? installerLanguage, string readingLanguage) =>
        installerBusy && installerLanguage is not null
        && string.Equals(VoiceAvailability.Primary(installerLanguage), VoiceAvailability.Primary(readingLanguage), StringComparison.OrdinalIgnoreCase);

    private static string? FirstKnown(params string?[] codes) =>
        codes.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

    /// <summary>
    /// Whether the stored "Phone voice" choice must give way to the natural voice: the phone
    /// can't read the language (known) and a pack is ready to.
    /// </summary>
    public static bool ShouldLeavePhoneVoice(bool phoneVoiceChosen, bool packReady, bool? phoneCanRead) =>
        phoneVoiceChosen && packReady && phoneCanRead == false;

    /// <summary>
    /// Which row the voice picker highlights (Slice 164): without a pack, the Phone voice row if shown,
    /// else none; with one, the Phone voice only while it is both shown and the stored choice, else the
    /// natural voice that reads instead.
    /// </summary>
    public static PickerSelection SelectedRow(bool packInstalled, bool phoneRowShown, bool phoneVoiceStored)
    {
        if (!packInstalled)
        {
            return phoneRowShown ? PickerSelection.Phone : PickerSelection.None;
        }

        return phoneRowShown && phoneVoiceStored ? PickerSelection.Phone : PickerSelection.Natural;
    }

    /// <summary>Whether the phone voice is the one that would speak.</summary>
    public static bool PhoneReads(bool phoneVoiceChosen, bool packReady) => phoneVoiceChosen || !packReady;

    /// <summary>
    /// What happens to a read in the reading language (Slice 90): the user's phone-voice choice is kept
    /// when the phone can speak it; otherwise an installed natural pack for that language reads; otherwise
    /// the phone voice if it can; else the read is blocked. A voice for another language never reads.
    /// </summary>
    public static VoiceRoute Resolve(bool phoneVoiceChosen, bool naturalPackForLanguage, bool? phoneCanRead, bool packDownloading) =>
        Route(PhoneReads(phoneVoiceChosen, naturalPackForLanguage), phoneCanRead, packDownloading);

    public static VoiceRoute Route(bool phoneReads, bool? phoneCanRead, bool packDownloading)
    {
        if (!phoneReads || phoneCanRead != false)
        {
            return VoiceRoute.Read;
        }

        return packDownloading ? VoiceRoute.Downloading : VoiceRoute.NoVoice;
    }
}
