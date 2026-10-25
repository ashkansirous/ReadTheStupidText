using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

/// <summary>An animal story character as the caster sees it: which animal, and whether the name says it is young (true), grown (false) or says nothing (null).</summary>
public sealed record AnimalHint(AnimalKind Kind, bool? Young);

/// <summary>How well a storyteller character plays an animal story character (Slice 125).</summary>
public enum AnimalFit
{
    /// <summary>Not an animal character of that animal.</summary>
    None,

    /// <summary>A young animal character of another animal, for a young animal that has no character of its own left.</summary>
    Young,

    /// <summary>The animal's own character of the right age (piglet for a pig, big bear for a grown bear).</summary>
    Exact,
}

/// <summary>
/// Which storyteller characters are animals (Slice 125, Decision 132), by roster id (the ids are the same in every language), and how a story character that is an
/// animal finds its own. A character with a known animal is cast to its animal character, young or grown by the name's words ("little", "baby", "Mrs.", "Papa"),
/// else young for a pig, sheep, goat, duck or rabbit. Pure data and rules, so the casting stays deterministic.
/// </summary>
public static class AnimalCharacters
{
    private sealed record Animal(AnimalKind[] Kinds, bool Young);

    private static readonly Dictionary<string, Animal> ByRosterId = new()
    {
        ["piglet"] = new([AnimalKind.Pig], Young: true),
        ["lamb"] = new([AnimalKind.Sheep, AnimalKind.Goat], Young: true),
        ["puppy"] = new([AnimalKind.Dog], Young: true),
        ["kitten"] = new([AnimalKind.Cat], Young: true),
        ["duckling"] = new([AnimalKind.Duck], Young: true),
        ["bunny"] = new([AnimalKind.Rabbit], Young: true),
        ["cub"] = new([AnimalKind.Bear], Young: true),
        ["mouse"] = new([AnimalKind.Mouse], Young: true),
        ["chick"] = new([AnimalKind.Hen], Young: true),
        ["bee"] = new([AnimalKind.Bee], Young: true),
        ["hen"] = new([AnimalKind.Hen], Young: false),
        ["bear"] = new([AnimalKind.Bear], Young: false),
        ["bigbear"] = new([AnimalKind.Bear], Young: false),
        ["wolf"] = new([AnimalKind.Wolf], Young: false),
        ["ewe"] = new([AnimalKind.Sheep, AnimalKind.Goat], Young: false),
        ["owl"] = new([AnimalKind.Owl], Young: false),
    };

    // The animals that are young unless the name says otherwise (a piglet, a lamb, a duckling, a bunny).
    private static readonly HashSet<AnimalKind> YoungByDefault = [AnimalKind.Pig, AnimalKind.Sheep, AnimalKind.Goat, AnimalKind.Duck, AnimalKind.Rabbit];

    /// <summary>The roster ids of all animal characters.</summary>
    public static IReadOnlyCollection<string> RosterIds => ByRosterId.Keys;

    /// <summary>The animal a roster character plays and whether it is a young one, or null for a character that is not an animal.</summary>
    public static (IReadOnlyList<AnimalKind> Kinds, bool Young)? Of(string rosterId)
    {
        return ByRosterId.TryGetValue(rosterId, out Animal? animal) ? (animal.Kinds, animal.Young) : null;
    }

    /// <summary>The hint for a story character known to be <paramref name="kind"/>: young or grown when its name says so.</summary>
    public static AnimalHint HintFor(AnimalKind kind, string name, string? language)
    {
        return new AnimalHint(kind, AgeOf(name, language));
    }

    /// <summary>Whether the name says the character is young (true), grown (false) or neither (null). Grown words ("Mrs.", "Papa", "old") come first, then the flavour the name guesses.</summary>
    public static bool? AgeOf(string name, string? language)
    {
        LanguageRules rules = LanguageRules.For(language);
        if (AnimalAgeWords.MentionsGrown(name, language, rules.Script))
        {
            return false;
        }

        return FlavourGuess.Guess(name, rules) switch
        {
            VoiceFlavour.Small or VoiceFlavour.Squeaky or VoiceFlavour.Bouncy => true,
            VoiceFlavour.Big or VoiceFlavour.Giant or VoiceFlavour.Grumpy or VoiceFlavour.Sleepy => false,
            _ => null,
        };
    }

    /// <summary>How well <paramref name="character"/> plays the animal of <paramref name="hint"/>.</summary>
    public static AnimalFit FitOf(StorytellerCharacter character, AnimalHint hint)
    {
        if (!ByRosterId.TryGetValue(character.Id, out Animal? animal))
        {
            return AnimalFit.None;
        }

        if (ExactOnes(hint).Contains(character.Id))
        {
            return AnimalFit.Exact;
        }

        return animal.Young && WantsYoung(hint) ? AnimalFit.Young : AnimalFit.None;
    }

    private static bool WantsYoung(AnimalHint hint)
    {
        return hint.Young ?? YoungByDefault.Contains(hint.Kind);
    }

    // The animal's own characters of the age wanted. A name that states the age gets only that age (no grown pig: a Mother Pig is cast like any mother); a name that says
    // nothing gets the animal's usual age, or whichever the animal has.
    private static IEnumerable<string> ExactOnes(AnimalHint hint)
    {
        var own = ByRosterId.Where(a => a.Value.Kinds.Contains(hint.Kind)).ToList();
        bool? age = hint.Young ?? (YoungByDefault.Contains(hint.Kind) ? true : null);
        if (age is null)
        {
            return own.Select(a => a.Key);
        }

        var ofAge = own.Where(a => a.Value.Young == age).Select(a => a.Key).ToList();
        return ofAge.Count > 0 || hint.Young is not null ? ofAge : own.Select(a => a.Key);
    }
}
