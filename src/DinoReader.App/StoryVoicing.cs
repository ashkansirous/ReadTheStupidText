using System.Collections.Concurrent;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>
/// Turns story text into voiced segments (Slice 60) and works out a story's cast (Slice 61):
/// segments the text, attributes each quote to a character and casts a voice per character
/// (never the narrator's). Who speaks is worked out once, at import, in the story's own language
/// (its <see cref="LanguageRules"/>, Slice 66), before any translation. The cast is stored with the
/// story; reading and recasting reuse the stored voices (<see cref="StoryCast.Pinned"/>) so a character
/// keeps their voice across restarts, text fixes and narrator changes. The voices a story can use are
/// the installed pack's for its language (Slice 51), or the Supertonic styles while none is installed.
/// </summary>
public sealed class StoryVoicing
{
	private readonly IVoicePackService _packs;
	private readonly StoryTranslator _translator;
	private readonly VoiceCaster _caster = new();
	private readonly ConcurrentDictionary<string, IReadOnlyList<VoiceOption>> _pools = new();

	// One segmenter and attributor per language (their rules are shared per language too), built on first use.
	private readonly ConcurrentDictionary<string, LanguagePipeline> _pipelines = new();

	private sealed record LanguagePipeline(LanguageRules Rules, StorySegmenter Segmenter, SpeakerAttributor Attributor);

	// One immutable snapshot, swapped atomically: attributing a long story is the costly part, and
	// a second read of the same text (or a narrator change) does not need it again. The sentences are
	// only built when a read asks for them (the cast screen never does).
	private sealed record Cache(string Language, string Text, AttributionResult Attribution, Lazy<IReadOnlyList<SentenceSpan>> Sentences);

	// The cast and the sentence pieces a read speaks, before any translation.
	private sealed record Prepared(VoiceCast Cast, IReadOnlyList<SplitSegment> Pieces);

	private volatile Cache? _cache;

	public StoryVoicing(IVoicePackService packs, IVoiceModelService model, StoryTranslator translator, VoicePackInstaller installer)
	{
		_packs = packs;
		_translator = translator;
		model.ReadyChanged += (_, _) => _pools.Clear();
		// A pack that finishes installing changes which voices a language has; a stale pool would keep fallback ids.
		installer.Changed += (_, _) => { if (installer.State == PackInstallState.Ready) _pools.Clear(); };
	}

	/// <summary>
	/// <see cref="Voice"/> on a worker thread: segmenting and attributing a long story is CPU work
	/// that must not block the UI. The story is translated afterwards when it is set to be (Slice 66).
	/// </summary>
	public async Task<IReadOnlyList<VoicedSegment>> VoiceAsync(
		Story story, string narratorVoiceId, int fromPosition = 0, CancellationToken cancellationToken = default, Action? translationSkipped = null)
	{
		Prepared prepared = await Task.Run(() => Prepare(story, narratorVoiceId, fromPosition), cancellationToken);
		if (!_translator.Applies(story))
		{
			return prepared.Pieces.Select(p => ToVoicedSegment(p, prepared.Cast, null)).ToList();
		}

		StoryTranslator.Outcome outcome = await _translator.TranslateAsync(story, prepared.Pieces, cancellationToken);
		if (!outcome.Translated)
		{
			// The story is read as written, in its own language and voices, so the spoken pack goes back to its language too.
			translationSkipped?.Invoke();
			return prepared.Pieces.Select(p => ToVoicedSegment(p, prepared.Cast, null)).ToList();
		}

		IReadOnlyList<SplitSegment> translated = outcome.Pieces;
		IReadOnlyList<VoicedSegment> voiced = translated.Select(p => ToVoicedSegment(p, prepared.Cast, null)).ToList();
		return Remapped(voiced, story.Language, _translator.SpokenLanguage(story), story.NarratorVoiceId);
	}

	/// <summary>The cast for <paramref name="text"/>, keeping any voice <paramref name="stored"/> already gives a character.
	/// <paramref name="merges"/> are the grown-up's merged characters (Slice 65).</summary>
	public Task<IReadOnlyList<CastMember>> CastAsync(
		string language, string text, string narratorVoiceId, IReadOnlyList<CastMember> stored, IReadOnlyDictionary<string, string>? merges = null) =>
		Task.Run(() => CastFor(language, text, narratorVoiceId, stored, merges));

	/// <summary>The voices a character of a story in <paramref name="language"/> can have (the whole pack; the narrator's is left out by the caller).</summary>
	public IReadOnlyList<VoiceOption> VoicesFor(string language) =>
		_pools.GetOrAdd(UiLanguage.Resolve(language), BuildPool);

	/// <summary>The language a voice preview speaks: the story's when its own pack is installed, else the one the voice speaks now. Never switches the voice.</summary>
	public string PreviewLanguage(string storyLanguage, string current)
	{
		string code = UiLanguage.Resolve(storyLanguage);
		return InstalledVoices(code) is not null ? code : current;
	}

	/// <summary>The voice a new story in <paramref name="language"/> starts with as its narrator: the default style when the pack has it, else the pack's first voice.</summary>
	public string NarratorFor(string language)
	{
		IReadOnlyList<VoiceOption> pool = VoicesFor(language);
		return pool.Any(v => v.Id == SupertonicVoiceTable.Default.Id) || pool.Count == 0
			? SupertonicVoiceTable.Default.Id
			: pool[0].Id;
	}

	/// <summary>How many voices characters can share out once <paramref name="narratorVoiceId"/> is the narrator's.</summary>
	public int CharacterVoiceCount(string language, string narratorVoiceId) =>
		VoicesFor(language).Count(v => v.Id != narratorVoiceId);

