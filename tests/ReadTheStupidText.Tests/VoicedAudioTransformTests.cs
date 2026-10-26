using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Tests;

public class VoicedAudioTransformTests
{
    private sealed class StubTransform(string tag) : IVoicedAudioTransform
    {
        public string CacheTag => tag;

        public float Pace => 1.1f;

        public TimeSpan SentenceGap => TimeSpan.FromMilliseconds(200);

        public float[] Apply(float[] samples, int sampleRate) => samples;
    }

    [Fact]
    public void A_voiced_segment_carries_its_transform_into_every_chunk_of_the_plan()
    {
        var transform = new StubTransform("Small");
        var segment = new VoicedSegment("Hello there little one.", "M1", 0, 23, 0, transform);

        IReadOnlyList<SpeechChunkPlan> plan = SpeechChunkPlanner.ForSegments([segment]);

        Assert.NotEmpty(plan);
        Assert.All(plan, chunk => Assert.Same(transform, chunk.Transform));
    }

    [Fact]
    public void A_segment_made_without_a_transform_has_none()
    {
        Assert.Null(new VoicedSegment("Hi.", "M1", 0, 3, 0).Transform);
    }

    [Fact]
    public void A_tag_changes_the_cache_key_but_no_tag_keeps_the_old_id()
    {
        var plain = new SentenceCacheKey("Hello.", VoiceEngineKind.Supertonic, "en", "M1", "model", "v1");
        var small = plain with { VoiceTag = "Small" };
        var big = plain with { VoiceTag = "Big" };

        Assert.NotEqual(plain.Id, small.Id);
        Assert.NotEqual(small.Id, big.Id);
        Assert.Equal(plain.Id, (plain with { VoiceTag = VoicedAudio.NoTag }).Id);
        Assert.Equal(plain.TextHash, small.TextHash);
    }

    [Fact]
    public void No_transform_means_the_engine_defaults()
    {
        float[] samples = [0.1f, 0.2f];

        Assert.Equal(VoicedAudio.DefaultPace, VoicedAudio.PaceOf(null));
        Assert.Equal(VoicedAudio.DefaultSentenceGap, VoicedAudio.GapOf(null));
        Assert.Equal(VoicedAudio.NoTag, VoicedAudio.TagOf(null));
        Assert.Same(samples, VoicedAudio.Apply(null, samples, 24000));
    }

    [Fact]
    public void A_transform_with_an_empty_tag_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => VoicedAudio.TagOf(new StubTransform("")));
        Assert.Equal(VoicedAudio.NoTag, VoicedAudio.TagOf(VoicedAudio.None));
    }
}
