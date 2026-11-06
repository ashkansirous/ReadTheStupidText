using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DinoReader.Core;
using ReadTheStupidText.Application.Translation;
using FallbackArea = ReadTheStupidText.Application.Usage.FallbackArea;
using FallbackReason = ReadTheStupidText.Application.Usage.FallbackReason;

namespace DinoReader;

/// <summary>
/// Translates a story's sentences into the reading language after the speakers were worked out in the story's own language (Slice 66, Decision 74).
/// Slice 278: every book is a store book and carries its own translations, so each piece comes from the book's bundled <see cref="Story.Translations"/>
/// and nothing else: there is no on-device engine, no language pack and no download. A language the book has no full table for is not offered
/// and a read in it stays in the book's own language.
/// </summary>
public sealed class StoryTranslator
{
	private readonly IServiceProvider _services;
	private readonly ConditionalWeakTable<Story, ConcurrentDictionary<string, bool>> _complete = new();

	public StoryTranslator(IServiceProvider services)
	{
		_services = services;
	}

	/// <summary>"My language": the grown-up's stored pick, else the device language when it is one of the fifteen. Not <c>CurrentUICulture</c>,
	/// which a language pick only changes for the screens opened afterwards and the reading screen can still see the startup culture.
	/// This is the app language; the grown-up screens and the shelf titles use it. The reading screen passes its own reading language (the child's pick, Slice 173) to every call below.</summary>
	public static string MyLanguage => LanguageChoice.Current();

	/// <summary>Whether reading <paramref name="story"/> in <paramref name="reading"/> goes through the translator: the book carries a full table for it.</summary>
	public bool Applies(Story story, string reading) =>
		BundledTranslations.Applies(story, reading, null) && TableIsComplete(story, reading);

	/// <summary>A table with holes is not used, so the read stays in the story's language; checked once per story instance.</summary>
	private bool TableIsComplete(Story story, string language) =>
		_complete.GetValue(story, _ => new()).GetOrAdd(language, l => BundledTranslations.IsComplete(story, l));

	/// <summary>Whether this read goes through a table the story carries (so it is read in whole sentences).</summary>
	public bool UsesBundledTable(Story story, string reading) => Applies(story, reading) && BundledTranslations.Has(story, reading);

	/// <summary>The language the voice speaks the story in: the reading language when translating, the story's own otherwise.</summary>
	public string SpokenLanguage(Story story, string reading) => Applies(story, reading) ? UiLanguage.Resolve(reading) : UiLanguage.Resolve(story.Language);

	/// <summary>The pieces, and whether their text was translated (false when the book's table has a hole and the story was left as it was).</summary>
	public sealed record Outcome(IReadOnlyList<SplitSegment> Pieces, bool Translated);

	/// <summary>The pieces with their text from the book's table; a table with a hole leaves the story as it was.</summary>
	public async Task<Outcome> TranslateAsync(
		Story story, IReadOnlyList<SplitSegment> pieces, IReadOnlyList<SentenceSpan> sentences, string reading, CancellationToken cancellationToken, bool forSpeech = false)
	{
		string target = UiLanguage.Resolve(reading);
		if (BundledTranslations.MissesAny(story, target, pieces.Select(p => p.Segment.Text)))
		{
			// A table with holes and nothing to fill them: reading half in one language would be worse than the source.
			Fell(story, FallbackReason.TranslationSkipped, reading);
			return new Outcome(pieces, false);
		}

		PieceTranslator table = forSpeech ? BundledTranslations.ComposeSpoken(story, target, null) : BundledTranslations.Compose(story, target, null);
		var translated = await StoryTranslation.TranslateAsync(pieces, table, cancellationToken);
		// Persian and Arabic say who speaks first (Slice 254); every other language keeps the order it was translated in.
		// The sentences are the ones the voice analysed, so the plan and the spoken grouping can never disagree.
		return new Outcome(SpeakerFirst.Regroups(target, story.Language, translated: true) ? SpeakerFirst.Regroup(translated, target, story.Text, sentences) : translated, true);
	}

	// Slice 273 part B: counts that a translated read was left as written, about this book and the language it was to be read in.
	private void Fell(Story story, string reason, string reading) =>
		(_services.GetService(typeof(UsageCounter)) as UsageCounter)?.RecordFallback(story, FallbackArea.Translation, reason, reading);

	/// <summary>
	/// The title the shelf shows (Slice 159): in the grown-up's language, from the book's own table or the shipped-titles table, else the title as it is.
	/// Synchronous and offline; the language pair decides, as for the text.
	/// </summary>
	public string ShelfTitle(Story story)
	{
		string target = MyLanguage;
		string source = UiLanguage.Resolve(story.Language);
		if (!TranslationFlow.ShouldTranslate(story.TranslateToMyLanguage, source, target))
		{
			return story.Title;
		}

		return BundledTranslations.Lookup(story, target, story.Title)
			?? (source == UiLanguage.English ? ShippedTitles.ForBook(story, target) : null)
			?? story.Title;
	}

	/// <summary>The story's title in the reading language for the reading screen (Slice 110): the bundled table's entry, else the shipped-titles table's, else the title as it is.</summary>
	public Task<string> TitleAsync(Story story, string reading, CancellationToken cancellationToken)
	{
		string target = UiLanguage.Resolve(reading);
		if (BundledTranslations.Lookup(story, target, story.Title) is { } bundled)
		{
			return Task.FromResult(bundled);
		}

		if (UiLanguage.Resolve(story.Language) == UiLanguage.English && ShippedTitles.ForBook(story, target) is { } shipped)
		{
			return Task.FromResult(shipped);
		}

		return Task.FromResult(story.Title);
	}

	/// <summary>Whether the child can read <paramref name="story"/> in <paramref name="language"/> right now (Slice 173): it is the book's own language, or it carries a full table for it.
	/// False when the book does not translate.</summary>
	public Task<bool> CanReadInAsync(Story story, string language)
	{
		bool sameLanguage = UiLanguage.Resolve(language) == UiLanguage.Resolve(story.Language);
		return Task.FromResult(sameLanguage || (story.TranslateToMyLanguage && BundledTranslations.IsComplete(story, UiLanguage.Resolve(language)) && BundledTranslations.Has(story, UiLanguage.Resolve(language))));
	}
}
