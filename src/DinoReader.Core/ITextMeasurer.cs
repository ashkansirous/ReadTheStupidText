namespace DinoReader.Core;

/// <summary>Measures how wide a string draws in the reading font and size. The app implements it
/// with the platform's text engine; tests use a fixed width per character.</summary>
public interface ITextMeasurer
{
    double Width(string text);
}
