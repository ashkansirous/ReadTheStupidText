using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// The grown-up's narrator pick per language (Slice 109, Decision 114): one voice for every book, starter, store and own. Only the pick
/// is kept here, in preferences; <see cref="StoryVoicing.NarratorOf"/> resolves it against the language's pack, falling back to the default.
/// </summary>
public sealed class NarratorChoice
{
	private const string KeyPrefix = "dino.narrator.";

	/// <summary>The voice picked for <paramref name="language"/>, or null when none was (the default narrates).</summary>
	public string? Stored(string language) => Preferences.Default.Get(KeyOf(language), (string?)null);

	public void Set(string language, string voiceId) => Preferences.Default.Set(KeyOf(language), voiceId);

	private static string KeyOf(string language) => KeyPrefix + UiLanguage.Resolve(language);
}
