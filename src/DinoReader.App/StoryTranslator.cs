using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DinoReader.Core;
using ReadTheStupidText.Application.Translation;

namespace DinoReader;

/// <summary>
/// Translates a story's sentences into the grown-up's language after the speakers were worked out in
/// the story's own language (Slice 66, Decision 74). Each piece is looked up in the story's bundled
/// <see cref="Story.Translations"/> first, then the on-device engine (<see cref="ITranslator"/>, ML Kit on Android from
/// Mobile.Shared; none on Windows and iOS), else it is left as it is (Slice 95). A story with a full bundled table therefore
/// translates on every platform with no engine. A missing engine model never blocks reading.
/// </summary>
public sealed class StoryTranslator
{
	private readonly ITranslator? _engine;
	private readonly ConcurrentDictionary<(string Text, string Source, string Target), string> _cache = new();

	public StoryTranslator(IServiceProvider services)
	{
		_engine = services.GetService(typeof(ITranslator)) as ITranslator;
	}

	/// <summary>Whether this build has a live translation engine (for a story being typed or scanned; a shipped story brings its own table).</summary>
	public bool IsAvailable => _engine is not null;

	private readonly ConditionalWeakTable<Story, ConcurrentDictionary<string, bool>> _complete = new();

	/// <summary>"My language": the grown-up's stored pick, else the device language when it is one of the nine. Not <c>CurrentUICulture</c>,
	/// which a language pick only changes for the screens opened afterwards and the reading screen can still see the startup culture.</summary>
	public static string MyLanguage => LanguageChoice.Current();

	/// <summary>Whether reading <paramref name="story"/> goes through the translator now.</summary>
	public bool Applies(Story story)
	{
		string mine = MyLanguage;
		return BundledTranslations.Applies(story, mine, _engine is null ? null : [.. _engine.SupportedLanguages])
			&& (_engine is not null || TableIsComplete(story, mine));
	}

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
		Story story, IReadOnlyList<SplitSegment> pieces, CancellationToken cancellationToken)
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
			var translated = await StoryTranslation.TranslateAsync(pieces, BundledTranslations.Compose(story, target, _engine is null ? null : engine), cancellationToken);
			return new Outcome(translated, true);
		}
		catch (TranslationModelUnavailableException ex)
		{
			Debug.WriteLine($"Dino Reader translation skipped, language model missing: {ex.Message}");
			return new Outcome(pieces, false);
		}
	}

	private async Task<string> PieceAsync(string text, string source, string target, CancellationToken cancellationToken)
	{
		if (_cache.TryGetValue((text, source, target), out string? known))
		{
			return known;
		}

		string translated = await _engine!.TranslateAsync(text, source, target, cancellationToken);
		_cache[(text, source, target)] = translated;
		return translated;
	}
}
