namespace ReadTheStupidText.Application.Reading;

/// <summary>The catalog fetch outcome: a status plus the packs (empty unless Ok).</summary>
public sealed record PackCatalogResult(PackDownloadResult Status, IReadOnlyList<PackInfo> Packs)
{
    public static PackCatalogResult Failure(PackDownloadResult status) => new(status, []);
}
