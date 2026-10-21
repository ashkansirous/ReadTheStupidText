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
	// a second read of the same text (or a narrator change) does not need it again.
	private sealed record Cache(string Text, AttributionResult Attribution);

	private volatile Cache? _cache;

	/// <summary>
	/// <see cref="Voice"/> on a worker thread: segmenting and attributing a long story is CPU work
	/// that must not block the UI.
	/// </summary>
	public Task<IReadOnlyList<VoicedSegment>> VoiceAsync(Story story, string narratorVoiceId) =>
		Task.Run(() => Voice(story, narratorVoiceId));

	/// <summary>The cast for <paramref name="text"/>, keeping any voice <paramref name="stored"/> already gives a character.</summary>
	public Task<IReadOnlyList<CastMember>> CastAsync(string text, string narratorVoiceId, IReadOnlyList<CastMember> stored) =>
		Task.Run(() => CastFor(text, narratorVoiceId, stored));

	/// <summary>The voiced segments of the story, read by <paramref name="narratorVoiceId"/> and the story's cast.</summary>
	public IReadOnlyList<VoicedSegment> Voice(Story story, string narratorVoiceId)
	{
		string text = story.Text;
		AttributionResult attribution = Attribute(text);
		VoiceCast cast = Cast(attribution, narratorVoiceId, story.Cast);
		return attribution.Segments.Select(s => ToVoicedSegment(s, text, cast)).ToList();
	}

	public IReadOnlyList<CastMember> CastFor(string text, string narratorVoiceId, IReadOnlyList<CastMember> stored)
	{
		AttributionResult attribution = Attribute(text);
		return StoryCast.ToMembers(attribution.Characters, Cast(attribution, narratorVoiceId, stored));
	}

	private VoiceCast Cast(AttributionResult attribution, string narratorVoiceId, IReadOnlyList<CastMember> stored) =>
		_caster.Cast(attribution.Characters, VoiceOptions, narratorVoiceId, StoryCast.Pinned(stored));

	private AttributionResult Attribute(string text)
	{
		Cache? cached = _cache;
		if (cached is not null && cached.Text == text)
		{
			return cached.Attribution;
		}

		AttributionResult attribution = _attributor.Attribute(text, _segmenter.Split(text));
		_cache = new Cache(text, attribution);
		return attribution;
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
