using System.Collections.Concurrent;
using ReadTheStupidText.Application.Reading;

namespace DinoReader.Core;

/// <summary>
/// The rule tables of every story language Dino Reader reads (Slice 66, Decisions 74 and 81),
/// picked by the story's language code. The tables are small starter lists (speech verbs,
/// titles, pronouns, common first names): a name that is not listed is still found from a speech
/// tag in the languages with capital letters, and a grown-up can fix any character on the cast
/// screen. Names, verbs and pronouns are lower-case; sets compare case-insensitively.
/// </summary>
internal static class RuleSets
{
    private const int DefaultDialogueGapChars = 160;

    private static readonly ConcurrentDictionary<AppLanguage, LanguageRules> Cache = new();

    /// <summary>The (shared, immutable) rules of a language code; English for an unknown code.</summary>
    public static LanguageRules For(string? language)
    {
        AppLanguage which = AppLanguages.TryParse(PrimaryCode(language), out AppLanguage parsed) ? parsed : AppLanguage.English;
        return Cache.GetOrAdd(which, Create);
    }

    private static LanguageRules Create(AppLanguage language) => language switch
    {
        AppLanguage.French => FrenchRules.Create(),
        AppLanguage.Spanish => SpanishRules.Create(),
        AppLanguage.Portuguese => PortugueseRules.Create(),
        AppLanguage.Russian => RussianRules.Create(),
        AppLanguage.Persian => FarsiRules.Create(),
        AppLanguage.Arabic => ArabicRules.Create(),
        AppLanguage.Chinese => ChineseRules.Create(),
        AppLanguage.Japanese => JapaneseRules.Create(),
        AppLanguage.Danish => DanishRules.Create(),
        AppLanguage.Finnish => FinnishRules.Create(),
        AppLanguage.Swedish => SwedishRules.Create(),
        AppLanguage.Norwegian => NorwegianRules.Create(),
        AppLanguage.Dutch => DutchRules.Create(),
        AppLanguage.German => GermanRules.Create(),
        _ => LanguageRules.English,
    };

    /// <summary>The language codes <see cref="For"/> has its own rules for, from <see cref="AppLanguage"/> (English first).</summary>
    public static IReadOnlyList<string> Codes { get; } = Enum.GetValues<AppLanguage>().Select(AppLanguages.Code).ToList();

    /// <summary>The lower-case primary subtag: "pt-BR" and "pt_BR" are "pt"; blank is "en".</summary>
    public static string PrimaryCode(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return "en";
        return language.Split('-', '_')[0].Trim().ToLowerInvariant();
    }

    /// <summary>A case-insensitive set from a comma-separated list (an entry may hold spaces).</summary>
    public static HashSet<string> Set(string commaSeparated) =>
        new(commaSeparated.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);

    public static HashSet<string> Union(HashSet<string> a, HashSet<string> b)
    {
        var all = new HashSet<string>(a, StringComparer.OrdinalIgnoreCase);
        all.UnionWith(b);
        return all;
    }

    public static IReadOnlyList<string> List(string commaSeparated) =>
        commaSeparated.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static int GapChars => DefaultDialogueGapChars;
}
