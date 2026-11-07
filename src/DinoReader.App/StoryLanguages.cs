using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>
/// The languages the child's stories can be in (Slice 173, Decision 168): the app language plus up to two side languages the parent picked
/// (<see cref="StoryLanguageSet"/> holds the rules), and the one language the child reads in. The side languages and the child's pick are kept in
/// preferences; the app language is <see cref="LanguageChoice.Current()"/>. The child's pick reaches the reading screen only, which holds its reading
/// language itself and passes it to everything a read uses; no grown-up screen or shelf title sees it. The usage events of a book that was read (open, finish, a switch)
/// do carry the language it was read in, a closed-list value like the app language.
/// Slice 278 part B: a story language needs only the books' pre-made audio (<see cref="ShelfAudioPacks"/>); no voice pack is downloaded for it.
/// </summary>
public static class StoryLanguages
{
	private const string SidesKey = "dino.story.sides";
	private const string PickKey = "dino.story.reading";
	private const char PairSeparator = ',';
	private const char FlagSeparator = ':';

	/// <summary>Raised after the side languages were saved (from Settings, the app-language swap or the intro), with the set now in force: whatever waited for a language that left it can stop.</summary>
	public static event Action<StoryLanguageSet>? SidesSaved;

	/// <summary>The app language and the saved side languages.</summary>
	public static StoryLanguageSet Current() => StoryLanguageSet.Create(LanguageChoice.Current(), StoryLanguageSet.Split(Preferences.Default.Get(SidesKey, (string?)null)));

	/// <summary>Saves the side languages of <paramref name="set"/> (its app language is the app's own pick). The child's pick is forgotten when it left the set.</summary>
	public static void SaveSides(StoryLanguageSet set)
	{
		Preferences.Default.Set(SidesKey, StoryLanguageSet.Join(set.Sides));
		if (!set.Contains(Preferences.Default.Get(PickKey, (string?)null)))
		{
			Preferences.Default.Remove(PickKey);
		}

		SidesSaved?.Invoke(set);
	}

	/// <summary>The language the child picked (remembered across books and restarts), the app language when none or when it left the set.</summary>
	public static string Pick() => Current().Reading(Preferences.Default.Get(PickKey, (string?)null));

	public static void SetPick(string language) => Preferences.Default.Set(PickKey, UiLanguage.Resolve(language));

	/// <summary>The country code whose flag stands for <paramref name="language"/> (one fixed flag per language, kept in the strings file so it can change); empty when none.</summary>
	public static string FlagOf(string language)
	{
		string code = UiLanguage.Resolve(language);
		foreach (string pair in AppResources.StoryLanguages_FlagMap.Split(PairSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			string[] parts = pair.Split(FlagSeparator);
			if (parts.Length == 2 && parts[0] == code)
			{
				return parts[1];
			}
		}

		return string.Empty;
	}

	/// <summary>The name of <paramref name="language"/> in the app's language ("Farsi" in an English app), from the strings file; its own script's name when the map lacks it (Slice 286).</summary>
	public static string NameInAppLanguage(string language)
	{
		string code = UiLanguage.Resolve(language);
		foreach (string pair in AppResources.StoryLanguages_NameMap.Split(PairSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			string[] parts = pair.Split(FlagSeparator);
			if (parts.Length == 2 && parts[0] == code)
			{
				return parts[1];
			}
		}

		return LanguageNames.NativeName(code);
	}

	/// <summary>The languages in <paramref name="languages"/> by their names in their own script, like "English, فارسی".</summary>
	public static string Names(IEnumerable<string> languages) => string.Join(", ", languages.Select(LanguageNames.NativeName));
}
