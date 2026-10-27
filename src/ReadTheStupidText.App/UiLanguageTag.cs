using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText_App;

/// <summary>
/// Maps the stored language code (one of the fifteen, Decisions 68 and 107) to the BCP-47 tag of its
/// <c>Strings\&lt;tag&gt;\Resources.resw</c> folder, and says which are right-to-left (Slice 57).
/// Pure BCL, so the unit tests compile this file directly.
/// </summary>
internal static class UiLanguageTag
{
    private const string EnglishCode = "en";

    private static readonly Dictionary<string, string> Tags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "en-US",
        ["fa"] = "fa",
        ["fr"] = "fr",
        ["es"] = "es",
        ["pt"] = "pt",
        ["zh"] = "zh-Hans",
        ["ar"] = "ar",
        ["ru"] = "ru",
        ["ja"] = "ja",
        ["da"] = "da",
        ["fi"] = "fi",
        ["sv"] = "sv",
        ["nb"] = "nb",
        ["nl"] = "nl",
        ["de"] = "de",
    };

    private static readonly HashSet<string> RightToLeft = new(StringComparer.OrdinalIgnoreCase) { "fa", "ar" };

    /// <summary>Every resource folder the app ships, in manifest order (English first, the default).</summary>
    public static IReadOnlyCollection<string> AllTags => Tags.Values;

    /// <summary>The resource tag for a stored language code, or null when the code is not one of the fifteen.</summary>
    public static string? For(string? code) =>
        code is not null && Tags.TryGetValue(code, out string? tag) ? tag : null;

    /// <summary>
    /// The resource tag the UI resolves to: the stored language's, else the first device language
    /// (a BCP-47 tag such as <c>ar-SA</c>) that one of the shipped folders serves, else English.
    /// </summary>
    public static string Effective(string? storedCode, string deviceTag) =>
        For(storedCode) ?? Match(deviceTag) ?? Tags[EnglishCode];

    /// <summary>The shipped folder serving a device tag by its primary language (ar-SA -> ar, zh-CN -> zh-Hans), or null.</summary>
    public static string? Match(string deviceTag)
    {
        string primary = deviceTag.Split('-', '_')[0];
        primary = AppLanguages.TryParse(primary, out AppLanguage known) ? AppLanguages.Code(known) : primary;
        return Tags.TryGetValue(primary, out string? tag) ? tag : null;
    }

    /// <summary>True when the effective resource language is Farsi or Arabic.</summary>
    public static bool IsRightToLeft(string? storedCode, string deviceTag) =>
        RightToLeft.Contains(Effective(storedCode, deviceTag));
}
