namespace ReadTheStupidText.Infrastructure.Translation;

/// <summary>
/// Where translation packs live and which legs a language pair needs (Slice 57, Decision 67). A pair
/// pivots through English, so "fr to fa" is the packs "fr-en" and "en-fa"; a pair with English at one
/// end is a single pack. Pure path logic; the packs themselves install through the pack service.
/// </summary>
public static class TranslationPacks
{
    public const string Pivot = "en";
    private const string FolderPrefix = "translation-";
    private const char PairSeparator = '-';

    /// <summary>The pack id of one translation leg, for example "translation-fr-en".</summary>
    public static string PackId(string source, string target) =>
        $"{FolderPrefix}{source.ToLowerInvariant()}{PairSeparator}{target.ToLowerInvariant()}";

    /// <summary>The one or two legs that carry <paramref name="source"/> to <paramref name="target"/> through English.</summary>
    public static IReadOnlyList<(string Source, string Target)> Legs(string source, string target)
    {
        bool direct = source.Equals(Pivot, StringComparison.OrdinalIgnoreCase)
            || target.Equals(Pivot, StringComparison.OrdinalIgnoreCase);
        return direct ? [(source, target)] : [(source, Pivot), (Pivot, target)];
    }

    /// <summary>Whether every leg of the pair has an installed pack folder under <paramref name="packsRoot"/>.</summary>
    public static bool IsInstalled(string packsRoot, string source, string target) =>
        Legs(source, target).All(leg => Directory.Exists(Path.Combine(packsRoot, PackId(leg.Source, leg.Target))));
}
