using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>
/// Turns typed story text into voiced segments (Slice 60): segments the text, attributes each
/// quote to a character, casts a voice per character (never the narrator's) and maps the result
/// onto the neutral <see cref="VoicedSegment"/> the speech reader takes. The cast is kept in
/// memory for the same text and narrator, so a second read gives every character the same voice
/// (Slice 61 persists it).
/// </summary>
public sealed class StoryVoicing
{
	private static readonly IReadOnlyList<VoiceOption> VoiceOptions = SupertonicVoiceTable.Voices
		.Select(v => new VoiceOption(v.Id, SupertonicVoiceTable.IsFemale(v.Id) ? Gender.Female : Gender.Male))
		.ToList();

	private readonly StorySegmenter _segmenter = new(LanguageRules.English);
	private readonly SpeakerAttributor _attributor = new(LanguageRules.English);
	private readonly VoiceCaster _caster = new();

	// One immutable snapshot, swapped atomically, so a key can never pair with another read's cast.
	private sealed record Cache(string Text, string NarratorVoiceId, AttributionResult Attribution, VoiceCast Cast);

	private volatile Cache? _cache;

	/// <summary>
	/// <see cref="Voice"/> on a worker thread: segmenting and attributing a long story is CPU work
	/// that must not block the UI.
	/// </summary>
	public Task<IReadOnlyList<VoicedSegment>> VoiceAsync(string text, string narratorVoiceId) =>
		Task.Run(() => Voice(text, narratorVoiceId));

	/// <summary>The voiced segments of <paramref name="text"/>, read by <paramref name="narratorVoiceId"/> and the cast.</summary>
	public IReadOnlyList<VoicedSegment> Voice(string text, string narratorVoiceId)
	{
		Cache cache = Resolve(text, narratorVoiceId);
		return cache.Attribution.Segments.Select(s => ToVoicedSegment(s, text, cache.Cast)).ToList();
	}

	private Cache Resolve(string text, string narratorVoiceId)
	{
		Cache? cached = _cache;
		if (cached is not null && cached.Text == text && cached.NarratorVoiceId == narratorVoiceId)
		{
			return cached;
		}

		AttributionResult attribution = _attributor.Attribute(text, _segmenter.Split(text));
		VoiceCast cast = _caster.Cast(attribution.Characters, VoiceOptions, narratorVoiceId);
		var fresh = new Cache(text, narratorVoiceId, attribution, cast);
		_cache = fresh;
		return fresh;
	}

	// Narration, and a quote nobody could be attributed to, are read by the narrator.
	private static VoicedSegment ToVoicedSegment(Segment segment, string story, VoiceCast cast)
	{
		string voiceId = segment.CharacterId is { } id
			? cast.VoiceFor(id) ?? cast.NarratorVoiceId
			: cast.NarratorVoiceId;
		return new VoicedSegment(segment.Text, voiceId, segment.SourceStart, segment.SourceEnd,
			SegmentTextLocator.TextStart(story, segment));
	}
}
