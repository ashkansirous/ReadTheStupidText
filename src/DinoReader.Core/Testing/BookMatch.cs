namespace DinoReader.Core;

/// <summary>Pure book lookup and page maths of the open-book command.</summary>
internal static class BookMatch
{
	/// <summary>The book whose package id or story id equals <paramref name="id"/>, else the first whose title, as a dashed lower-case slug, contains it.</summary>
	public static Story? Find(IEnumerable<Story> stories, string id)
	{
		string wanted = id.Trim();
		if (wanted.Length == 0)
		{
			return null;
		}

		List<Story> all = stories.ToList();
		return all.FirstOrDefault(s => string.Equals(s.SourceId, wanted, StringComparison.OrdinalIgnoreCase) || string.Equals(s.Id, wanted, StringComparison.OrdinalIgnoreCase))
			?? all.FirstOrDefault(s => Slug(s.Title).Contains(Slug(wanted), StringComparison.Ordinal));
	}

	internal static string Slug(string text) => string.Join('-', text.Trim().ToLowerInvariant().Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries));

	/// <summary>The first and last sentence on the zero-based <paramref name="page"/>, or null when the book has no such page or the page holds none.</summary>
	public static (int First, int Last)? SentencesOfPage(Story story, int page)
	{
		if (page < 0 || page >= story.Pages.Count)
		{
			return null;
		}

		IReadOnlyList<SentenceSpan> sentences = StorySentences.Build(story.Text, LanguageRules.For(story.Language), story.BreakMap());
		IReadOnlyList<int> starts = StoryRecordings.PageStarts(story.Pages);
		int first = -1;
		int last = -1;
		for (int i = 0; i < sentences.Count; i++)
		{
			if (StoryRecordings.PageOfPosition(starts, sentences[i].Start) == page)
			{
				first = first < 0 ? i : first;
				last = i;
			}
		}

		return first < 0 ? null : (first, last);
	}
}
