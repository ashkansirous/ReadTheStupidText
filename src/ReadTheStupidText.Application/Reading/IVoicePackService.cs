namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Downloads and tracks language packs (Slice 50, Decisions 60-62). Resumes via
/// HTTP Range into a <c>.partial</c> file, retries with backoff, and verifies the
/// sha256 before moving a pack into place. <see cref="IVoiceModelService"/> stays
/// the readiness signal the speech readers use, fed by the installed pack folder.
/// </summary>
public interface IVoicePackService
{
    /// <summary>Fetches <c>packs.json</c>; the status says why when it could not.</summary>
    Task<PackCatalogResult> GetCatalogAsync(CancellationToken cancellationToken);

    /// <summary>The installed pack's folder (present, with every manifest file), or null when it is not installed.</summary>
    string? GetInstalledPath(string packId);

    /// <summary>Every installed, complete voice pack with its parsed manifest (Slice 51); the input of <see cref="InstalledVoiceCatalog"/>.</summary>
    IReadOnlyList<InstalledVoicePack> GetInstalledVoicePacks();

    /// <summary>Downloads, verifies and installs a pack from the catalog; progress is 0..1.</summary>
    Task<PackDownloadResult> DownloadAsync(string packId, IProgress<double>? progress, CancellationToken cancellationToken);
}
