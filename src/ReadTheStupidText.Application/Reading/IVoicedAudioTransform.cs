namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The one hook through which a product shapes a voice on top of the voice id the engine synthesizes with
/// (Slice 129, Decision 136). The shared engine only knows "this text, this voice, this speed"; Dino Reader's
/// character flavours, pitch shifts and animal effects live in <c>DinoReader.Core</c> and arrive as an opaque
/// transform on a <see cref="VoicedSegment"/>. A segment without one (every Read The Stupid Text read) is
/// synthesized and cached exactly as before.
/// </summary>
public interface IVoicedAudioTransform
{
    /// <summary>Part of the sentence cache key: audio shaped differently is a different clip. Never empty. Pace and gap are not in the cache key, so the tag must identify them too.</summary>
    string CacheTag { get; }

    /// <summary>The engine speed the sentence is synthesized at (above 1 is quicker).</summary>
    float Pace { get; }

    /// <summary>The silence between the sentences of a joined chunk.</summary>
    TimeSpan SentenceGap { get; }

    /// <summary>Shapes the engine's mono samples; the result is what is encoded, cached and played.</summary>
    float[] Apply(float[] samples, int sampleRate);
}

/// <summary>What a reader uses when a segment has no <see cref="IVoicedAudioTransform"/>: the engine's own sound.</summary>
public static class VoicedAudio
{
    public const float DefaultPace = 1f;
    public const string NoTag = "";
    public static readonly TimeSpan DefaultSentenceGap = TimeSpan.FromMilliseconds(150);

    /// <summary>The no-op transform: pace 1, the default gap, no tag (so the cache id is the untagged one), samples untouched.</summary>
    public static IVoicedAudioTransform None { get; } = new NoTransform();

    public static float PaceOf(IVoicedAudioTransform? transform) => (transform ?? None).Pace;

    public static TimeSpan GapOf(IVoicedAudioTransform? transform) => (transform ?? None).SentenceGap;

    /// <summary>The cache tag; a real transform must have a non-empty one, since an empty tag would share the plain clip's id.</summary>
    public static string TagOf(IVoicedAudioTransform? transform)
    {
        if (transform is null || ReferenceEquals(transform, None))
        {
            return NoTag;
        }

        return string.IsNullOrEmpty(transform.CacheTag)
            ? throw new InvalidOperationException($"{transform.GetType().Name}.CacheTag must not be null or empty.")
            : transform.CacheTag;
    }

    public static float[] Apply(IVoicedAudioTransform? transform, float[] samples, int sampleRate) =>
        (transform ?? None).Apply(samples, sampleRate);

    private sealed class NoTransform : IVoicedAudioTransform
    {
        public string CacheTag => NoTag;

        public float Pace => DefaultPace;

        public TimeSpan SentenceGap => DefaultSentenceGap;

        public float[] Apply(float[] samples, int sampleRate) => samples;
    }
}
