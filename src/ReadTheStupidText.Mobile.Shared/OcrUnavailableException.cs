namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Thrown by an <see cref="ReadTheStupidText.Application.Images.IImageTextExtractor"/> that has
/// no OCR engine on this platform yet. Carries no user-facing text: the page that catches it
/// composes the copy.
/// </summary>
public sealed class OcrUnavailableException : Exception
{
    public OcrUnavailableException()
        : base("No on-device OCR engine is available on this platform.")
    {
    }
}
