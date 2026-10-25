using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Tests;

public class PitchShifterTests
{
    private const int Rate = 24000;
    private const double Tolerance = 0.04;

    private static float[] Tone(double hertz, double seconds)
    {
        var samples = new float[(int)(Rate * seconds)];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(0.5 * Math.Sin(2 * Math.PI * hertz * i / Rate));
        }

        return samples;
    }

    // Counts upward zero crossings away from the ends (the first and last frame are fades), so the
    // result is the tone's frequency in hertz.
    private static double Frequency(float[] samples)
    {
        int from = samples.Length / 5;
        int to = samples.Length - samples.Length / 5;
        int crossings = 0;
        for (int i = from + 1; i < to; i++)
        {
            if (samples[i - 1] < 0 && samples[i] >= 0)
            {
                crossings++;
            }
        }

        return crossings / ((to - from) / (double)Rate);
    }

    [Theory]
    [InlineData(3.0)]
    [InlineData(4.0)]
    [InlineData(-3.0)]
    [InlineData(-1.5)]
    public void The_pitch_moves_by_the_semitones_and_the_length_stays(double semitones)
    {
        float[] tone = Tone(220, 1.5);

        float[] shifted = PitchShifter.Shift(tone, Rate, semitones);

        Assert.Equal(tone.Length, shifted.Length);
        double expected = 220 * Math.Pow(2, semitones / 12);
        Assert.InRange(Frequency(shifted), expected * (1 - Tolerance), expected * (1 + Tolerance));
    }

    [Fact]
    public void A_level_step_keeps_the_pitch_on_both_sides_of_it()
    {
        float[] tone = Tone(220, 2.0);
        int step = tone.Length / 2;
        for (int i = 0; i < step; i++)
        {
            tone[i] *= 0.1f;
        }

        float[] shifted = PitchShifter.Shift(tone, Rate, 4.0);

        double expected = 220 * Math.Pow(2, 4.0 / 12);
        Assert.InRange(FrequencyBetween(shifted, tone.Length / 5, step - tone.Length / 20), expected * (1 - Tolerance), expected * (1 + Tolerance));
        Assert.InRange(FrequencyBetween(shifted, step + tone.Length / 20, tone.Length - tone.Length / 5), expected * (1 - Tolerance), expected * (1 + Tolerance));
    }

    private static double FrequencyBetween(float[] samples, int from, int to)
    {
        int crossings = 0;
        for (int i = from + 1; i < to; i++)
        {
            if (samples[i - 1] < 0 && samples[i] >= 0)
            {
                crossings++;
            }
        }

        return crossings / ((to - from) / (double)Rate);
    }

    [Fact]
    public void A_short_clip_keeps_its_onset()
    {
        float[] shifted = PitchShifter.Shift(Tone(220, 0.2), Rate, 3);

        float onset = shifted.Take(Rate / 100).Max(Math.Abs);
        Assert.InRange(onset, 0.2f, 0.65f);
    }

    [Fact]
    public void A_shift_of_zero_returns_the_samples_untouched()
    {
        float[] tone = Tone(220, 0.5);

        Assert.Same(tone, PitchShifter.Shift(tone, Rate, 0));
    }

    [Fact]
    public void Very_short_audio_is_returned_as_it_is()
    {
        float[] tiny = new float[40];

        Assert.Same(tiny, PitchShifter.Shift(tiny, Rate, 3));
    }

    [Fact]
    public void The_shifted_audio_keeps_its_loudness_and_stays_in_range()
    {
        float[] shifted = PitchShifter.Shift(Tone(220, 1.0), Rate, 4);

        float peak = shifted.Max(Math.Abs);
        Assert.InRange(peak, 0.35f, 0.65f);
    }

    [Fact]
    public void Silence_stays_silent()
    {
        float[] shifted = PitchShifter.Shift(new float[Rate], Rate, 3);

        Assert.All(shifted, s => Assert.Equal(0f, s));
    }

    [Fact]
    public void A_bleat_wobbles_the_voice_but_keeps_its_length_and_loudness()
    {
        float[] tone = Tone(220, 1.0);

        float[] bleat = PitchShifter.Apply(tone, Rate, VoiceFlavours.ShapeOf(VoiceFlavour.Bleat));

        Assert.Equal(tone.Length, bleat.Length);
        Assert.NotEqual(tone, bleat);
        Assert.InRange(bleat.Max(Math.Abs), 0.3f, 0.55f);
    }

    [Fact]
    public void A_flavour_without_a_wobble_is_left_as_the_pitch_shift_made_it() =>
        Assert.Equal(Tone(220, 0.5), PitchShifter.Apply(Tone(220, 0.5), Rate, VoiceFlavours.ShapeOf(VoiceFlavour.Normal)));
}

