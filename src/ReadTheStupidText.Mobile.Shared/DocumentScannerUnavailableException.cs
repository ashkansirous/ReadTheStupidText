namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Thrown by an <see cref="IDocumentScanner"/> that cannot start on this device. Carries no
/// user-facing text: the page that catches it composes the copy.
/// </summary>
public sealed class DocumentScannerUnavailableException : Exception
{
    public DocumentScannerUnavailableException(Exception? inner = null)
        : base("The document scanner is not available on this device.", inner)
    {
    }
}
