using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

/// <summary>How old a character is (Slice 156, Decision 161). A closed set: a book's author picks one, and every roster character has one.</summary>
public enum CharacterAge
{
    Baby,
    Child,
    Young,
    Adult,
    Old,
}

/// <summary>What a character is when it is not an animal that has a sound (Slice 156): a person or a fantasy kind. An animal story character's kind is its <see cref="CastMember.Animal"/> (Slice 152), never a second field here;
/// <see cref="Fox"/> is the one animal with no sound word (<see cref="AnimalKind"/> has none), so its roster character needs a kind.</summary>
public enum CharacterBeing
{
    Person,
    Pirate,
    Knight,
    Jester,
    Troll,
    Giant,
    Dragon,
    Fairy,
    Witch,
    Robot,
    Fox,
}

/// <summary>How a character's voice should sound (Slice 156): plain words, so an author can say "a squawky seagull" without naming an engine voice.</summary>
public enum VoiceQuality
{
    Squawky,
    Squeaky,
    Bouncy,
    Gruff,
    Soft,
    Booming,
    Sleepy,
    Sly,
    Bold,
    Warm,
    Whispery,
    Bright,
}

/// <summary>
/// A character's traits as a book's author states them (Slice 156, Decision 161), which stay correct whatever voice the app ends up using: <see cref="Age"/>, what it is
/// (<see cref="Being"/>, null for an animal, whose kind is <see cref="CastMember.Animal"/>) and its <see cref="Qualities"/>. The gender is the cast member's own. Every part is optional, so
/// <c>schemaVersion</c> stays 1.
/// </summary>
public sealed record CharacterTraits(CharacterAge? Age = null, CharacterBeing? Being = null, IReadOnlyList<VoiceQuality>? Qualities = null)
{
    /// <summary>Equal when the age, kind and the qualities (in order) are equal, so a stored cast compares by value.</summary>
    public bool Equals(CharacterTraits? other) =>
        other is not null && Age == other.Age && Being == other.Being && (Qualities ?? []).SequenceEqual(other.Qualities ?? []);

    public override int GetHashCode() => HashCode.Combine(Age, Being, (Qualities ?? []).Count);
}

/// <summary>
/// The traits of the storyteller characters, by roster id (the ids are the same in every language), and how well a roster character fits a story character's traits (Slice 156):
/// the fallback when the book's suggested voice is not in the language's roster. Ranked kind, then gender, then age, then qualities; an opposite gender counts more than a different kind
/// (<see cref="KindMiss"/> is below <see cref="CharacterRoster.GenderMisfit"/>'s 50), because a wrong-gender voice is worse than a different animal.
/// </summary>
public static class CharacterTraitMatch
{
    private const int KindMiss = 40;
    private const int AgeStep = 12;
    // Old against Young, Child or Baby is a different kind of voice (an old owl sounding like a chick), so it counts more than a kind miss: 2 * AgeStep + this is above KindMiss.
    private const int OldYoungExtra = 20;
    private const int QualityMiss = 3;
    // A place whose flavour is the opposite of a wanted quality (a sleepy ewe for a bold girl) is worse than the wrong kind.
    private const int ContradictionMiss = 50;
    private static readonly (VoiceQuality[] Wanted, VoiceFlavour[] Flavours)[] Contradictions =
    [
        ([VoiceQuality.Bold, VoiceQuality.Bright], [VoiceFlavour.Sleepy, VoiceFlavour.Hushed]),
        ([VoiceQuality.Booming, VoiceQuality.Gruff], [VoiceFlavour.Squeaky, VoiceFlavour.Small]),
    ];

    private sealed record Roster(CharacterAge Age, CharacterBeing? Being, VoiceQuality[] Qualities);

