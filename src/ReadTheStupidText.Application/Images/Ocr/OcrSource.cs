namespace ReadTheStupidText.Application.Images;

/// <summary>Which engine produced an <see cref="ImageText"/>.</summary>
public enum OcrSource
{
    /// <summary>The downloaded PaddleOCR pack for the language.</summary>
    PaddleOcr,

    /// <summary>
    /// The platform's built-in recognizer (ML Kit Latin on Android, Apple Vision on iOS), because
    /// no pack serves the language.
    /// </summary>
    MlKit,

    /// <summary>
    /// The built-in recognizer (see <see cref="MlKit"/>) used while the language's OCR pack is not
    /// installed yet and the language is not a Latin one, so the text is likely poor. The UI can say the pack is still downloading.
    /// </summary>
    MlKitPackPending,
}
