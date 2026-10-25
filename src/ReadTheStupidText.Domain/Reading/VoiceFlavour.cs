namespace ReadTheStupidText.Domain.Reading;

/// <summary>
/// How a character's voice is shaped on top of the voice that speaks it (Slice 113, Decision 117): a fixed
/// pitch shift and pace. A closed set (Slice 116 added Squeaky, Grumpy, Whisper and Giant for the storyteller characters). <see cref="Storyteller"/> is the narrator's own flavour: never guessed
/// or offered to a parent as a choice for a character. RTST always plays <see cref="Normal"/>.
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
}

/// <summary>The pitch (semitones), pace (engine speed, above 1 is quicker) and sentence gap of a flavour.</summary>
public readonly record struct FlavourShape(double Semitones, float Pace, TimeSpan SentenceGap, double WobbleDepth = 0);

/// <summary>The shape table for <see cref="VoiceFlavour"/>. Pitch stays within about +-4 semitones so voices don't turn into chipmunks.</summary>
public static class VoiceFlavours
{
    private static readonly TimeSpan NormalGap = TimeSpan.FromMilliseconds(150);
    private const double BleatWobble = 0.03;
    private static readonly TimeSpan StorytellerGap = TimeSpan.FromMilliseconds(260);

    /// <summary>The flavours a parent can pick for a character, in menu order (the narrator's Storyteller is not one).</summary>
    public static IReadOnlyList<VoiceFlavour> Pickable { get; } =
        [VoiceFlavour.Normal, VoiceFlavour.Small, VoiceFlavour.Big, VoiceFlavour.Bouncy, VoiceFlavour.Sleepy];

    /// <summary>Every flavour a storyteller character may carry and a stored cast may hold (Slice 116): all but the narrator's own Storyteller.</summary>
    public static IReadOnlyList<VoiceFlavour> Assignable { get; } =
        [.. Pickable, VoiceFlavour.Squeaky, VoiceFlavour.Grumpy, VoiceFlavour.Whisper, VoiceFlavour.Giant];

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
        VoiceFlavour.Bleat => new FlavourShape(2.0, 1.0f, NormalGap, BleatWobble),
        VoiceFlavour.BleatLamb => new FlavourShape(3.5, 1.05f, NormalGap, BleatWobble),
        VoiceFlavour.Storyteller => new FlavourShape(0.0, 0.94f, StorytellerGap),
        _ => new FlavourShape(0.0, 1.0f, NormalGap),
    };
}
