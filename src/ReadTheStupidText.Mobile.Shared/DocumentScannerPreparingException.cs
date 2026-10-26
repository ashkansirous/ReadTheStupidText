namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Thrown by an <see cref="IDocumentScanner"/> whose backing module is still being downloaded or
/// prepared (for example by Google Play services). Transient: retrying shortly should work, so the
/// page tells the user to try again rather than falling back to a photo.
/// </summary>
public sealed class DocumentScannerPreparingException : Exception
{
    public DocumentScannerPreparingException(Exception? inner = null)
        : base("The document scanner is still being prepared.", inner)
    {
    }
}
