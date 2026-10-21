namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Raises <see cref="LanguageChanged"/> when the stored language changes, so the voice model
/// for the new language is resolved straight away instead of on the next read.
/// </summary>
public interface ILanguageChangeNotifier
{
    event EventHandler? LanguageChanged;
}
