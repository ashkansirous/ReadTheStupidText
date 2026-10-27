using System.Collections.Concurrent;
using ReadTheStupidText.Application.Reading;

namespace DinoReader.Core;

/// <summary>
/// The small words <see cref="AnimalGuess"/> relies on, written for each language (Slice 125): the <em>links</em> that may stand between a name and its animal word
/// ("Pip <b>the</b> pig", "a pig <b>called</b> Pip"), the <em>cues</em> that introduce a group of names ("Their names <b>were</b> Pip, Pop and Pim"), and the plural endings an
/// animal word may carry. A language with no list simply guesses less: a missed guess is better than a wrong one (the grown-up can set the animal on the cast screen).
/// </summary>
internal static class AnimalGuessWords
{
    private static readonly string[] EnglishEndings = ["s", "es"];
    private static readonly string[] OtherEndings = ["s", "es", "en", "er", "ar", "ne", "n", "ner"];
    private static readonly string[] GermanEndings = ["e", "en", "n", "er", "s", "nen"];

    // Plural forms English does not make with an ending, and the animal each belongs to.
    private static readonly Dictionary<string, AnimalKind> EnglishIrregularPlurals = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sheep"] = AnimalKind.Sheep,
        ["mice"] = AnimalKind.Mouse,
        ["wolves"] = AnimalKind.Wolf,
        ["calves"] = AnimalKind.Cow,
    };

    private static readonly IReadOnlyDictionary<string, AnimalKind> NoIrregularPlurals = new Dictionary<string, AnimalKind>();

    // The word sets are built once per language: the guess runs on every cast render and story analysis.
    private static readonly ConcurrentDictionary<AppLanguage, HashSet<string>> LinkSets = new();
    private static readonly ConcurrentDictionary<AppLanguage, HashSet<string>> CueSets = new();
    private static readonly ConcurrentDictionary<AppLanguage, HashSet<string>> ConjunctionSets = new();
    private static readonly ConcurrentDictionary<AppLanguage, HashSet<string>> PrepositionSets = new();

    private static AppLanguage Which(string? language)
    {
        return AppLanguages.TryParse(RuleSets.PrimaryCode(language), out AppLanguage parsed) ? parsed : AppLanguage.English;
    }

    /// <summary>The endings that make an animal word a plural in <paramref name="language"/>.</summary>
    public static string[] PluralEndings(string? language)
    {
        AppLanguage which = Which(language);
        return which == AppLanguage.English ? EnglishEndings : which == AppLanguage.German ? GermanEndings : OtherEndings;
    }

    /// <summary>Whether the endings only ever make a plural (English "pigs"); elsewhere they can be a definite singular too (Swedish "grisen").</summary>
    public static bool EndingsAreOnlyPlural(string? language)
    {
        return Which(language) == AppLanguage.English;
    }

    /// <summary>The plural forms of <paramref name="language"/> that carry no ending, with their animal (English only).</summary>
    public static IReadOnlyDictionary<string, AnimalKind> IrregularPlurals(string? language)
    {
        return Which(language) == AppLanguage.English ? EnglishIrregularPlurals : NoIrregularPlurals;
    }

    /// <summary>The single words that may stand between a name and its animal word (articles and "called").</summary>
    public static HashSet<string> Links(string? language)
    {
        return LinkSets.GetOrAdd(Which(language), l => Set(l switch
        {
            AppLanguage.French => "le la les l un une appelé appelée nommé nommée",
            AppLanguage.Spanish => "el la los las un una llamado llamada",
            AppLanguage.Portuguese => "o a os as um uma chamado chamada",
            AppLanguage.Dutch => "de het een genaamd",
            AppLanguage.German => "der die das den dem ein eine einen namens genannt",
            AppLanguage.Danish => "en et den det kaldet",
            AppLanguage.Swedish => "en ett den det kallad",
            AppLanguage.Norwegian => "en et den det kalt",
            AppLanguage.English => "the a an called named",
            _ => string.Empty,
        }));
    }

    /// <summary>The words that say "the names are ..." between a plural animal word and a list of names.</summary>
    public static HashSet<string> Cues(string? language)
    {
        return CueSets.GetOrAdd(Which(language), l => Set(l switch
        {
            AppLanguage.French => "appelés appelées appelaient étaient sont noms",
            AppLanguage.Spanish => "llamados llamadas llamaban eran son nombres",
            AppLanguage.Portuguese => "chamados chamadas chamavam eram são nomes",
            AppLanguage.Dutch => "heetten heten genaamd waren zijn namen",
            AppLanguage.German => "hießen heißen genannt waren sind namen namens",
            AppLanguage.Danish => "hed hedder kaldet var navne",
            AppLanguage.Swedish => "hette heter kallade var namn",
            AppLanguage.Norwegian => "het heter kalt var navn",
            AppLanguage.English => "called named were are names",
            _ => string.Empty,
        }));
    }

    /// <summary>The words that join the names of a list ("Pip, Pop <b>and</b> Pim").</summary>
    public static HashSet<string> Conjunctions(string? language)
    {
        return ConjunctionSets.GetOrAdd(Which(language), l => Set(l switch
        {
            AppLanguage.French => "et ou",
            AppLanguage.Spanish => "y e o u",
            AppLanguage.Portuguese => "e ou",
            AppLanguage.Dutch => "en of",
            AppLanguage.German => "und oder",
            AppLanguage.Danish => "og eller",
            AppLanguage.Swedish => "och eller",
            AppLanguage.Norwegian => "og eller",
            AppLanguage.English => "and or",
            _ => string.Empty,
        }));
    }

    /// <summary>The words that put an animal word somewhere else ("Mia and Leo were <b>near</b> cats"), so a "were ..." gap that holds one is not a group introduction.</summary>
    public static HashSet<string> Prepositions(string? language)
    {
        return PrepositionSets.GetOrAdd(Which(language), l => Set(l switch
        {
            AppLanguage.French => "près avec sans de du des dans sur sous chez par pour contre",
            AppLanguage.Spanish => "cerca con sin de del en sobre bajo por para contra",
            AppLanguage.Portuguese => "perto com sem de do da em sobre sob por para contra",
            AppLanguage.Dutch => "bij met zonder van in op onder door voor tegen naast",
            AppLanguage.German => "bei mit ohne von in auf unter durch für gegen neben nahe",
            AppLanguage.Danish => "ved med uden af i på under af for mod nær",
            AppLanguage.Swedish => "vid med utan av i på under av för mot nära",
            AppLanguage.Norwegian => "ved med uten av i på under for mot nær",
            AppLanguage.English => "near by with without of from in on at under for against beside behind",
            _ => string.Empty,
        }));
    }

    private static HashSet<string> Set(string words)
    {
        return new HashSet<string>(words.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
    }
}
