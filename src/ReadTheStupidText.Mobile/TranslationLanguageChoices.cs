using System.Globalization;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>One entry of the source-language picker: a language code and the name it is shown under.</summary>
public sealed record TranslationLanguageChoice(string Code, string Name);

/// <summary>
/// Builds the source-language picker's entries (Slice 54): the fourteen app languages first, named in
/// themselves like the language chooser, then every other language the translator supports, named by
/// the system in its own language. Names are data from the platform, not translatable UI copy.
/// </summary>
public static class TranslationLanguageChoices
{
    public static IReadOnlyList<TranslationLanguageChoice> Build(IEnumerable<string> supportedCodes)
    {
        HashSet<string> supported = supportedCodes.Select(c => c.ToLowerInvariant()).ToHashSet();
        var choices = new List<TranslationLanguageChoice>();
        foreach ((AppLanguage language, string name) in LanguageChoices.All)
        {
            string code = AppLanguages.Code(language);
            if (supported.Remove(code))
            {
                choices.Add(new TranslationLanguageChoice(code, name));
            }
        }

        choices.AddRange(supported.Select(NamedByTheSystem).OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase));
        return choices;
    }

    private static TranslationLanguageChoice NamedByTheSystem(string code)
    {
        try
        {
            string name = CultureInfo.GetCultureInfo(code).NativeName;
            return string.IsNullOrWhiteSpace(name)
                ? new TranslationLanguageChoice(code, code)
                : new TranslationLanguageChoice(code, char.ToUpper(name[0], CultureInfo.CurrentCulture) + name[1..]);
        }
        catch (Exception)
        {
            // A language the system cannot name must never stop the picker (or the page) building.
            return new TranslationLanguageChoice(code, code);
        }
    }
}
