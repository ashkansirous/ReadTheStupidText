using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DinoReader.Core;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Mobile.Shared;

namespace DinoReader;

/// <summary>
/// Translates a story's sentences into the grown-up's language after the speakers were worked out in
/// the story's own language (Slice 66, Decision 74). Each piece is looked up in the story's bundled
/// <see cref="Story.Translations"/> first, then the on-device engine (<see cref="ITranslator"/>, ML Kit on Android and iOS from
/// Mobile.Shared; none on Windows), else it is left as it is (Slice 95). A story with a full bundled table therefore
/// translates on every platform with no engine. A missing engine model never blocks reading.
/// </summary>
public sealed class StoryTranslator
{
	private readonly ITranslator? _engine;
	private readonly ITranslationPacks? _packs;
	private readonly IServiceProvider _services;
	private readonly ConcurrentDictionary<(string Text, string Source, string Target), string> _cache = new();
	private readonly SharedWork<(string Text, string Source, string Target), string> _inFlight = new(); // the screen and the voice translate the same pieces (Slice 110)

	public StoryTranslator(IServiceProvider services)
	{
		_services = services;
		_engine = services.GetService(typeof(ITranslator)) as ITranslator;
		_packs = _engine as ITranslationPacks;
		if (_packs is not null)
		{
			_packs.AutoDownload = false; // Dino asks before a language pack downloads (Slice 159, Decision 164); a missing pack means "not translated"
		}
	}

	/// <summary>Whether this build has a live translation engine (for a story being typed or scanned; a shipped story brings its own table).</summary>
	public bool IsAvailable => _engine is not null;

	private readonly ConditionalWeakTable<Story, ConcurrentDictionary<string, bool>> _complete = new();

	/// <summary>"My language": the grown-up's stored pick, else the device language when it is one of the fifteen. Not <c>CurrentUICulture</c>,
	/// which a language pick only changes for the screens opened afterwards and the reading screen can still see the startup culture.</summary>
	public static string MyLanguage => LanguageChoice.Current();

	/// <summary>Whether reading <paramref name="story"/> goes through the translator now.</summary>
	public bool Applies(Story story)
	{
		string mine = MyLanguage;
		return BundledTranslations.Applies(story, mine, _engine is null ? null : [.. _engine.SupportedLanguages])
			&& (_engine is not null || TableIsComplete(story, mine));
	}

	/// <summary>Whether the translate switch can do anything for <paramref name="story"/> (a table, a supported engine pair, or nothing to translate).</summary>
	public bool CanTranslate(Story story) =>
		BundledTranslations.CanTranslate(story, MyLanguage, _engine is null ? null : [.. _engine.SupportedLanguages]);

	/// <summary>With no engine a table with holes is not used (see <see cref="TranslateAsync"/>), so the read stays in the story's language; checked once per story instance.</summary>
	private bool TableIsComplete(Story story, string language) =>
		_complete.GetValue(story, _ => new()).GetOrAdd(language, l => BundledTranslations.IsComplete(story, l));

	/// <summary>Whether this read goes through a table the story carries (so it is read in whole sentences).</summary>
	public bool UsesBundledTable(Story story) => Applies(story) && BundledTranslations.Has(story, MyLanguage);

	/// <summary>The language the voice speaks the story in: the grown-up's when translating, the story's own otherwise.</summary>
	public string SpokenLanguage(Story story) => Applies(story) ? MyLanguage : UiLanguage.Resolve(story.Language);

	/// <summary>The pieces, and whether their text was translated (false when a missing language model left the story as it was).</summary>
	public sealed record Outcome(IReadOnlyList<SplitSegment> Pieces, bool Translated);

	/// <summary>The pieces with their text translated; a missing language model leaves the story as it was.</summary>
	public async Task<Outcome> TranslateAsync(
		Story story, IReadOnlyList<SplitSegment> pieces, CancellationToken cancellationToken, bool forSpeech = false)
	{
		string source = UiLanguage.Resolve(story.Language);
		string target = MyLanguage;
		if (_engine is null && BundledTranslations.MissesAny(story, target, pieces.Select(p => p.Segment.Text)))
		{
			// A table with holes and nothing to fill them: reading half in one language would be worse than the source.
			return new Outcome(pieces, false);
		}

		PieceTranslator engine = (text, token) => PieceAsync(text, source, target, token);
		try
		{
			PieceTranslator? fallback = _engine is null ? null : engine;
			PieceTranslator table = forSpeech ? BundledTranslations.ComposeSpoken(story, target, fallback) : BundledTranslations.Compose(story, target, fallback);
			var translated = await StoryTranslation.TranslateAsync(pieces, table, cancellationToken);
			return new Outcome(translated, true);
		}
		catch (TranslationModelUnavailableException ex)
		{
			Debug.WriteLine($"Dino Reader translation skipped, language model missing: {ex.Message}");
			return new Outcome(pieces, false);
		}
	}

