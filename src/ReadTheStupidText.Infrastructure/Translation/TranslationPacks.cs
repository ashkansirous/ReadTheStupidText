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

    // The legs the Opus-MT packs cover (Slice 75, docs/plan/slices/75-windows-translation-engine.md):
    // thirteen languages into English, thirteen out of English (Slice 106 added da, fi, sv, nb, nl both ways, Slice 168 de both ways, Slice 184 en-ja; nb is
    // Norwegian Bokmal, served by the Scandinavian multi-language models, the target token lives in the pack's
    // translation.json). English to Japanese is Mitsua's ElanMT (CC-BY-SA-4.0, a downloaded pack only, converted to ONNX by packs.yml, Slice 184). Persian has no usable
    // leg in either direction, so those pairs are unsupported rather than badly translated.
    private static readonly string[] IntoEnglish = ["fr", "es", "pt", "zh", "ar", "ru", "ja", "da", "fi", "sv", "nb", "nl", "de"];
    private static readonly string[] FromEnglish = ["fr", "es", "pt", "zh", "ar", "ru", "ja", "da", "fi", "sv", "nb", "nl", "de"];

    /// <summary>Every language that has at least one leg, English included.</summary>
    public static IReadOnlyList<string> Languages { get; } = [Pivot, .. IntoEnglish.Union(FromEnglish)];

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

    /// <summary>Whether a pack exists for one leg (English at exactly one end).</summary>
    public static bool IsLegSupported(string source, string target)
    {
        if (target.Equals(Pivot, StringComparison.OrdinalIgnoreCase))
        {
            return IntoEnglish.Contains(source, StringComparer.OrdinalIgnoreCase);
        }

        return source.Equals(Pivot, StringComparison.OrdinalIgnoreCase)
            && FromEnglish.Contains(target, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Whether every leg of the pair has a pack model at all (installed or not).</summary>
    public static bool IsPairSupported(string source, string target) =>
        !source.Equals(target, StringComparison.OrdinalIgnoreCase)
        && Legs(source, target).All(leg => IsLegSupported(leg.Source, leg.Target));

    /// <summary>
    /// Whether the translate toggle makes sense for a user whose language is <paramref name="target"/>: English
    /// is reached from every source language, any other target needs its own English-to-target pack.
    /// Null when no target is known.
    /// </summary>
    public static bool? CanServeTarget(string? target)
    {
        if (string.IsNullOrEmpty(target))
        {
            return null;
        }

        return target.Equals(Pivot, StringComparison.OrdinalIgnoreCase) || IsLegSupported(Pivot, target);
    }

    /// <summary>The pack ids that must be installed to translate the pair, empty when the pair is unsupported.</summary>
    public static IReadOnlyList<string> RequiredPackIds(string source, string target) =>
        IsPairSupported(source, target) ? Legs(source, target).Select(l => PackId(l.Source, l.Target)).ToList() : [];

    /// <summary>The folder of a leg under <paramref name="packsRoot"/>, installed or not.</summary>
    public static string PackFolder(string packsRoot, string source, string target) =>
        Path.Combine(packsRoot, PackId(source, target));

    /// <summary>
    /// The default notion of an installed pack when no pack service is at hand: the leg's folder holds its
    /// <c>translation.json</c>. The app instead passes the pack service's validated lookup, so one definition
    /// of "installed" serves the installer, the readiness check and loading.
    /// </summary>
    public static Func<string, string?> FolderLookup(Func<string> packsRoot) =>
        packId =>
        {
            string folder = Path.Combine(packsRoot(), packId);
            return File.Exists(Path.Combine(folder, TranslationPackFiles.Spec)) ? folder : null;
        };

    /// <summary>Whether every leg of the pair is installed according to <paramref name="installedPath"/> (pack id to folder, null when absent or incomplete).</summary>
    public static bool IsInstalled(Func<string, string?> installedPath, string source, string target) =>
        Legs(source, target).All(leg => installedPath(PackId(leg.Source, leg.Target)) is not null);

    /// <summary>Whether any translation pack is installed (the engine can do something).</summary>
    public static bool AnyInstalled(Func<string, string?> installedPath) =>
        IntoEnglish.Any(l => installedPath(PackId(l, Pivot)) is not null)
        || FromEnglish.Any(l => installedPath(PackId(Pivot, l)) is not null);
}
