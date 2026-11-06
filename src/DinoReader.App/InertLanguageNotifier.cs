using ReadTheStupidText.Application.Reading;

namespace DinoReader;

/// <summary>
/// Slice 278 part B: Dino's <see cref="ILanguageChangeNotifier"/>. The shared voice services start a voice pack download whenever the stored language changes (the Debug
/// <c>skip-first-run</c> hook resolves the shared voice pack installer, which listens to this), and Dino downloads no voice, so nothing in it ever hears about a language change this way.
/// Dino reads the stored language itself wherever it needs it.
/// </summary>
internal sealed class InertLanguageNotifier : ILanguageChangeNotifier
{
	// The event is never raised; add/remove do nothing, so no subscriber is kept alive either.
	public event EventHandler? LanguageChanged
	{
		add { }
		remove { }
	}
}
