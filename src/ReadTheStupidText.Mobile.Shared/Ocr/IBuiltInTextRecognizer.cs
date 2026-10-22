using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The text recognizer the operating system provides, used by <see cref="LanguageImageTextExtractor"/>
/// while a language's OCR pack is missing or cannot run: ML Kit on Android, Apple Vision on iOS
/// (Slice 55). Its coverage is a property of the platform, so each implementation says which
/// languages it reads well.
/// </summary>
public interface IBuiltInTextRecognizer
{
    /// <summary>True when this recognizer reads the language's script well without a pack.</summary>
    bool Covers(string language);

    /// <summary>The lines with their boxes, plus the pixel size of the frame the boxes are in.</summary>
    Task<OcrReading> ExtractLinesAsync(string imagePath);
}
