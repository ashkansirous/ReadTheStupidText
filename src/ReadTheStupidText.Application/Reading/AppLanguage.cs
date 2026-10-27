namespace ReadTheStupidText.Application.Reading;

/// <summary>The fifteen app languages (Decisions 68 and 107; German joined in Slice 168).</summary>
public enum AppLanguage
{
    English,
    Persian,
    French,
    Spanish,
    Portuguese,
    Chinese,
    Arabic,
    Russian,
    Japanese,
    Danish,
    Finnish,
    Swedish,
    Norwegian,
    Dutch,
    German,
}

/// <summary>Maps <see cref="AppLanguage"/> to the language codes stored in settings and used by the catalog.</summary>
public static class AppLanguages
{
    private static readonly Dictionary<AppLanguage, string> Codes = new()
    {
        [AppLanguage.English] = "en",
        [AppLanguage.Persian] = "fa",
        [AppLanguage.French] = "fr",
        [AppLanguage.Spanish] = "es",
        [AppLanguage.Portuguese] = "pt",
        [AppLanguage.Chinese] = "zh",
        [AppLanguage.Arabic] = "ar",
        [AppLanguage.Russian] = "ru",
        [AppLanguage.Japanese] = "ja",
        [AppLanguage.Danish] = "da",
        [AppLanguage.Finnish] = "fi",
        [AppLanguage.Swedish] = "sv",
        [AppLanguage.Norwegian] = "nb",
        [AppLanguage.Dutch] = "nl",
        [AppLanguage.German] = "de",
    };

    public static string Code(AppLanguage language) => Codes[language];

    public static bool TryParse(string? code, out AppLanguage language)
    {
        code = Canonical(code);
        foreach ((AppLanguage candidate, string candidateCode) in Codes)
        {
            if (string.Equals(candidateCode, code, StringComparison.OrdinalIgnoreCase))
            {
                language = candidate;
                return true;
            }
        }

        language = default;
        return false;
    }

    /// <summary>
    /// The one place a language tag is canonicalised: devices and tagged stories still report Norwegian as
    /// the legacy "no", or as Nynorsk "nn"; both are read and shown as Bokmal (nb), the one Norwegian the app ships.
    /// </summary>
    public static string? Canonical(string? code) =>
        string.Equals(code, "no", StringComparison.OrdinalIgnoreCase) || string.Equals(code, "nn", StringComparison.OrdinalIgnoreCase)
            ? "nb"
            : code;
}
