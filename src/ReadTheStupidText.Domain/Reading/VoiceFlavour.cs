namespace ReadTheStupidText.Domain.Reading;

/// <summary>
/// How a character's voice is shaped on top of the voice that speaks it (Slice 113, Decision 117): a fixed
/// pitch shift and pace. A closed set. <see cref="Storyteller"/> is the narrator's own flavour: never guessed
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
}

/// <summary>The pitch (semitones), pace (engine speed, above 1 is quicker) and sentence gap of a flavour.</summary>
public readonly record struct FlavourShape(double Semitones, float Pace, TimeSpan SentenceGap);

/// <summary>The shape table for <see cref="VoiceFlavour"/>. Pitch stays within about +-4 semitones so voices don't turn into chipmunks.</summary>
public static class VoiceFlavours
{
    private static readonly TimeSpan NormalGap = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan StorytellerGap = TimeSpan.FromMilliseconds(260);

    /// <summary>The flavours a parent can pick for a character, in menu order (the narrator's Storyteller is not one).</summary>
    public static IReadOnlyList<VoiceFlavour> Pickable { get; } =
        [VoiceFlavour.Normal, VoiceFlavour.Small, VoiceFlavour.Big, VoiceFlavour.Bouncy, VoiceFlavour.Sleepy];

    public static FlavourShape ShapeOf(VoiceFlavour flavour) => flavour switch
    {
        VoiceFlavour.Small => new FlavourShape(3.0, 1.08f, NormalGap),
        VoiceFlavour.Big => new FlavourShape(-3.0, 0.92f, NormalGap),
        VoiceFlavour.Bouncy => new FlavourShape(4.0, 1.15f, NormalGap),
        VoiceFlavour.Sleepy => new FlavourShape(-1.5, 0.85f, NormalGap),
        VoiceFlavour.Storyteller => new FlavourShape(0.0, 0.94f, StorytellerGap),
        _ => new FlavourShape(0.0, 1.0f, NormalGap),
    };
}
