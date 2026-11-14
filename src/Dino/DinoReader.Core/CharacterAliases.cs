namespace DinoReader.Core;

/// <summary>
/// Decides when two names are the same character: a single word that equals the first or
/// last word of a longer name ("Peter" and "Peter Rabbit"). A short name that fits more
/// than one longer name is ambiguous and stays separate.
/// </summary>
internal static class CharacterAliases
{
    public static bool Same(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase) || IsShortFormOf(a, b) || IsShortFormOf(b, a);

    /// <summary>Maps each name to its canonical (longest unambiguous) form.</summary>
    public static Dictionary<string, string> Canonicalize(IEnumerable<string> names)
    {
        var all = names.Distinct().ToList();
        return all.ToDictionary(n => n, n => CanonicalOf(n, all));
    }

    private static string CanonicalOf(string name, List<string> all)
    {
        var longer = all.Where(other => IsShortFormOf(name, other)).ToList();
        return longer.Count == 1 ? longer[0] : name;
    }

    private static bool IsShortFormOf(string shortName, string longName)
    {
        if (shortName.Contains(' ') || !longName.Contains(' ')) return false;
        var words = longName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return Equal(words[0], shortName) || Equal(words[^1], shortName);
    }

    private static bool Equal(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
