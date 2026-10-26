using ReadTheStupidText.Application.Reading;

namespace DinoReader.Core;

/// <summary>
/// A <see cref="VoiceFlavour"/> as the shared engine's opaque voice hook (Slice 129, Decision 136): the flavour's
/// pace and sentence gap, a pitch shift (and a bleat's wobble) on the samples, and the flavour's name as the cache tag.
/// </summary>
public sealed class FlavourTransform : IVoicedAudioTransform
{
    private static readonly IReadOnlyDictionary<VoiceFlavour, FlavourTransform> All =
        Enum.GetValues<VoiceFlavour>().Where(f => f != VoiceFlavour.Normal)
            .ToDictionary(f => f, f => new FlavourTransform(f));

    private readonly FlavourShape _shape;

    private FlavourTransform(VoiceFlavour flavour)
    {
        CacheTag = flavour.ToString();
        _shape = VoiceFlavours.ShapeOf(flavour);
    }

    /// <summary>The transform for a flavour; null for <see cref="VoiceFlavour.Normal"/> (the engine's own sound, and the cache id it always had).</summary>
    public static IVoicedAudioTransform? For(VoiceFlavour flavour) => All.GetValueOrDefault(flavour);

    public string CacheTag { get; }

    public float Pace => _shape.Pace;

    public TimeSpan SentenceGap => _shape.SentenceGap;

    public float[] Apply(float[] samples, int sampleRate) => PitchShifter.Apply(samples, sampleRate, _shape);
}
