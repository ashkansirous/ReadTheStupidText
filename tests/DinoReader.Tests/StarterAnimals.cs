using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>
/// The animals of the characters whose names do not say it (Slice 125, Decision 132): the builders store them in the book's cast, so the pigs Pip, Pop and Pim oink
/// and are cast as piglets. A character whose name has an animal word (Wolf, Papa Bear, Mrs. Rabbit) is found from the name and needs no entry here.
/// A character stored as no animal (<see cref="CastMember.NotAnimal"/>) is cast by gender like a person in every language: the Ugly Duckling (really a swan) and his
/// sister Dilly, whose rosters' duckling voices are one gender per language and would otherwise give him a girl's voice or her a boy's; Peter Rabbit (he), with the bunny voice, and Town Mouse (she) and Country Mouse (he), with the mouse voice (the cost is that they make
/// no meow, quack, squeak or rabbit sounds); and Goldilocks' Mama Bear, since every roster's bear voices are men's. The stored animal also picks the voice, so giving them back their animal
/// would put a man's or the wrong-gender voice on them (56 gender tests fail): a separate "cast as a person" field is needed first (Slice 152 review).
/// A book whose writer chose each voice (<see cref="StarterVoices"/>, Slice 156) needs none of this: the Little Red Hen's Cat and Duck have their animals back, and their gender comes from the book.
/// </summary>
internal static class StarterAnimals
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, AnimalKind>> ByStory = new Dictionary<string, IReadOnlyDictionary<string, AnimalKind>>
    {
        [StarterTexts.PigsId] = Named(AnimalKind.Pig, "Pip", "Pop", "Pim"),
        [StoreTexts.BillyGoatsId] = Named(AnimalKind.Goat, "Little Billy", "Middle Billy", "Big Billy"),
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> NotAnimalsByStory = new Dictionary<string, IReadOnlySet<string>>
    {
        [StarterTexts.GingerbreadId] = new HashSet<string> { "Grandpa", "Granny" }, // the text guesses animals from "Grandpa ... the cow"; they are people
        [StoreTexts.UglyDucklingId] = new HashSet<string> { "Duckling", "Dilly" },
        [StoreTexts.GoldilocksId] = new HashSet<string> { "Mama Bear" },
        [StoreTexts.PeterRabbitId] = new HashSet<string> { "Peter" }, // the bunny voice is a girl in most languages and Peter is a boy (Slice 121)
        [StoreTexts.TownMouseId] = new HashSet<string> { "Town Mouse", "Country Mouse" }, // the mouse voice is a boy in en/fa, a girl elsewhere; she and he (Slice 121)
    };

    private static IReadOnlyDictionary<string, AnimalKind> Named(AnimalKind kind, params string[] names) => names.ToDictionary(n => n, _ => kind);

    /// <summary>
    /// The decided animal of each of <paramref name="characters"/> of the story <paramref name="storyId"/>, by character id: the ones named here, else the ones the name says
    /// (<see cref="AnimalSounds.Resolve"/>, never a guess from the text: Grandpa is not the Cow). Slice 152 stores the result in the book so nothing is guessed at read time.
    /// </summary>
    public static IReadOnlyDictionary<string, AnimalKind> For(string storyId, IReadOnlyList<Character> characters)
    {
        var decided = new Dictionary<string, AnimalKind>(AnimalSounds.Resolve(characters, StarterBuilder.StoryLanguage));
        if (!ByStory.TryGetValue(storyId, out IReadOnlyDictionary<string, AnimalKind>? names))
        {
            return decided;
        }

        foreach (Character character in characters.Where(c => names.ContainsKey(c.Name)))
        {
            decided[character.Id] = names[character.Name];
        }

        return decided;
    }

    /// <summary>
    /// Every member carries an explicit animal or "none" (Slice 152, Decision 157). A member with no animal decided is stored as none, but the build fails when the story text
    /// suggests it is an animal (<see cref="AnimalGuess"/>, e.g. "Pip the pig") and the writer left it out of <c>ByStory</c>: list it there, or in <c>NotAnimalsByStory</c> when it is deliberately none.
    /// </summary>
    public static IReadOnlyList<CastMember> Explicit(string storyId, string text, IReadOnlyList<Character> characters, IReadOnlyList<CastMember> members)
    {
        var undecided = members.Where(m => !m.HasAnimalSetting).ToList();
        var ids = undecided.Select(m => m.CharacterId).ToHashSet();
        var suspects = AnimalSounds.Resolve(characters.Where(c => ids.Contains(c.Id)).ToList(), StarterBuilder.StoryLanguage, text: text);
        if (suspects.Count > 0)
        {
            string names = string.Join(", ", undecided.Where(m => suspects.ContainsKey(m.CharacterId)).Select(m => m.Name));
            throw new InvalidOperationException($"{storyId}: {names} look like an animal in the text but have none decided; add them to StarterAnimals.ByStory, or to NotAnimalsByStory if they are none.");
        }

        return members.Select(m => m.HasAnimalSetting ? m : m with { NotAnimal = true }).ToList();
    }

    /// <summary>The cast members of the story <paramref name="storyId"/>, those named here as no animal marked so.</summary>
    public static IReadOnlyList<CastMember> WithNotAnimals(string storyId, IReadOnlyList<CastMember> members)
    {
        if (!NotAnimalsByStory.TryGetValue(storyId, out IReadOnlySet<string>? names))
        {
            return members;
        }

        return members.Select(m => names.Contains(m.Name) ? m with { Animal = null, NotAnimal = true } : m).ToList();
    }
}
