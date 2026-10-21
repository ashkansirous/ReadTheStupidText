namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The language-to-pack choice shared by every pack kind (voice, OCR): the pack serving the fewest
/// languages is the most specific and wins, and ties break on the pack id so the result never
/// depends on folder enumeration order.
/// </summary>
public static class PackSelection
{
    /// <summary>The installed packs of a kind that serve a language, best match first.</summary>
    public static IEnumerable<InstalledVoicePack> Serving(IEnumerable<InstalledVoicePack> installed, PackKind kind, string language) =>
        installed
            .Where(p => p.Manifest.Kind == kind)
            .Where(p => PackCatalogParser.SplitLanguages(p.Manifest.Language).Contains(language, StringComparer.OrdinalIgnoreCase))
            .OrderBy(p => PackCatalogParser.SplitLanguages(p.Manifest.Language).Count)
            .ThenBy(p => p.Manifest.Id, StringComparer.Ordinal);

    /// <summary>The catalog entry of a kind that serves a language (the fewest-language pack), or null.</summary>
    public static PackInfo? FromCatalog(IEnumerable<PackInfo> catalog, PackKind kind, string language) =>
        catalog
            .Where(p => p.Kind == kind)
            .Where(p => p.Languages.Contains(language, StringComparer.OrdinalIgnoreCase))
            .OrderBy(p => p.Languages.Count)
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .FirstOrDefault();
}
