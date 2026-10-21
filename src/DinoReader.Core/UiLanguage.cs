using ReadTheStupidText.Application.Reading;

namespace DinoReader.Core;

/// <summary>
/// The nine languages Dino Reader's screens and stories use (Decision 68): which one the screens
/// speak (the device language when it is one of the nine, else English) and which run right to left.
/// Pure, so it is unit-tested; the app turns the code into a culture.
/// </summary>
public static class UiLanguage
{
    public const string English = "en";

    /// <summary>The nine codes, English first, in the order the language list shows them.</summary>
    public static IReadOnlyList<string> Supported { get; } = RuleSets.Codes;

    /// <summary>The supported code for a language or culture name ("pt-BR" is "pt"); English when unsupported or blank.</summary>
    public static string Resolve(string? language)
    {
        return AppLanguages.TryParse(RuleSets.PrimaryCode(language), out AppLanguage known) ? AppLanguages.Code(known) : English;
    }

    /// <summary>Whether <paramref name="language"/> is one of the nine (not a fallback).</summary>
    public static bool IsSupported(string? language) =>
        AppLanguages.TryParse(RuleSets.PrimaryCode(language), out _);

    /// <summary>Farsi and Arabic: the one right-to-left set, which does not depend on the runtime's ICU culture data.</summary>
    public static bool IsRightToLeft(string? language) =>
        AppLanguages.TryParse(RuleSets.PrimaryCode(language), out AppLanguage known)
        && known is AppLanguage.Persian or AppLanguage.Arabic;
}
