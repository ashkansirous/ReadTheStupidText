namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Downloads and tracks language packs (Slice 50, Decisions 60-62). Resumes via
/// HTTP Range into a <c>.partial</c> file, retries with backoff, and verifies the
/// sha256 before moving a pack into place. <see cref="IVoiceModelService"/> stays
/// the readiness signal the speech readers use, fed by the installed pack folder.
/// </summary>
public interface IVoicePackService
{
    /// <summary>
    /// Raised after a pack is installed or replaced (Slice 90), so a cache of what is installed can drop
    /// itself. A service that never raises it keeps the default no-op.
    /// </summary>
    event EventHandler? InstalledPacksChanged
    {
        add { }
        remove { }
    }

    /// <summary>Fetches <c>packs.json</c>; the status says why when it could not.</summary>
    Task<PackCatalogResult> GetCatalogAsync(CancellationToken cancellationToken);

    /// <summary>The catalog fetched within <paramref name="maxAge"/> if there is one, else a fresh fetch (Slice 52: lets a second installer reuse the first one's fetch).</summary>
    Task<PackCatalogResult> GetCatalogCachedAsync(TimeSpan maxAge, CancellationToken cancellationToken);

    /// <summary>The installed pack's folder (present, with every manifest file), or null when it is not installed.</summary>
    string? GetInstalledPath(string packId);

    /// <summary>Every installed, complete voice pack with its parsed manifest (Slice 51); the input of <see cref="InstalledVoiceCatalog"/>.</summary>
    IReadOnlyList<InstalledVoicePack> GetInstalledVoicePacks();

    /// <summary>
    /// Whether this service installs, lists and removes storyteller packs (Slice 114, Dino Reader only). False by default: a head whose service
    /// does not say so shows no storyteller card, whatever else is registered.
    /// </summary>
    bool SupportsStorytellerPacks => false;

    /// <summary>
    /// Every installed, complete storyteller pack (Slice 114). Only Dino reads this, so the text reader's lookups never see one.
    /// A service without storyteller support (<see cref="SupportsStorytellerPacks"/>) has none.
    /// </summary>
    IReadOnlyList<InstalledVoicePack> GetInstalledStorytellerPacks() => [];

    /// <summary>The voice and the storyteller packs in one scan of the packs folder (each scan validates every installed pack's files).</summary>
    IReadOnlyList<InstalledVoicePack> GetInstalledVoiceAndStorytellerPacks() => [.. GetInstalledVoicePacks(), .. GetInstalledStorytellerPacks()];

    /// <summary>
    /// Removes an installed pack (Slice 114: "Use Dino's first voices") and raises <see cref="InstalledPacksChanged"/>; false when it was not
    /// installed or this service cannot remove packs. Call it only while nothing is reading.
    /// </summary>
    bool Uninstall(string packId) => false;

    /// <summary>Every installed, complete OCR pack with its parsed manifest (Slice 52); the input of <c>InstalledOcrCatalog</c>.</summary>
    IReadOnlyList<InstalledVoicePack> GetInstalledOcrPacks();

    /// <summary>Downloads, verifies and installs a pack from the catalog; progress is 0..1.</summary>
    Task<PackDownloadResult> DownloadAsync(string packId, IProgress<double>? progress, CancellationToken cancellationToken);
}
