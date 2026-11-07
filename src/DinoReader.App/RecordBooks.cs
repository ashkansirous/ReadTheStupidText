using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DinoReader;

/// <summary>
/// The book as one story language reads it, and the counts that go with it (Slice 286, Batch 49 frame 193): a language's lines are the pieces the record page cuts from that language's own
/// words, so the Farsi Storyteller can have fewer lines than the English one. Every count on the intro cards, the checklist, the progress bar and the book sheet comes from these pieces.
/// </summary>
internal static class RecordBooks
{
	/// <summary>The book in <paramref name="language"/>: <paramref name="source"/> itself when the story reads in that language, else the words of its translation on the same sentence numbers; null when the translation could not be made.</summary>
	public static async Task<RecordBook?> InAsync(IServiceProvider services, Story story, RecordBook source, string language, CancellationToken cancellation)
	{
		if (!services.GetRequiredService<StoryTranslator>().Applies(story, language))
		{
			return source;
		}

		TranslatedStory? translated = await services.GetRequiredService<StoryVoicing>().TranslatedDisplayAsync(story, RecordSplitBook.SentencesOf(story), language, cancellation);
		return translated is null ? null : RecordSplitBook.WithTranslatedText(source, translated);
	}

	/// <summary>Every spoken line of <paramref name="book"/> as the record page cuts it in <paramref name="language"/> with everybody ticked. Measures text, so it runs off the UI thread.</summary>
	public static IReadOnlyList<RecordPiece> AllLines(Story story, RecordBook book, string language) =>
		RecordCard.Pieces(story, book, new RecordChoice(RecordMode.PerCharacter, book.Speakers.Select(s => s.Id).ToHashSet()), language);

	/// <summary>How many of <paramref name="lines"/> (all of them, or one speaker's) are recorded.</summary>
	public static RecordProgress LinesProgress(Story story, IReadOnlyList<RecordPiece> lines, string? speaker = null)
	{
		IReadOnlyList<RecordPiece> scope = speaker is null ? lines : lines.Where(p => p.Speaker == speaker).ToList();
		return new RecordProgress(RecordSplitWalk.RecordedCount(story, scope), scope.Count, RecordUnit.Lines);
	}

	/// <summary>The count the book sheet and the cards show for <paramref name="language"/> in <paramref name="mode"/>: pages recorded of all pages (the same in every language), or lines recorded of that language's lines.</summary>
	public static RecordProgress Progress(Story story, RecordBook book, IReadOnlyList<RecordPiece> lines, RecordMode mode, string language) =>
		mode == RecordMode.WholePage ? RecordSplitWalk.Progress(story, book, mode, language) : LinesProgress(story, lines);
}
