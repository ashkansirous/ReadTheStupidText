using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using DinoReader.Core;
using ReadTheStupidText.Application.Translation;

namespace DinoReader;

/// <summary>
/// Translates a story's sentences into the grown-up's language after the speakers were worked out in
/// the story's own language (Slice 66, Decision 74). The on-device engine (<see cref="ITranslator"/>) is
/// optional: the ML Kit translator lives in the Read The Stupid Text app project and is not shared yet,
/// so this build carries no engine, <see cref="IsAvailable"/> is false and stories read in their own
/// language. When an engine is registered the whole flow works; a missing model never blocks reading.
/// </summary>
public sealed class StoryTranslator
{
	private readonly ITranslator? _engine;
	private readonly ConcurrentDictionary<(string Text, string Source, string Target), string> _cache = new();

	public StoryTranslator(IServiceProvider services)
	{
		_engine = services.GetService(typeof(ITranslator)) as ITranslator;
	}

	/// <summary>Whether this build can translate at all (an engine is registered).</summary>
	public bool IsAvailable => _engine is not null;

	/// <summary>The language the screens (and so "my language") speak: the device language when it is one of the nine.</summary>
	public static string MyLanguage => UiLanguage.Resolve(CultureInfo.CurrentUICulture.Name);

	/// <summary>Whether reading <paramref name="story"/> goes through the translator now.</summary>
	public bool Applies(Story story)
	{
		string source = UiLanguage.Resolve(story.Language);
		return _engine is not null
			&& TranslationFlow.ShouldTranslate(story.TranslateToMyLanguage, source, MyLanguage)
			&& TranslationFlow.IsPairSupported([.. _engine.SupportedLanguages], source, MyLanguage);
	}

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
		try
		{
			var translated = await StoryTranslation.TranslateAsync(pieces, (text, token) => PieceAsync(text, source, target, token), cancellationToken);
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
