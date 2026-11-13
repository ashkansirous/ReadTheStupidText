namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The catalog fetch outcome: a status plus the packs (empty unless Ok). Slice 357: <paramref name="ServerAnswer"/> is what the server
/// answered when this very fetch failed (404, 429, 5xx ...), null otherwise; it travels with the result, never in shared state.
/// </summary>
public sealed record PackCatalogResult(PackDownloadResult Status, IReadOnlyList<PackInfo> Packs, Net.HttpErrorKind? ServerAnswer = null)
{
    public static PackCatalogResult Failure(PackDownloadResult status, Net.HttpErrorKind? serverAnswer = null) => new(status, [], serverAnswer);
}
