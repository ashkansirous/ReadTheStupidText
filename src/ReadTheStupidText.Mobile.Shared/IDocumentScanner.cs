namespace ReadTheStupidText.Mobile;

/// <summary>
/// A multi-page document scanner (Slice 45, Decision 64). Android implements it with the ML Kit
/// document scanner; it is registered from <c>Platforms/Android</c> only, so a platform without
/// one simply has no registration (and hides Scan).
/// </summary>
public interface IDocumentScanner
{
    /// <summary>
    /// Runs the scanner and returns the scanned page images' locations (a plain path or a
    /// <c>file://</c> / <c>content://</c> uri string) in page order. The list is empty when the
    /// user cancels. Throws <see cref="DocumentScannerUnavailableException"/> when the scanner can
    /// never start here (for example, no Google Play services),
    /// <see cref="DocumentScannerPreparingException"/> when it is still being prepared, and
    /// <see cref="DocumentScannerStartFailedException"/> when it could not start right now.
    /// </summary>
    Task<IReadOnlyList<string>> ScanAsync();

    /// <summary>
    /// Best-effort warm-up so the first <see cref="ScanAsync"/> finds the scanner ready. Never
    /// throws.
    /// </summary>
    Task PrepareAsync();
}