    private static readonly Dictionary<string, Roster> ByRosterId = new()
    {
        ["granny"] = new(CharacterAge.Old, CharacterBeing.Person, [VoiceQuality.Warm, VoiceQuality.Soft]),
        ["grandad"] = new(CharacterAge.Old, CharacterBeing.Person, [VoiceQuality.Warm, VoiceQuality.Booming]),
        ["whisperer"] = new(CharacterAge.Adult, CharacterBeing.Person, [VoiceQuality.Whispery, VoiceQuality.Soft, VoiceQuality.Sleepy]),
        ["storyteller"] = new(CharacterAge.Adult, CharacterBeing.Person, [VoiceQuality.Warm]),
        ["night-storyteller"] = new(CharacterAge.Adult, CharacterBeing.Person, [VoiceQuality.Soft, VoiceQuality.Sleepy]),
        ["deep-storyteller"] = new(CharacterAge.Adult, CharacterBeing.Person, [VoiceQuality.Booming, VoiceQuality.Warm]),
        ["troll"] = new(CharacterAge.Adult, CharacterBeing.Troll, [VoiceQuality.Gruff]),
        ["giant"] = new(CharacterAge.Adult, CharacterBeing.Giant, [VoiceQuality.Booming, VoiceQuality.Warm]),
        ["bear"] = new(CharacterAge.Old, null, [VoiceQuality.Warm, VoiceQuality.Booming]),
        ["fox"] = new(CharacterAge.Adult, CharacterBeing.Fox, [VoiceQuality.Sly, VoiceQuality.Whispery]),
        ["knight"] = new(CharacterAge.Adult, CharacterBeing.Knight, [VoiceQuality.Bold]),
        ["owl"] = new(CharacterAge.Old, null, [VoiceQuality.Sleepy, VoiceQuality.Soft]),
        ["mouse"] = new(CharacterAge.Young, null, [VoiceQuality.Squeaky]),
        ["jester"] = new(CharacterAge.Adult, CharacterBeing.Jester, [VoiceQuality.Bouncy, VoiceQuality.Bright]),
        ["dragon"] = new(CharacterAge.Adult, CharacterBeing.Dragon, [VoiceQuality.Booming, VoiceQuality.Gruff]),
        ["mummy"] = new(CharacterAge.Adult, CharacterBeing.Person, [VoiceQuality.Warm]),
        ["bunny"] = new(CharacterAge.Young, null, [VoiceQuality.Soft]),
        ["bee"] = new(CharacterAge.Young, null, [VoiceQuality.Bouncy, VoiceQuality.Bright]),
        ["princess"] = new(CharacterAge.Young, CharacterBeing.Person, [VoiceQuality.Bright, VoiceQuality.Bold]),
        ["witch"] = new(CharacterAge.Old, CharacterBeing.Witch, [VoiceQuality.Gruff, VoiceQuality.Sly]),
        ["fairy"] = new(CharacterAge.Young, CharacterBeing.Fairy, [VoiceQuality.Squeaky, VoiceQuality.Bright]),
        ["hen"] = new(CharacterAge.Old, null, [VoiceQuality.Sleepy, VoiceQuality.Warm]),
        ["chick"] = new(CharacterAge.Young, null, [VoiceQuality.Bouncy, VoiceQuality.Squeaky]),
        ["gardener"] = new(CharacterAge.Adult, CharacterBeing.Person, [VoiceQuality.Gruff]),
        ["lamb"] = new(CharacterAge.Young, null, [VoiceQuality.Soft]),
        ["lamb2"] = new(CharacterAge.Young, null, [VoiceQuality.Soft, VoiceQuality.Bright]),
        ["puppy"] = new(CharacterAge.Young, null, [VoiceQuality.Bouncy]),
        ["puppy2"] = new(CharacterAge.Young, null, [VoiceQuality.Bouncy, VoiceQuality.Warm]),
        ["kitten"] = new(CharacterAge.Young, null, [VoiceQuality.Soft]),
        ["kitten2"] = new(CharacterAge.Young, null, [VoiceQuality.Soft, VoiceQuality.Warm]),
        ["duckling"] = new(CharacterAge.Young, null, [VoiceQuality.Squeaky, VoiceQuality.Squawky]),
        ["duckling2"] = new(CharacterAge.Young, null, [VoiceQuality.Bouncy, VoiceQuality.Squawky]),
        ["cub"] = new(CharacterAge.Young, null, [VoiceQuality.Soft]),
        ["bigbear"] = new(CharacterAge.Adult, null, [VoiceQuality.Booming]),
        ["wolf"] = new(CharacterAge.Adult, null, [VoiceQuality.Gruff]),
        ["ewe"] = new(CharacterAge.Adult, null, [VoiceQuality.Soft, VoiceQuality.Sleepy]),
        ["piglet"] = new(CharacterAge.Young, null, [VoiceQuality.Squeaky, VoiceQuality.Bouncy]),
        ["piglet2"] = new(CharacterAge.Young, null, [VoiceQuality.Squeaky]),
        ["piglet3"] = new(CharacterAge.Young, null, [VoiceQuality.Bright, VoiceQuality.Squeaky]),
    };

