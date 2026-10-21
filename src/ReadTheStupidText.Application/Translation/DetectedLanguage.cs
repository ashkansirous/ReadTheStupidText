namespace ReadTheStupidText.Application.Translation;

/// <summary>A language guess for a piece of text: a BCP-47 tag (as the engine reports it) and its 0..1 confidence.</summary>
public sealed record DetectedLanguage(string Tag, float Confidence);
