namespace ReadTheStupidText.Application.Reading;

/// <summary>Where Dino's "Storyteller voices" card stands for a language (Slice 114).</summary>
public enum StorytellerState
{
    /// <summary>No card: no storyteller pack serves the language, or the catalog could not say.</summary>
    Hidden,

    /// <summary>The card with Download and "Hear one".</summary>
    Offer,

    /// <summary>The pack is installed: the green line and "Use Dino's first voices".</summary>
    Installed,
}

/// <summary>
/// The pure decisions behind the storyteller card on Dino's cast screen (Slice 114, Decision 118): which state it is in, the
/// size it names and where its sample lives. The pack and its catalog entry are the whole truth, so a catalog without the pack
/// (an older packs release) shows nothing.
/// </summary>
public static class StorytellerOffer
{
    private const double BytesPerMegabyte = 1_000_000;
    private const string SampleSuffix = "-sample.wav";
    private const string ZipExtension = PackNaming.ZipSuffix;

    /// <summary>The state for <paramref name="language"/>: installed wins (it needs no catalog), then a listed pack is an offer.</summary>
    public static StorytellerState StateFor(string language, IEnumerable<InstalledVoicePack> installed, PackInfo? listed)
    {
        if (PackSelection.Serving(installed, PackKind.Storyteller, language).Any())
        {
            return StorytellerState.Installed;
        }

        return listed is not null && listed.Languages.Contains(language, StringComparer.OrdinalIgnoreCase)
            ? StorytellerState.Offer
            : StorytellerState.Hidden;
    }

    /// <summary>The catalog's storyteller pack serving <paramref name="language"/>, or null.</summary>
    public static PackInfo? ListedFor(IEnumerable<PackInfo> catalog, string language) =>
        PackSelection.FromCatalog(catalog, PackKind.Storyteller, language);

    /// <summary>Whether the catalog lists a newer version of the installed storyteller pack (Slice 114): the pack is updated like the voice packs are.</summary>
    public static bool UpdateAvailable(InstalledVoicePack installed, IEnumerable<PackInfo> catalog) =>
        catalog.FirstOrDefault(p => p.Id == installed.Manifest.Id) is { } listed
        && PackVersions.IsNewer(listed.Version, installed.Manifest.Version);

    /// <summary>The download size as the card names it: whole megabytes (1 MB = 1,000,000 bytes), at least 1.</summary>
    public static int Megabytes(long sizeBytes) => Math.Max(1, (int)Math.Round(sizeBytes / BytesPerMegabyte));

    /// <summary>
    /// The file name of the pack's voice sample on the packs release, next to the zip: <c>kokoro-en-1.zip</c> has
    /// <c>kokoro-en-1-sample.wav</c>. packs.yml makes it with the model, so it always matches the pack's voices.
    /// </summary>
    public static string SampleFileName(PackInfo pack) => PackNaming.ZipFileName(pack.Id, pack.Version)[..^ZipExtension.Length] + SampleSuffix;

    /// <summary>A search pattern for every version's sample of the pack (<c>kokoro-en-*-sample.wav</c>), so a cache can drop the old ones.</summary>
    public static string SamplePattern(PackInfo pack) => pack.Id + "-*" + SampleSuffix;

    /// <summary>The sample's URL: the pack's own URL with the zip name swapped for <see cref="SampleFileName"/>, so it lives where the pack does.</summary>
    public static Uri SampleUri(PackInfo pack)
    {
        var zip = new Uri(pack.Url);
        return new Uri(zip, SampleFileName(pack));
    }
}