    // The animals that all count as "a bird" for a story character the roster has no exact bird for (a seagull is played by a duckling).
    private static readonly HashSet<AnimalKind> BirdLike = [AnimalKind.Bird, AnimalKind.Duck, AnimalKind.Hen, AnimalKind.Rooster, AnimalKind.Owl];

    /// <summary>Whether <paramref name="rosterId"/> has traits (every roster character must; a test checks every language's roster).</summary>
    public static bool Has(string rosterId) => ByRosterId.ContainsKey(rosterId);

    /// <summary>The age of a roster character, or null for an id with no traits.</summary>
    public static CharacterAge? AgeOf(string rosterId) => ByRosterId.TryGetValue(rosterId, out Roster? r) ? r.Age : null;

    /// <summary>The voice qualities of a roster character (none for an id with no traits).</summary>
    public static IReadOnlyList<VoiceQuality> QualitiesOf(string rosterId) => ByRosterId.TryGetValue(rosterId, out Roster? r) ? r.Qualities : [];

    /// <summary>What a roster character is: its <see cref="CharacterBeing"/> when it is a person or fantasy kind, null for an animal (see <see cref="AnimalCharacters.Of"/>).</summary>
    public static CharacterBeing? BeingOf(string rosterId) => ByRosterId.TryGetValue(rosterId, out Roster? r) ? r.Being : null;

    /// <summary>
    /// How far <paramref name="candidate"/> is from a story character of <paramref name="gender"/> with <paramref name="traits"/> (and, for an animal, <paramref name="animal"/>): 0 fits perfectly.
    /// Kind, then gender, then age, then voice qualities (see the type's remarks for the weights).
    /// </summary>
    public static int Cost(StorytellerCharacter candidate, Gender gender, CharacterTraits traits, AnimalKind? animal)
    {
        int cost = CharacterRoster.GenderMisfit(candidate.Gender, gender) + KindCost(candidate, traits, animal);
        if (traits.Age is { } wanted && AgeOf(candidate.Id) is { } has)
        {
            cost += AgeCost(wanted, has);
        }

        cost += Contradicts(candidate, traits) ? ContradictionMiss : 0;
        return cost + (traits.Qualities ?? []).Count(q => !QualitiesOf(candidate.Id).Contains(q)) * QualityMiss;
    }

    private static bool Contradicts(StorytellerCharacter candidate, CharacterTraits traits) =>
        Contradictions.Any(c => c.Flavours.Contains(candidate.Flavour) && (traits.Qualities ?? []).Any(c.Wanted.Contains));

    private static int AgeCost(CharacterAge wanted, CharacterAge has)
    {
        int cost = Math.Abs((int)wanted - (int)has) * AgeStep;
        bool oldAgainstYoung = (wanted == CharacterAge.Old && has <= CharacterAge.Young) || (has == CharacterAge.Old && wanted <= CharacterAge.Young);
        return oldAgainstYoung ? cost + OldYoungExtra : cost;
    }

    private static int KindCost(StorytellerCharacter candidate, CharacterTraits traits, AnimalKind? animal)
    {
        if (animal is { } kind)
        {
            return AnimalCharacters.Of(candidate.Id)?.Kinds.Any(k => k == kind || (BirdLike.Contains(k) && BirdLike.Contains(kind))) == true ? 0 : KindMiss;
        }

        return traits.Being is { } being && BeingOf(candidate.Id) != being ? KindMiss : 0;
    }
}
