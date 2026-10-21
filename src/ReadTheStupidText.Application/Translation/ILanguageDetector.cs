namespace ReadTheStupidText.Application.Translation;

/// <summary>Identifies the language of a text on the device (Slice 54). Never sends the text anywhere.</summary>
public interface ILanguageDetector
{
    /// <summary>The most likely language, or null when the engine cannot tell.</summary>
    Task<DetectedLanguage?> DetectAsync(string text, CancellationToken cancellationToken);
}
