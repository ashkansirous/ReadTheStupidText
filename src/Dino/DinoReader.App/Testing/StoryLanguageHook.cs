#if DEBUG || DINO_TEST_BOOKS
using DinoReader.Core;

namespace DinoReader.Testing;

/// <summary>
/// Slice 434: <c>story-language</c> for the Microsoft Store shot of the same page in English, Spanish and Japanese. It makes English, Spanish and Japanese the story languages (the app language stays main)
/// and picks one as the reading language, so the next book opened reads in it, exactly as a child's pick in the reading page's language menu does. Reachable in a Release test package through the
/// Windows head's <c>story-language.request</c> file (<c>Platforms/Windows/FullShelfRequest.cs</c>); outcome <c>HOOK story-language ok &lt;code&gt;</c> or <c>failed ...</c>.
/// </summary>
internal static class StoryLanguageHook
{
	internal const string Name = "story-language";

	private static readonly string[] ShotLanguages = ["en", "es", "ja"];

	/// <summary>Applies the pick; returns the outcome text that follows <c>HOOK story-language</c>.</summary>
	internal static string Apply(string code)
	{
		if (!UiLanguage.IsSupported(code))
		{
			return $"failed unknown language {code}";
		}

		StoryLanguageSet set = StoryLanguageSet.Create(LanguageChoice.Current(), ShotLanguages);
		if (!set.Contains(code))
		{
			return $"failed {code} is not in the story languages {string.Join(',', set.Sides)} + {set.Main}";
		}

		ShelfAudioPacks.ForcedOfferedLanguages = ShotLanguages; // the offline VM has no audio packs; without this the reading page keeps the app language (OffersBook)
		StoryLanguages.SaveSides(set);
		StoryLanguages.SetPick(code);
		return $"ok {StoryLanguages.Pick()}";
	}
}
#endif
