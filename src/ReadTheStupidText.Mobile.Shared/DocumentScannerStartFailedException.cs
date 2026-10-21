namespace ReadTheStupidText.Mobile;

/// <summary>
/// Thrown by an <see cref="IDocumentScanner"/> that could not start right now for a transient
/// reason (for example no foreground activity yet). Retrying should work, so the page tells the
/// user to try again rather than pushing them to the photo fallback.
/// </summary>
public sealed class DocumentScannerStartFailedException : Exception
{
    public DocumentScannerStartFailedException(Exception? inner = null)
        : base("The document scanner couldn't start; try again.", inner)
    {
    }
}
