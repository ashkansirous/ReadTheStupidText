namespace ReadTheStupidText.Application.Images;

/// <summary>Language facts the OCR engine needs.</summary>
public static class OcrLanguages
{
    private static readonly HashSet<string> RightToLeft = new(StringComparer.OrdinalIgnoreCase) { "ar", "fa" };
    private static readonly HashSet<string> Latin = new(StringComparer.OrdinalIgnoreCase) { "en", "fr", "es", "pt" };

    public static bool IsRightToLeft(string language) => RightToLeft.Contains(language);

    /// <summary>True when ML Kit's Latin recognizer reads the language's script.</summary>
    public static bool IsLatinScript(string language) => Latin.Contains(language);
}
