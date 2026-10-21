using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>
/// Turns story text into voiced segments (Slice 60) and works out a story's cast (Slice 61):
/// segments the text, attributes each quote to a character and casts a voice per character
/// (never the narrator's). The cast is worked out once, at import, and stored with the story;
/// reading and recasting reuse the stored voices (<see cref="StoryCast.Pinned"/>) so a character
/// keeps their voice across restarts, text fixes and narrator changes.
/// </summary>
public sealed class StoryVoicing
{
	private static readonly IReadOnlyList<VoiceOption> VoiceOptions = SupertonicVoiceTable.Voices
		.Select(v => new VoiceOption(v.Id, SupertonicVoiceTable.IsFemale(v.Id) ? Gender.Female : Gender.Male))
		.ToList();

	private readonly StorySegmenter _segmenter = new(LanguageRules.English);
	private readonly SpeakerAttributor _attributor = new(LanguageRules.English);
	private readonly VoiceCaster _caster = new();

	// One immutable snapshot, swapped atomically: attributing a long story is the costly part, and
	// a second read of the same text (or a narrator change) does not need it again. The sentences are
	// only built when a read asks for them (the cast screen never does).
	private sealed record Cache(string Text, AttributionResult Attribution, Lazy<IReadOnlyList<SentenceSpan>> Sentences);

	private volatile Cache? _cache;

	/// <summary>
	/// <see cref="Voice"/> on a worker thread: segmenting and attributing a long story is CPU work
	/// that must not block the UI.
	/// </summary>
	public Task<IReadOnlyList<VoicedSegment>> VoiceAsync(Story story, string narratorVoiceId, int fromPosition = 0) =>
		Task.Run(() => Voice(story, narratorVoiceId, fromPosition));

	/// <summary>The cast for <paramref name="text"/>, keeping any voice <paramref name="stored"/> already gives a character.
	/// <paramref name="merges"/> are the grown-up's merged characters (Slice 65).</summary>
	public Task<IReadOnlyList<CastMember>> CastAsync(
		string text, string narratorVoiceId, IReadOnlyList<CastMember> stored, IReadOnlyDictionary<string, string>? merges = null) =>
		Task.Run(() => CastFor(text, narratorVoiceId, stored, merges));

	/// <summary>The voices a character can have (the whole pack; the narrator's is left out by the caller).</summary>
	public IReadOnlyList<VoiceOption> Voices => VoiceOptions;

	/// <summary>How many voices characters can share out once <paramref name="narratorVoiceId"/> is the narrator's.</summary>
	public int CharacterVoiceCount(string narratorVoiceId) => VoiceOptions.Count(v => v.Id != narratorVoiceId);

	/// <summary>The story's characters and lines as the cast screen shows them: merges applied, an edited
	/// character's gender in place of the guess. On a worker thread, like <see cref="VoiceAsync"/>.</summary>
	public Task<AttributionResult> ResolveAsync(Story story) =>
		Task.Run(() => StoryCast.Resolve(Analyse(story.Text).Attribution, story.Merges, story.Cast));

	/// <summary>The voiced segments of the story from <paramref name="fromPosition"/> on (a story index: a tapped
	/// or skipped-to sentence), read by <paramref name="narratorVoiceId"/> and the story's cast.</summary>
	public IReadOnlyList<VoicedSegment> Voice(Story story, string narratorVoiceId, int fromPosition = 0)
	{
		string text = story.Text;
		Cache cache = Analyse(text);
		AttributionResult resolved = StoryCast.Resolve(cache.Attribution, story.Merges, story.Cast);
		VoiceCast cast = Cast(resolved, narratorVoiceId, story.Cast);
		IReadOnlyList<Segment> segments = SegmentSlicer.From(resolved.Segments, text, fromPosition);

		// One piece per sentence, so the reader raises a chunk per sentence and the highlight steps with it.
		return SegmentSentenceSplitter.Split(segments, text, cache.Sentences.Value)
			.Select(p => ToVoicedSegment(p, cast)).ToList();
	}

	public IReadOnlyList<CastMember> CastFor(
		string text, string narratorVoiceId, IReadOnlyList<CastMember> stored, IReadOnlyDictionary<string, string>? merges = null)
	{
		AttributionResult resolved = StoryCast.Resolve(Analyse(text).Attribution, merges ?? Story.NoMerges, stored);
		return StoryCast.ToMembers(resolved.Characters, Cast(resolved, narratorVoiceId, stored), stored);
	}

	private VoiceCast Cast(AttributionResult attribution, string narratorVoiceId, IReadOnlyList<CastMember> stored) =>
		_caster.Cast(attribution.Characters, VoiceOptions, narratorVoiceId, StoryCast.Pinned(stored));

	private Cache Analyse(string text)
	{
		Cache? cached = _cache;
		if (cached is not null && cached.Text == text)
		{
			return cached;
		}

		AttributionResult attribution = _attributor.Attribute(text, _segmenter.Split(text));
		var fresh = new Cache(text, attribution, new Lazy<IReadOnlyList<SentenceSpan>>(() => StorySentences.Build(text, LanguageRules.English)));
		_cache = fresh;
		return fresh;
	}

	// Narration, and a quote nobody could be attributed to, are read by the narrator (VoiceForLine).
	private static VoicedSegment ToVoicedSegment(SplitSegment piece, VoiceCast cast)
	{
		Segment segment = piece.Segment;
		string voiceId = cast.VoiceForLine(segment.CharacterId);
		return new VoicedSegment(segment.Text, voiceId, segment.SourceStart, segment.SourceEnd, piece.TextStart);
	}
}
