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
	private sealed record Cache(string Language, string Text, LineBreakMap LineBreaks, AttributionResult Attribution, Lazy<IReadOnlyList<SentenceSpan>> Sentences);

	// The analysis and the sentence pieces a read speaks, before any translation. The cast is worked out afterwards, in the language spoken (Slice 108).
	private sealed record Prepared(Cache Analysis, IReadOnlyList<SplitSegment> Pieces);

	/// <summary>A cast made for a language that had none (Slice 108), for the caller to save.</summary>
	public sealed record NewCast(string Language, IReadOnlyList<CastMember> Members);

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
		bool wholePieces = _translator.UsesBundledTable(story);
		Prepared prepared = await Task.Run(() => Prepare(story, fromPosition, wholePieces), cancellationToken);
		if (!_translator.Applies(story))
		{
			return await Task.Run(() => VoicedInOwnLanguage(story, prepared, narratorVoiceId), cancellationToken);
		}

		StoryTranslator.Outcome outcome = await _translator.TranslateAsync(story, prepared.Pieces, cancellationToken);
		if (!outcome.Translated)
		{
			// The story is read as written, in its own language and voices, so the spoken pack goes back to its language too.
			translationSkipped?.Invoke();
			return await Task.Run(() => VoicedInOwnLanguage(story, prepared, narratorVoiceId), cancellationToken);
		}

		return await Task.Run(() => VoicedTranslated(story, prepared, outcome.Pieces, narratorVoiceId), cancellationToken);
	}

	// A translated read is cast from the spoken language's own cast (Slice 108), its narrator the story's narrator mapped into that pack.
	// The pack scans behind the voices run here, on the worker thread that calls it.
	private IReadOnlyList<VoicedSegment> VoicedTranslated(Story story, Prepared prepared, IReadOnlyList<SplitSegment> pieces, string narratorVoiceId)
	{
		string spoken = _translator.SpokenLanguage(story);
		VoiceCast cast = CastIn(story, prepared, spoken, NarratorIn(story, spoken, narratorVoiceId));
		return pieces.Select(p => ToVoicedSegment(p, cast)).ToList();
	}

	private IReadOnlyList<VoicedSegment> VoicedInOwnLanguage(Story story, Prepared prepared, string narratorVoiceId)
	{
		VoiceCast cast = CastIn(story, prepared, story.Language, narratorVoiceId);
		return prepared.Pieces.Select(p => ToVoicedSegment(p, cast)).ToList();
	}

	/// <summary>The language a read of <paramref name="story"/> is spoken in now, and so the one whose cast the cast screen edits (Slice 108).</summary>
	public string ReadingLanguage(Story story) => _translator.SpokenLanguage(story);

	/// <summary>The narrator voice in <paramref name="language"/>'s pack: the story's own pick, or its nearest voice there (the story's narrator belongs to its own language's pack).</summary>
	public string NarratorIn(Story story, string language, string? narratorVoiceId = null)
	{
		string narrator = narratorVoiceId ?? story.NarratorVoiceId;
		if (UiLanguage.Resolve(language) == UiLanguage.Resolve(story.Language))
		{
			return narrator;
		}

		return VoiceRemap.MapSet([narrator], VoicesFor(story.Language), VoicesFor(language)).GetValueOrDefault(narrator, narrator);
	}

	/// <summary>The voices of <paramref name="language"/>'s pack the grown-up can pick as the narrator there: those the story's one narrator
	/// (kept in its own language's pack until Slice 109) can be set to so that <see cref="NarratorIn"/> gives exactly that voice.</summary>
	public IReadOnlyList<VoiceOption> NarratorChoicesIn(Story story, string language) =>
		VoicesFor(language).Where(v => StoryNarratorFor(story, language, v.Id) is not null).ToList();

	/// <summary>The <see cref="Story.NarratorVoiceId"/> that makes <paramref name="voiceId"/> the narrator in <paramref name="language"/>, or null when none does.</summary>
	public string? StoryNarratorFor(Story story, string language, string voiceId)
	{
		if (UiLanguage.Resolve(language) == UiLanguage.Resolve(story.Language))
		{
			return voiceId;
		}

		return new[] { voiceId }.Concat(VoicesFor(story.Language).Select(v => v.Id))
			.FirstOrDefault(own => NarratorIn(story, language, own) == voiceId);
	}

	/// <summary>A cast for the language <paramref name="story"/> is read in now when it has none yet (Slice 108), else null (also null while that
	/// language has no voices yet or the story no characters, so nothing empty is kept). Seeded by the genders the grown-up corrected in the
	/// story's own cast; the caller saves it so the voices stay put. On a worker thread.</summary>
	public Task<NewCast?> NewCastForReadingAsync(Story story) =>
		story.HasCastIn(ReadingLanguage(story)) ? Task.FromResult<NewCast?>(null) : Task.Run(() => NewCastForReading(story));

	private NewCast? NewCastForReading(Story story)
	{
		string language = ReadingLanguage(story);
		if (story.HasCastIn(language) || VoicesFor(language).Count == 0)
		{
			return null;
		}

		// Only the corrected genders carry over, never the voices: those belong to the story language's pack.
		IReadOnlyList<CastMember> seed = story.Cast.Where(m => m.Edited).Select(m => m with { VoiceId = string.Empty }).ToList();
		IReadOnlyList<CastMember> members = CastFor(story.Language, language, story.Text, NarratorIn(story, language), seed, story.Merges, story.BreakMap());
		return members.Count == 0 ? null : new NewCast(language, members);
	}

	/// <summary>The cast for <paramref name="text"/>, keeping any voice <paramref name="stored"/> already gives a character.
	/// <paramref name="merges"/> are the grown-up's merged characters (Slice 65).</summary>
	public Task<IReadOnlyList<CastMember>> CastAsync(
		string language, string text, string narratorVoiceId, IReadOnlyList<CastMember> stored, IReadOnlyDictionary<string, string>? merges = null,
		LineBreakMap? lineBreaks = null) =>
		Task.Run(() => CastFor(language, text, narratorVoiceId, stored, merges, lineBreaks));

	/// <summary>The voices a character of a story in <paramref name="language"/> can have (the whole pack; the narrator's is left out by the caller).</summary>
	public IReadOnlyList<VoiceOption> VoicesFor(string language) =>
		_pools.GetOrAdd(UiLanguage.Resolve(language), BuildPool);

	/// <summary>The language a voice preview speaks: the story's when its own pack is installed, else the one the voice speaks now. Never switches the voice.</summary>
	public string PreviewLanguage(string storyLanguage, string current)
	{
		string code = UiLanguage.Resolve(storyLanguage);
		return InstalledVoices(code) is not null ? code : current;
	}

	private const string NorwegianLanguageCode = "nb";

	/// <summary>The voice a new story in <paramref name="language"/> starts with as its narrator: the default style when the pack has it, else the pack's first voice
	/// (Norwegian, before its pack installs: the pack's first voice, so the saved id is one the pack knows).</summary>
	public string NarratorFor(string language)
	{
		IReadOnlyList<VoiceOption> pool = VoicesFor(language);
		if (pool.Count == 0)
		{
			return UiLanguage.Resolve(language) == NorwegianLanguageCode ? NorwegianVoices.DefaultId : SupertonicVoiceTable.Default.Id;
		}

		return pool.Any(v => v.Id == SupertonicVoiceTable.Default.Id) ? SupertonicVoiceTable.Default.Id : pool[0].Id;
	}

	/// <summary>How many voices characters can share out once <paramref name="narratorVoiceId"/> is the narrator's.</summary>
	public int CharacterVoiceCount(string language, string narratorVoiceId) =>
		VoicesFor(language).Count(v => v.Id != narratorVoiceId);

	/// <summary>The story's characters and lines as the cast screen shows them: merges applied, an edited
	/// character's gender in place of the guess. On a worker thread, like <see cref="VoiceAsync"/>.</summary>
	public Task<AttributionResult> ResolveAsync(Story story, string castLanguage) =>
		Task.Run(() => StoryCast.Resolve(Analyse(story.Language, story.Text, story.BreakMap()).Attribution, story.Merges, story.CastIn(castLanguage)));

	/// <summary>The voiced segments of the story from <paramref name="fromPosition"/> on, untranslated (a story index: a tapped
	/// or skipped-to sentence), read by <paramref name="narratorVoiceId"/> and the story's cast.</summary>
	public IReadOnlyList<VoicedSegment> Voice(Story story, string narratorVoiceId, int fromPosition = 0)
	{
		return VoicedInOwnLanguage(story, Prepare(story, fromPosition), narratorVoiceId);
	}

	public IReadOnlyList<CastMember> CastFor(
		string language, string text, string narratorVoiceId, IReadOnlyList<CastMember> stored, IReadOnlyDictionary<string, string>? merges = null,
		LineBreakMap? lineBreaks = null) =>
		CastFor(language, language, text, narratorVoiceId, stored, merges, lineBreaks);

	/// <summary>The cast of a story written in <paramref name="storyLanguage"/>, voiced from <paramref name="castLanguage"/>'s pack (Slice 108):
	/// who speaks is worked out in the story's language, the voices come from the language it is read in.</summary>
	public IReadOnlyList<CastMember> CastFor(
		string storyLanguage, string castLanguage, string text, string narratorVoiceId, IReadOnlyList<CastMember> stored,
		IReadOnlyDictionary<string, string>? merges = null, LineBreakMap? lineBreaks = null)
	{
		AttributionResult resolved = StoryCast.Resolve(Analyse(storyLanguage, text, lineBreaks).Attribution, merges ?? Story.NoMerges, stored);
		return StoryCast.ToMembers(resolved.Characters, Cast(castLanguage, resolved, narratorVoiceId, stored), stored);
	}

	private Prepared Prepare(Story story, int fromPosition, bool wholePieces = false)
	{
		Cache cache = Analyse(story.Language, story.Text, story.BreakMap());
		AttributionResult resolved = StoryCast.Resolve(cache.Attribution, story.Merges, story.Cast);

		// One piece per sentence, so the reader raises a chunk per sentence and the highlight steps with it.
		// StoryPieces is shared with the starter builder, so a bundled translation is keyed by exactly these pieces (Slice 95).
		return new Prepared(cache, StoryPieces.Split(story, resolved, cache.Sentences.Value, fromPosition, wholePieces));
	}

	// The voices of the language's own cast: its pins and edited genders, in its own pack. A language with no stored cast is cast fresh here.
	private VoiceCast CastIn(Story story, Prepared prepared, string language, string narratorVoiceId)
	{
		IReadOnlyList<CastMember> stored = story.CastIn(language);
		AttributionResult resolved = StoryCast.Resolve(prepared.Analysis.Attribution, story.Merges, stored);
		return Cast(language, resolved, narratorVoiceId, stored);
	}

	private VoiceCast Cast(string language, AttributionResult attribution, string narratorVoiceId, IReadOnlyList<CastMember> stored) =>
		_caster.Cast(attribution.Characters, VoicesFor(language), narratorVoiceId, StoryCast.Pinned(stored));

	private Cache Analyse(string language, string text, LineBreakMap? breaks = null)
	{
		Cache? cached = _cache;
		if (cached is not null && cached.Text == text && cached.Language == language && (breaks is null || SameBreaks(cached.LineBreaks, breaks)))
		{
			return cached;
		}

		LineBreakMap lineBreaks = breaks ?? LineReflow.BreakMap(text);
		LanguagePipeline pipeline = _pipelines.GetOrAdd(UiLanguage.Resolve(language), PipelineFor);
		AttributionResult attribution = pipeline.Attributor.Attribute(text, pipeline.Segmenter.Split(text, lineBreaks));
		var fresh = new Cache(language, text, lineBreaks, attribution, new Lazy<IReadOnlyList<SentenceSpan>>(() => StorySentences.Build(text, pipeline.Rules, lineBreaks)));
		_cache = fresh;
		return fresh;
	}

	private static bool SameBreaks(LineBreakMap a, LineBreakMap b) =>
		a.SentenceBreaks.SetEquals(b.SentenceBreaks) && a.HeadingBreaks.SetEquals(b.HeadingBreaks) && a.DroppedHyphenBreaks.SetEquals(b.DroppedHyphenBreaks);

	private static LanguagePipeline PipelineFor(string language)
	{
		LanguageRules rules = LanguageRules.For(language);
		return new LanguagePipeline(rules, new StorySegmenter(rules), new SpeakerAttributor(rules));
	}

	private IReadOnlyList<VoiceOption> BuildPool(string language)
	{
		// A language Supertonic does not serve (Norwegian) has an empty pool until its pack installs, so no Supertonic id
		// is saved into a cast that the pack's own voices would then not know (pinned stored voices are kept as they are).
		IReadOnlyList<VoiceInfo> voices = InstalledVoices(language)
			?? (SupertonicVoiceTable.Serves(language) ? SupertonicVoiceTable.VoicesFor(language) : []);
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

	// A pack that does not say which of its voices are for girls (Persian Piper, Melo) gives Unknown, which any character may take.
	// Supertonic and the Norwegian pack (speaker codes, Slice 104) know theirs.
	private static VoiceOption ToOption(VoiceInfo voice)
	{
		bool supertonic = voice.Id.StartsWith(SupertonicVoiceTable.IdPrefix, StringComparison.Ordinal);
		Gender gender = supertonic
			? (SupertonicVoiceTable.IsFemale(voice.Id) ? Gender.Female : Gender.Male)
			: NorwegianVoices.IsFemale(voice.Id) switch { true => Gender.Female, false => Gender.Male, null => Gender.Unknown };
		return new VoiceOption(voice.Id, gender);
	}

	// Narration, and a quote nobody could be attributed to, are read by the narrator (VoiceForLine).
	private static VoicedSegment ToVoicedSegment(SplitSegment piece, VoiceCast cast)
	{
		Segment segment = piece.Segment;
		return new VoicedSegment(segment.Text, cast.VoiceForLine(segment.CharacterId), segment.SourceStart, segment.SourceEnd, piece.TextStart);
	}
}
