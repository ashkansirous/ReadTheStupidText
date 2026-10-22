namespace ReadTheStupidText.Application.Images;

/// <summary>Language facts the OCR engine needs; the script table in <see cref="ScriptDetector"/> is the source.</summary>
public static class OcrLanguages
{
    public static bool IsRightToLeft(string language) => ScriptDetector.IsRightToLeft(language);

    /// <summary>True when ML Kit's Latin recognizer reads the language's script.</summary>
    public static bool IsLatinScript(string language) => ScriptDetector.ScriptOf(language) == TextScript.Latin;
}