	/// <summary>
	/// The title the shelf shows (Slice 159): in the grown-up's language, from the book's own table (which holds the title an own book was
	/// translated with) or the shipped-titles table, else the title as it is. Synchronous and offline; the switch, the language pair and the grown-up's "Show original text" decide, as for the text.
	/// </summary>
	public string ShelfTitle(Story story)
	{
		string target = MyLanguage;
		string source = UiLanguage.Resolve(story.Language);
		if (story.ShowOriginalText || !TranslationFlow.ShouldTranslate(story.TranslateToMyLanguage, source, target))
		{
			return story.Title;
		}

		return BundledTranslations.Lookup(story, target, story.Title)
			?? (source == UiLanguage.English ? ShippedTitles.ForBook(story, target) : null)
			?? story.Title;
	}

	/// <summary>The story's title in the grown-up's language for the reading screen (Slice 110): the bundled table's entry, else the engine's, else the title as it is.</summary>
	public async Task<string> TitleAsync(Story story, CancellationToken cancellationToken)
	{
		string target = MyLanguage;
		if (BundledTranslations.Lookup(story, target, story.Title) is { } bundled)
		{
			return bundled;
		}

		if (UiLanguage.Resolve(story.Language) == UiLanguage.English && ShippedTitles.ForBook(story, target) is { } shipped)
		{
			return shipped;
		}

		if (_engine is null || !story.Title.Any(char.IsLetter))
		{
			return story.Title;
		}

		try
		{
			string translated = (await PieceAsync(story.Title, UiLanguage.Resolve(story.Language), target, cancellationToken)).Trim();
			return translated.Length > 0 ? translated : story.Title;
		}
		catch (TranslationModelUnavailableException)
		{
			return story.Title;
		}
	}

	/// <summary>
	/// Whether <paramref name="story"/> should be translated on the phone but is not yet: the switch is on, its language differs from mine, the book has no full
	/// table, and this build has an engine that covers the pair (none on Windows: such a book stays in its own language).
	/// </summary>
	public bool NeedsOwnTranslation(Story story)
	{
		string mine = MyLanguage;
		string source = UiLanguage.Resolve(story.Language);
		return _engine is not null && _packs is not null
			&& TranslationFlow.ShouldTranslate(story.TranslateToMyLanguage, source, mine)
			&& TranslationFlow.IsPairSupported([.. _engine.SupportedLanguages], source, mine)
			&& OwnBookTranslation.Missing(story, mine).Count > 0;
	}

	/// <summary>The language packs this translation needs that are not on the device yet (none when they all are).</summary>
	public async Task<IReadOnlyList<string>> MissingPacksAsync(Story story)
	{
		if (_packs is null)
		{
			return [];
		}

		var missing = new List<string>();
		foreach (string language in OwnBookTranslation.PackLanguages(UiLanguage.Resolve(story.Language), MyLanguage))
		{
			if (!await _packs.IsPackReadyAsync(language))
			{
				missing.Add(language);
			}
		}

		return missing;
	}

	/// <summary>Downloads the given packs one by one; <paramref name="progress"/> gets 0..1 across them. Throws <see cref="TranslationModelUnavailableException"/> on a failure.</summary>
	public async Task DownloadPacksAsync(IReadOnlyList<string> languages, IProgress<double>? progress, CancellationToken cancellationToken)
	{
		for (int i = 0; i < languages.Count; i++)
		{
			await _packs!.DownloadPackAsync(languages[i], null, cancellationToken);
			progress?.Report((i + 1) / (double)languages.Count);
		}
	}

	/// <summary>
	/// Translates what <paramref name="story"/> misses in my language (text and title) and saves it inside the book, so the shelf and the reader show it offline afterwards.
	/// False when nothing could be translated (the pack is missing); the book is then left as it was.
	/// </summary>
	public async Task<bool> SaveOwnTranslationAsync(Story story, CancellationToken cancellationToken)
	{
		string mine = MyLanguage;
		string source = UiLanguage.Resolve(story.Language);
		IStoryLibrary? library = _services.GetService(typeof(IStoryLibrary)) as IStoryLibrary;
		if (_engine is null || library is null)
		{
			return false;
		}

		var table = new Dictionary<string, string>(BundledTranslations.TableFor(story, mine) ?? new Dictionary<string, string>(), StringComparer.Ordinal);
		try
		{
			foreach (string text in OwnBookTranslation.Missing(story, mine))
			{
				string translated = (await PieceAsync(text, source, mine, cancellationToken)).Trim();
				table[text] = translated.Length > 0 ? translated : text;
			}
		}
		catch (TranslationModelUnavailableException ex)
		{
			Debug.WriteLine($"Dino Reader could not save a translation, language model missing: {ex.Message}");
			return false;
		}

		return await StoreTableAsync(library, story.Id, mine, table);
	}

	// Applied to the book as it is on disk now, so an edit saved meanwhile is kept; only the language's table changes.
	private static async Task<bool> StoreTableAsync(IStoryLibrary library, string id, string language, IReadOnlyDictionary<string, string> table)
	{
		if (await library.GetAsync(id) is not { } current)
		{
			return false;
		}

		await library.UpdateAsync(OwnBookTranslation.WithTable(current, language, table));
		return true;
	}

	private async Task<string> PieceAsync(string text, string source, string target, CancellationToken cancellationToken)
	{
		if (_cache.TryGetValue((text, source, target), out string? known))
		{
			return known;
		}

		var key = (text, source, target);
		return await _inFlight.RunAsync(key, async token =>
		{
			string translated = await _engine!.TranslateAsync(text, source, target, token);
			_cache[key] = translated;
			return translated;
		}, cancellationToken);
	}
}
