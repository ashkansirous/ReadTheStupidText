namespace DinoReader.Core;

/// <summary>
/// How a character's voice is shaped on top of the voice that speaks it (Slice 113, Decision 117): a fixed
/// pitch shift and pace. A closed set (Slice 116 added Squeaky, Grumpy, Whisper and Giant for the storyteller characters). <see cref="Storyteller"/> is the narrator's own flavour: never guessed
/// or offered to a parent as a choice for a character. RTST never uses flavours (Slice 129: they live here, and reach the shared engine as an <c>IVoicedAudioTransform</c>).
/// </summary>
public enum VoiceFlavour
{
    Normal,
    Small,
    Big,
    Bouncy,
    Sleepy,
    Storyteller,
    Squeaky,
    Grumpy,
    Whisper,
    Giant,

    /// <summary>A sheep's or goat's bleat (Slice 117): the voice a little higher with a faint wobble. Only the animal sound uses it; never guessed or offered.</summary>
    Bleat,

    /// <summary>A lamb's or kid's bleat: <see cref="Bleat"/>, higher.</summary>
    BleatLamb,

    /// <summary>Slice 144: a bright, quick voice a little above normal. Only the Farsi roster uses it, to give its few voices more distinct characters; never picked from the menu.</summary>
    Chirpy,

    /// <summary>Slice 144: a soft, slightly lifted, unhurried voice for a whispery little one (Farsi roster only; never picked from the menu).</summary>
    Hushed,

    /// <summary>Slice 144: a slightly low, easy voice for a calm, sleepy animal (Farsi roster only; not a young-sounding one).</summary>
    Mellow,

    /// <summary>Slice 144: a lifted, lively voice (Farsi roster only).</summary>
    Perky,

    /// <summary>Slice 144: a gentle, simple voice a touch above normal and unhurried, for a calm little one (Farsi roster only; not counted as young-sounding).</summary>
    Gentle,
}

/// <summary>The pitch (semitones), pace (engine speed, above 1 is quicker) and sentence gap of a flavour.</summary>
public readonly record struct FlavourShape(double Semitones, float Pace, TimeSpan SentenceGap, double WobbleDepth = 0);

/// <summary>The shape table for <see cref="VoiceFlavour"/>. Pitch stays within about +-4 semitones so voices don't turn into chipmunks.</summary>
public static class VoiceFlavours
{
    private static readonly TimeSpan NormalGap = ReadTheStupidText.Application.Reading.VoicedAudio.DefaultSentenceGap;
    private const double BleatWobble = 0.03;
    private static readonly TimeSpan StorytellerGap = TimeSpan.FromMilliseconds(260);

    /// <summary>The flavours a parent can pick for a character, in menu order (the narrator's Storyteller is not one).</summary>
    public static IReadOnlyList<VoiceFlavour> Pickable { get; } =
        [VoiceFlavour.Normal, VoiceFlavour.Small, VoiceFlavour.Big, VoiceFlavour.Bouncy, VoiceFlavour.Sleepy];

    /// <summary>Every flavour a storyteller character may carry and a stored cast may hold (Slice 116): all but the narrator's own Storyteller.</summary>
    public static IReadOnlyList<VoiceFlavour> Assignable { get; } =
        [.. Pickable, VoiceFlavour.Squeaky, VoiceFlavour.Grumpy, VoiceFlavour.Whisper, VoiceFlavour.Giant, VoiceFlavour.Chirpy, VoiceFlavour.Hushed, VoiceFlavour.Mellow, VoiceFlavour.Perky, VoiceFlavour.Gentle];

    public static FlavourShape ShapeOf(VoiceFlavour flavour) => flavour switch
    {
        VoiceFlavour.Small => new FlavourShape(3.0, 1.08f, NormalGap),
        VoiceFlavour.Big => new FlavourShape(-3.0, 0.92f, NormalGap),
        VoiceFlavour.Bouncy => new FlavourShape(4.0, 1.15f, NormalGap),
        VoiceFlavour.Sleepy => new FlavourShape(-1.5, 0.85f, NormalGap),
        VoiceFlavour.Squeaky => new FlavourShape(4.0, 1.12f, NormalGap),
        VoiceFlavour.Grumpy => new FlavourShape(-2.5, 0.95f, NormalGap),
        VoiceFlavour.Whisper => new FlavourShape(-0.5, 0.88f, StorytellerGap),
        VoiceFlavour.Giant => new FlavourShape(-4.0, 0.86f, StorytellerGap),
        VoiceFlavour.Chirpy => new FlavourShape(2.0, 1.22f, NormalGap),
        VoiceFlavour.Hushed => new FlavourShape(1.5, 0.9f, StorytellerGap),
        VoiceFlavour.Mellow => new FlavourShape(-1.0, 1.05f, NormalGap),
        VoiceFlavour.Perky => new FlavourShape(2.5, 1.08f, NormalGap),
        VoiceFlavour.Gentle => new FlavourShape(1.0, 0.97f, NormalGap),
        VoiceFlavour.Bleat => new FlavourShape(2.0, 1.0f, NormalGap, BleatWobble),
        VoiceFlavour.BleatLamb => new FlavourShape(3.5, 1.05f, NormalGap, BleatWobble),
        VoiceFlavour.Storyteller => new FlavourShape(0.0, 0.94f, StorytellerGap),
        _ => new FlavourShape(0.0, 1.0f, NormalGap),
    };
}
