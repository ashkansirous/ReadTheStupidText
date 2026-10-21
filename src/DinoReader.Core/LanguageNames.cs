using ReadTheStupidText.Application.Reading;

namespace DinoReader.Core;

/// <summary>
/// The nine languages, each named in itself (endonyms) so a grown-up can find theirs whatever language
/// the screens speak. Endonyms are data, not translatable copy.
/// </summary>
public static class LanguageNames
{
    public static IReadOnlyList<(string Code, string NativeName)> All { get; } =
        Enum.GetValues<AppLanguage>().Select(l => (AppLanguages.Code(l), NativeNameOf(l))).ToList();

    private static string NativeNameOf(AppLanguage language) => language switch
    {
        AppLanguage.English => "English",
        AppLanguage.Persian => "فارسی",
        AppLanguage.French => "Français",
        AppLanguage.Spanish => "Español",
        AppLanguage.Portuguese => "Português",
        AppLanguage.Chinese => "中文",
        AppLanguage.Arabic => "العربية",
        AppLanguage.Russian => "Русский",
        AppLanguage.Japanese => "日本語",
        _ => throw new ArgumentOutOfRangeException(nameof(language)),
    };

    /// <summary>The language's own name; the English name for an unsupported code.</summary>
    public static string NativeName(string? code)
    {
        string resolved = UiLanguage.Resolve(code);
        return All.First(l => l.Code == resolved).NativeName;
    }

    /// <summary>The code behind a picked endonym, or null when it is none of the nine.</summary>
    public static string? CodeOf(string nativeName) =>
        All.Where(l => l.NativeName == nativeName).Select(l => l.Code).FirstOrDefault();
}
