namespace ReadTheStupidText.Application.Reading;

/// <summary>The fourteen app languages (Decisions 68 and 107).</summary>
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
    };

    public static string Code(AppLanguage language) => Codes[language];

    public static bool TryParse(string? code, out AppLanguage language)
    {
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
}
