using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Application.Images;

/// <summary>An installed OCR pack ready to load: the folder that holds its models.</summary>
public sealed record OcrModelPaths(string Folder, string Language, string Version)
{
    public string Detection => Path.Combine(Folder, OcrPackLayout.DetectionModel);

    public string Recognition => Path.Combine(Folder, OcrPackLayout.RecognitionModel);

    public string Dictionary => Path.Combine(Folder, OcrPackLayout.Dictionary);

    public bool RightToLeft => OcrLanguages.IsRightToLeft(Language);

    /// <summary>True when both name the same installed pack (folder and version); the language is only how it was opened.</summary>
    public bool IsSamePack(OcrModelPaths? other) =>
        other is not null && Folder == other.Folder && Version == other.Version;
}

/// <summary>
/// Picks the installed OCR pack that serves a language, or null (ML Kit reads). Like
/// <see cref="InstalledVoiceCatalog"/>, the pack serving the fewest languages is the most specific.
/// </summary>
public static class InstalledOcrCatalog
{
    public static OcrModelPaths? Resolve(IEnumerable<InstalledVoicePack> installed, string language)
    {
        InstalledVoicePack? pack = PackSelection.Serving(installed, PackKind.Ocr, language).FirstOrDefault();

        return pack is null ? null : new OcrModelPaths(pack.Folder, language, pack.Manifest.Version);
    }

    /// <summary>
    /// Every installed OCR pack that reads a non-Latin script (the built-in recognizer owns Latin),
    /// the one serving <paramref name="hint"/> first. A pack loads with the hint language when it
    /// serves it, else with its own first non-Latin language, so its script decides right-to-left
    /// ordering and a mixed pack ("en,ru") is not dropped as Latin. Packs that do not serve the hint
    /// come in a stable order: the most specific (fewest languages) first, then by folder.
    /// </summary>
    public static IReadOnlyList<OcrModelPaths> NonLatinCandidates(IEnumerable<InstalledVoicePack> installed, string hint) =>
        installed
            .Where(p => p.Manifest.Kind == PackKind.Ocr)
            .Select(p => (Pack: p, Paths: ToPaths(p, hint)))
            .Where(x => ScriptDetector.ScriptOf(x.Paths.Language) is not (TextScript.Latin or TextScript.Unknown))
            .OrderByDescending(x => string.Equals(x.Paths.Language, hint, StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => PackCatalogParser.SplitLanguages(x.Pack.Manifest.Language).Count)
            .ThenBy(x => x.Pack.Folder, StringComparer.Ordinal)
            .Select(x => x.Paths)
            .ToList();

    private static OcrModelPaths ToPaths(InstalledVoicePack pack, string hint)
    {
        IReadOnlyList<string> languages = PackCatalogParser.SplitLanguages(pack.Manifest.Language);
        string language = languages.FirstOrDefault(l => string.Equals(l, hint, StringComparison.OrdinalIgnoreCase))
            ?? languages.FirstOrDefault(l => ScriptDetector.ScriptOf(l) is not (TextScript.Latin or TextScript.Unknown))
            ?? languages.FirstOrDefault()
            ?? hint;

        return new OcrModelPaths(pack.Folder, language, pack.Manifest.Version);
    }
}

/// <summary>Picks the catalog entry of the OCR pack that serves a language.</summary>
public static class OcrPackSelector
{
    public static PackInfo? Select(IEnumerable<PackInfo> catalog, string language) =>
        PackSelection.FromCatalog(catalog, PackKind.Ocr, language);
}