public class VoiceFlavourTests
{
    [Fact]
    public void Normal_changes_nothing()
    {
        FlavourShape shape = VoiceFlavours.ShapeOf(VoiceFlavour.Normal);

        Assert.Equal(0, shape.Semitones);
        Assert.Equal(1f, shape.Pace);
    }

    [Theory]
    [InlineData(VoiceFlavour.Small, true, true)]
    [InlineData(VoiceFlavour.Bouncy, true, true)]
    [InlineData(VoiceFlavour.Big, false, false)]
    [InlineData(VoiceFlavour.Sleepy, false, false)]
    public void Small_and_bouncy_are_higher_and_quicker_big_and_sleepy_lower_and_slower(VoiceFlavour flavour, bool higher, bool quicker)
    {
        FlavourShape shape = VoiceFlavours.ShapeOf(flavour);

        Assert.Equal(higher, shape.Semitones > 0);
        Assert.Equal(quicker, shape.Pace > 1f);
    }

    [Fact]
    public void Every_shift_stays_within_four_semitones()
    {
        foreach (VoiceFlavour flavour in Enum.GetValues<VoiceFlavour>())
        {
            Assert.InRange(VoiceFlavours.ShapeOf(flavour).Semitones, -4.0, 4.0);
        }
    }

    [Fact]
    public void The_storyteller_is_slower_than_normal_with_no_pitch_change_and_warmer_pauses()
    {
        FlavourShape storyteller = VoiceFlavours.ShapeOf(VoiceFlavour.Storyteller);

        Assert.Equal(0, storyteller.Semitones);
        Assert.InRange(storyteller.Pace, 0.9f, 0.99f);
        Assert.True(storyteller.SentenceGap > VoiceFlavours.ShapeOf(VoiceFlavour.Normal).SentenceGap);
    }

    [Fact]
    public void The_storyteller_is_not_offered_as_a_choice()
    {
        Assert.DoesNotContain(VoiceFlavour.Storyteller, VoiceFlavours.Pickable);
        Assert.Equal(5, VoiceFlavours.Pickable.Count);
    }

    [Fact]
    public void A_voiced_segment_carries_its_flavour_into_every_chunk_of_the_plan()
    {
        var segment = new VoicedSegment("Hello there little one.", "M1", 0, 23, 0, VoiceFlavour.Small);

        IReadOnlyList<SpeechChunkPlan> plan = SpeechChunkPlanner.ForSegments([segment]);

        Assert.NotEmpty(plan);
        Assert.All(plan, chunk => Assert.Equal(VoiceFlavour.Small, chunk.Flavour));
    }

    [Fact]
    public void A_segment_made_without_a_flavour_is_normal()
    {
        Assert.Equal(VoiceFlavour.Normal, new VoicedSegment("Hi.", "M1", 0, 3, 0).Flavour);
    }

    [Fact]
    public void A_flavour_changes_the_cache_key_but_normal_keeps_the_old_id()
    {
        var normal = new SentenceCacheKey("Hello.", VoiceEngineKind.Supertonic, "en", "M1", "model", "v1");
        var small = normal with { Flavour = VoiceFlavour.Small };
        var big = normal with { Flavour = VoiceFlavour.Big };

        Assert.NotEqual(normal.Id, small.Id);
        Assert.NotEqual(small.Id, big.Id);
        Assert.Equal(normal.Id, (normal with { Flavour = VoiceFlavour.Normal }).Id);
        Assert.Equal(normal.TextHash, small.TextHash);
    }
}
