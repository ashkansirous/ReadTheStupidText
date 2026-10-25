namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The pure decisions behind Dino's default storyteller download (Slices 114 and 115): which catalog entry serves a language, whether
/// an installed pack has a newer version and the size named. A catalog without the pack (an older packs release) lists nothing.
/// </summary>
public static class StorytellerOffer
{
    private const double BytesPerMegabyte = 1_000_000;

    /// <summary>The catalog's storyteller pack serving <paramref name="language"/>, or null.</summary>
    public static PackInfo? ListedFor(IEnumerable<PackInfo> catalog, string language) =>
        PackSelection.FromCatalog(catalog, PackKind.Storyteller, language);

    /// <summary>Whether the catalog lists a newer version of the installed storyteller pack (Slice 114): the pack is updated like the voice packs are.</summary>
    public static bool UpdateAvailable(InstalledVoicePack installed, IEnumerable<PackInfo> catalog) =>
        catalog.FirstOrDefault(p => p.Id == installed.Manifest.Id) is { } listed
        && PackVersions.IsNewer(listed.Version, installed.Manifest.Version);

    /// <summary>The download size as the card names it: whole megabytes (1 MB = 1,000,000 bytes), at least 1.</summary>
    public static int Megabytes(long sizeBytes) => Math.Max(1, (int)Math.Round(sizeBytes / BytesPerMegabyte));
}
