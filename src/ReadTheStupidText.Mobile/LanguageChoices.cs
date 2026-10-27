using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The fifteen languages, each named in itself (endonyms) so a user can find theirs whatever
/// language the UI is in. Endonyms are data, not translatable UI copy.
/// </summary>
public static class LanguageChoices
{
    public static readonly IReadOnlyList<(AppLanguage Language, string NativeName)> All =
    [
        (AppLanguage.English, "English"),
        (AppLanguage.Persian, "فارسی"),
        (AppLanguage.French, "Français"),
        (AppLanguage.Spanish, "Español"),
        (AppLanguage.Portuguese, "Português"),
        (AppLanguage.Chinese, "中文"),
        (AppLanguage.Arabic, "العربية"),
        (AppLanguage.Russian, "Русский"),
        (AppLanguage.Japanese, "日本語"),
        (AppLanguage.Danish, "Dansk"),
        (AppLanguage.Finnish, "Suomi"),
        (AppLanguage.Swedish, "Svenska"),
        (AppLanguage.Norwegian, "Norsk"),
        (AppLanguage.Dutch, "Nederlands"),
        (AppLanguage.German, "Deutsch"),
    ];

    public static string NativeName(AppLanguage language) => All.First(c => c.Language == language).NativeName;
}