	/// <summary>The story's characters and lines as the cast screen shows them: merges applied, an edited
	/// character's gender in place of the guess. On a worker thread, like <see cref="VoiceAsync"/>.</summary>
	public Task<AttributionResult> ResolveAsync(Story story) =>
		Task.Run(() => StoryCast.Resolve(Analyse(story.Language, story.Text).Attribution, story.Merges, story.Cast));

	/// <summary>The voiced segments of the story from <paramref name="fromPosition"/> on, untranslated (a story index: a tapped
	/// or skipped-to sentence), read by <paramref name="narratorVoiceId"/> and the story's cast.</summary>
	public IReadOnlyList<VoicedSegment> Voice(Story story, string narratorVoiceId, int fromPosition = 0)
	{
		Prepared prepared = Prepare(story, narratorVoiceId, fromPosition);
		return prepared.Pieces.Select(p => ToVoicedSegment(p, prepared.Cast, null)).ToList();
	}

	public IReadOnlyList<CastMember> CastFor(
		string language, string text, string narratorVoiceId, IReadOnlyList<CastMember> stored, IReadOnlyDictionary<string, string>? merges = null)
	{
		AttributionResult resolved = StoryCast.Resolve(Analyse(language, text).Attribution, merges ?? Story.NoMerges, stored);
		return StoryCast.ToMembers(resolved.Characters, Cast(language, resolved, narratorVoiceId, stored), stored);
	}

	private Prepared Prepare(Story story, string narratorVoiceId, int fromPosition)
	{
		string text = story.Text;
		Cache cache = Analyse(story.Language, text);
		AttributionResult resolved = StoryCast.Resolve(cache.Attribution, story.Merges, story.Cast);
		VoiceCast cast = Cast(story.Language, resolved, narratorVoiceId, story.Cast);
		IReadOnlyList<Segment> segments = SegmentSlicer.From(resolved.Segments, text, fromPosition);

		// One piece per sentence, so the reader raises a chunk per sentence and the highlight steps with it.
		return new Prepared(cast, SegmentSentenceSplitter.Split(segments, text, cache.Sentences.Value));
	}

	private VoiceCast Cast(string language, AttributionResult attribution, string narratorVoiceId, IReadOnlyList<CastMember> stored) =>
		_caster.Cast(attribution.Characters, VoicesFor(language), narratorVoiceId, StoryCast.Pinned(stored));

	private Cache Analyse(string language, string text)
	{
		Cache? cached = _cache;
		if (cached is not null && cached.Text == text && cached.Language == language)
		{
			return cached;
		}

		LanguagePipeline pipeline = _pipelines.GetOrAdd(UiLanguage.Resolve(language), PipelineFor);
		AttributionResult attribution = pipeline.Attributor.Attribute(text, pipeline.Segmenter.Split(text));
		var fresh = new Cache(language, text, attribution, new Lazy<IReadOnlyList<SentenceSpan>>(() => StorySentences.Build(text, pipeline.Rules)));
		_cache = fresh;
		return fresh;
	}

	private static LanguagePipeline PipelineFor(string language)
	{
		LanguageRules rules = LanguageRules.For(language);
		return new LanguagePipeline(rules, new StorySegmenter(rules), new SpeakerAttributor(rules));
	}

	private IReadOnlyList<VoiceOption> BuildPool(string language)
	{
		IReadOnlyList<VoiceInfo> voices = InstalledVoices(language) ?? SupertonicVoiceTable.VoicesFor(language);
		CastText.RememberVoices(voices);
		return voices.Select(ToOption).ToList();
	}

	private IReadOnlyList<VoiceInfo>? InstalledVoices(string language)
	{
		try
		{
			return InstalledVoiceCatalog.Resolve(_packs.GetInstalledVoicePacks(), language)?.AvailableVoices;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	// A pack that does not say which of its voices are for girls (Piper, Melo) gives Unknown, which any character may take.
	private static VoiceOption ToOption(VoiceInfo voice)
	{
		bool supertonic = voice.Id.StartsWith(SupertonicVoiceTable.IdPrefix, StringComparison.Ordinal);
		Gender gender = !supertonic ? Gender.Unknown : SupertonicVoiceTable.IsFemale(voice.Id) ? Gender.Female : Gender.Male;
		return new VoiceOption(voice.Id, gender);
	}

	// A translated story is spoken in another language, whose pack may not have the voices the cast was given:
	// the whole set is remapped together (narrator first) so the narrator and the characters stay distinct.
	private IReadOnlyList<VoicedSegment> Remapped(IReadOnlyList<VoicedSegment> voiced, string storyLanguage, string spokenLanguage, string narratorVoiceId)
	{
		IEnumerable<string> ids = new[] { narratorVoiceId }.Concat(voiced.Select(v => v.VoiceId));
		var map = VoiceRemap.MapSet(ids, VoicesFor(storyLanguage), VoicesFor(spokenLanguage));
		return voiced.Select(v => v with { VoiceId = map.GetValueOrDefault(v.VoiceId, v.VoiceId) }).ToList();
	}

	// Narration, and a quote nobody could be attributed to, are read by the narrator (VoiceForLine).
	private static VoicedSegment ToVoicedSegment(SplitSegment piece, VoiceCast cast, Func<string, string>? remap)
	{
		Segment segment = piece.Segment;
		string voiceId = cast.VoiceForLine(segment.CharacterId);
		return new VoicedSegment(segment.Text, remap is null ? voiceId : remap(voiceId), segment.SourceStart, segment.SourceEnd, piece.TextStart);
	}
}
