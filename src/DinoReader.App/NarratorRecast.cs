using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Keeps the narrator's voice out of every cast (Slice 109, Decision 114). The narrator is one voice per language for all books, so a book
/// whose stored cast already gave that voice to a character has that character re-cast quietly; a character the grown-up had pinned to it loses the pin.
/// The same pass re-casts a cast whose voices are no longer the language's (the storyteller pack was installed or removed, Slice 114), but only the
/// characters the grown-up did not pick: an explicit pick stays unless its voice left with its pack, and then it takes the closest voice.
/// </summary>
internal static class NarratorRecast
{
	/// <summary>The story with every language's clashing characters re-cast, or null when no cast has the narrator's voice (or its language has no voices yet). On a worker thread.</summary>
	public static async Task<Story?> TryRecastAsync(StoryVoicing voicing, Story story)
	{
		Story changed = story;
		foreach (string language in LanguagesOf(story))
		{
			IReadOnlyList<CastMember> stored = story.CastIn(language);
			string narrator = voicing.NarratorOf(language);
			IReadOnlyList<VoiceOption> pool = voicing.VoicesFor(language);
			if (!CastEditor.UsesVoice(stored, narrator) && !CastEditor.HasVoiceOutside(stored, pool))
			{
				continue;
			}

			IReadOnlyList<CastMember> recast = await voicing.CastForAsync(story.Language, language, story.Text, stored, story.Merges, story.BreakMap());
			if (recast.Count > 0)
			{
				IReadOnlyList<CastMember> kept = CastEditor.KeepExplicitPicks(stored, recast, pool, narrator);
				IReadOnlyList<CastMember> result = CastEditor.UnpinOverridden(stored, kept, narrator);
				changed = result.SequenceEqual(stored) ? changed : changed.WithCastIn(language, result);
			}
		}

		return ReferenceEquals(changed, story) ? null : changed;
	}

	// The story's own language and every other language it has a cast in.
	private static IEnumerable<string> LanguagesOf(Story story) =>
		new[] { story.Language }.Concat(story.LanguageCasts?.Keys ?? []).DistinctBy(UiLanguage.Resolve);
}
