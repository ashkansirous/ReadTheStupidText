namespace ReadTheStupidText.Application.Translation;

/// <summary>
/// The languages ML Kit Translation covers (the same 59 on Android and iOS), as a plain table so that asking what the engine supports never
/// loads ML Kit: Dino Reader (a children's app) keeps ML Kit unloaded until a grown-up asks for a translation. Engine codes, as ML Kit spells them
/// (Norwegian is <c>no</c>); <see cref="All"/> holds the app's codes (<see cref="EngineLanguageCodes"/>). Pure.
/// </summary>
public static class MlKitLanguages
{
    private static readonly string[] EngineCodes =
    [
        "af", "sq", "ar", "be", "bn", "bg", "ca", "zh", "hr", "cs", "da", "nl", "en", "eo", "et", "fi", "fr", "gl", "ka", "de", "el",
        "gu", "ht", "he", "hi", "hu", "is", "id", "ga", "it", "ja", "kn", "ko", "lv", "lt", "mk", "ms", "mt", "mr", "no", "fa", "pl",
        "pt", "ro", "ru", "sk", "sl", "es", "sw", "sv", "tl", "ta", "te", "th", "tr", "uk", "ur", "vi", "cy",
    ];

    /// <summary>The app's lower-case codes of every language the engine translates from and to.</summary>
    public static IReadOnlyList<string> All { get; } = EngineCodes.Select(EngineLanguageCodes.FromEngine).ToArray();
}
