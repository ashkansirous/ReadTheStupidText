using System.Text.RegularExpressions;
using ReadTheStupidText.Application.Reading;

namespace DinoReader.Core;

/// <summary>
/// The words in a character's name that say it is grown (Slice 125): "Mrs.", "Papa", "Mother", "Granny", "old", "big", written for each language. The young side needs no list
/// of its own: a diminutive ("little", "baby") and the young animals' words already make <see cref="FlavourGuess"/> say Small. Lower-case, compared case-insensitively;
/// in Chinese and Japanese (no spaces) a word matches the start or the end of a name, never a part in the middle.
/// </summary>
internal static class AnimalAgeWords
{
    private static readonly Regex WordRun = new(@"[\p{L}\p{M}\p{N}]+", RegexOptions.Compiled);

    private static string ListOf(AppLanguage language) => language switch
    {
        AppLanguage.French => "madame, monsieur, mère, maman, mamie, papa, père, papi, papy, grand, grande, vieux, vieille, gros, grosse",
        AppLanguage.Spanish => "señora, señor, madre, mamá, papá, padre, abuela, abuelo, grande, viejo, vieja",
        AppLanguage.Portuguese => "senhora, senhor, mãe, mamãe, papai, pai, avó, avô, vovó, vovô, grande, velho, velha",
        AppLanguage.Russian => "мама, мать, папа, отец, бабушка, дедушка, большой, большая, старый, старая, госпожа, господин",
        AppLanguage.Persian => "مامان, مادر, بابا, پدر, بزرگ, پیر, خانم, آقا",
        AppLanguage.Arabic => "أم, ماما, أب, بابا, جدة, جد, كبير, كبيرة, عجوز, السيدة, السيد",
        AppLanguage.Chinese => "妈妈, 爸爸, 奶奶, 爷爷, 夫人, 先生, 大",
        AppLanguage.Japanese => "ママ, パパ, お母さん, おかあさん, お父さん, おとうさん, おばあさん, おじいさん, 大きな, 母, 父",
        AppLanguage.Danish => "fru, hr, mor, far, mormor, morfar, farmor, farfar, stor, store, gammel, gamle",
        AppLanguage.Finnish => "rouva, herra, äiti, isä, mummo, vaari, iso, suuri, vanha",
        AppLanguage.Swedish => "fru, herr, mamma, pappa, mor, far, mormor, morfar, farmor, farfar, stor, stora, gammal",
        AppLanguage.Norwegian => "fru, herr, mor, far, mamma, pappa, bestemor, bestefar, stor, store, gammel",
        AppLanguage.Dutch => "mevrouw, meneer, moeder, mama, vader, papa, oma, opa, grote, groot, oude, oud",
        _ => "mrs, mr, mister, mother, mama, mummy, mum, mom, father, papa, daddy, dad, grandma, grandpa, granny, grandmother, grandfather, old, big, great, lady, queen, king",
    };

    /// <summary>Whether <paramref name="name"/> has a grown word of the language.</summary>
    public static bool MentionsGrown(string name, string? language, ScriptStyle script)
    {
        AppLanguage which = AppLanguages.TryParse(RuleSets.PrimaryCode(language), out AppLanguage parsed) ? parsed : AppLanguage.English;
        IReadOnlyList<string> words = RuleSets.List(ListOf(which));
        if (script == ScriptStyle.Unspaced)
        {
            string trimmed = name.Trim();
            return words.Any(w => trimmed.StartsWith(w, StringComparison.Ordinal) || trimmed.EndsWith(w, StringComparison.Ordinal));
        }

        var tokens = WordRun.Matches(name).Select(m => m.Value).ToList();
        return words.Any(w => tokens.Contains(w, StringComparer.OrdinalIgnoreCase));
    }
}
