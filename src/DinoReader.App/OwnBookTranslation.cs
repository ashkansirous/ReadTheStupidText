using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// The pure rules of translating a parent's own book on the phone (Slice 159, Decision 164). The translation is kept inside the book as a
/// table in <see cref="Story.Translations"/> (the same shape a shipped book brings), so the shelf, the grown-up screens and the reader show it
/// offline afterwards with no engine, and a changed text simply misses the new pieces and is translated again.
/// </summary>
public static class OwnBookTranslation
{
	private const string English = "en";

	/// <summary>The language packs a translation from <paramref name="source"/> to <paramref name="target"/> needs: both, and English, which ML Kit pivots through.</summary>
	public static IReadOnlyList<string> PackLanguages(string source, string target)
	{
		var languages = new List<string> { source };
		if (target != source)
		{
			languages.Add(target);
		}

		if (source != English && target != English)
		{
			languages.Add(English);
		}

		return languages;
	}

	/// <summary>The texts the table must hold for <paramref name="story"/> to show in full: its pieces, then its title.</summary>
	public static IReadOnlyList<string> WantedTexts(Story story)
	{
		var texts = new List<string>(StoryPieces.TranslatablePieceTexts(story));
		if (story.Title.Any(char.IsLetter) && !texts.Contains(story.Title))
		{
			texts.Add(story.Title);
		}

		return texts;
	}

	/// <summary>The wanted texts the book has no entry for in <paramref name="language"/>.</summary>
	public static IReadOnlyList<string> Missing(Story story, string language) =>
		WantedTexts(story).Where(t => BundledTranslations.Lookup(story, language, t) is null).ToList();

	/// <summary>
	/// The book with its <paramref name="language"/> table replaced by <paramref name="table"/>, keeping only entries for texts the book still has
	/// (an edited text drops its old translation) and every other language's table.
	/// </summary>
	public static Story WithTable(Story story, string language, IReadOnlyDictionary<string, string> table)
	{
		HashSet<string> wanted = [.. WantedTexts(story)];
		var kept = table.Where(e => wanted.Contains(e.Key)).ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
		string key = BundledTranslations.Key(language);
		var tables = new Dictionary<string, IReadOnlyDictionary<string, string>>(story.Translations ?? new Dictionary<string, IReadOnlyDictionary<string, string>>());
		foreach (string existing in tables.Keys.Where(k => BundledTranslations.Key(k) == key).ToList())
		{
			tables.Remove(existing);
		}

		tables[key] = kept;
		return story with { Translations = tables };
	}
}
