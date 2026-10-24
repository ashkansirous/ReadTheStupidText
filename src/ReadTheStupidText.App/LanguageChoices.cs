using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText_App;

/// <summary>One language of the fourteen, named in itself (Decision 68). Names are data, not translatable copy.</summary>
public sealed record LanguageChoice(AppLanguage Language, string NativeName);

/// <summary>The fourteen app languages in picker order, shared by the first-run window and the control panel.</summary>
public static class LanguageChoices
{
    public static IReadOnlyList<LanguageChoice> All { get; } =
    [
        new(AppLanguage.English, "English"),
        new(AppLanguage.Persian, "فارسی"),
        new(AppLanguage.French, "Français"),
        new(AppLanguage.Spanish, "Español"),
        new(AppLanguage.Portuguese, "Português"),
        new(AppLanguage.Chinese, "中文"),
        new(AppLanguage.Arabic, "العربية"),
        new(AppLanguage.Russian, "Русский"),
        new(AppLanguage.Japanese, "日本語"),
        new(AppLanguage.Danish, "Dansk"),
        new(AppLanguage.Finnish, "Suomi"),
        new(AppLanguage.Swedish, "Svenska"),
        new(AppLanguage.Norwegian, "Norsk"),
        new(AppLanguage.Dutch, "Nederlands"),
    ];

    /// <summary>The native name of a language code, or the code itself when it is not one of the fourteen.</summary>
    public static string NativeName(string code) =>
        AppLanguages.TryParse(code, out AppLanguage language) ? All.First(c => c.Language == language).NativeName : code;
}
