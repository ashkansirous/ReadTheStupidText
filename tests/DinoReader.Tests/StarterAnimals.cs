using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>
/// The animals of the characters whose names do not say it (Slice 125, Decision 132): the builders store them in the book's cast, so the pigs Pip, Pop and Pim oink
/// and are cast as piglets. A character whose name has an animal word (Wolf, Papa Bear, Mrs. Rabbit) is found from the name and needs no entry here.
/// A character stored as no animal (<see cref="CastMember.NotAnimal"/>) is cast by gender like a person in every language: the Ugly Duckling (really a swan) and his
/// sister Dilly, whose rosters' duckling voices are one gender per language and would otherwise give him a girl's voice or her a boy's; and the Little Red
/// Hen's Cat (she) and Duck (he), for the same reason with the kitten and duckling voices (the cost is that they make no meow or quack); and Goldilocks'
/// Mama Bear, since every roster's bear voices are men's.
/// </summary>
internal static class StarterAnimals
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, AnimalKind>> ByStory = new Dictionary<string, IReadOnlyDictionary<string, AnimalKind>>
    {
        [StarterTexts.PigsId] = Named(AnimalKind.Pig, "Pip", "Pop", "Pim"),
        [StoreTexts.BillyGoatsId] = Named(AnimalKind.Goat, "Little Billy", "Middle Billy", "Big Billy"),
        [StoreTexts.PeterRabbitId] = Named(AnimalKind.Rabbit, "Peter"),
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> NotAnimalsByStory = new Dictionary<string, IReadOnlySet<string>>
    {
        [StoreTexts.UglyDucklingId] = new HashSet<string> { "Duckling", "Dilly" },
        [StoreTexts.GoldilocksId] = new HashSet<string> { "Mama Bear" },
        [StoreTexts.RedHenId] = new HashSet<string> { "Cat", "Duck" }, // kitten and duckling voices are girls in most languages, boys in fa/es/pt; Duck is a boy (Slice 121)
    };

    private static IReadOnlyDictionary<string, AnimalKind> Named(AnimalKind kind, params string[] names) => names.ToDictionary(n => n, _ => kind);

    /// <summary>The decided animal of each of <paramref name="characters"/> of the story <paramref name="storyId"/>, by character id.</summary>
    public static IReadOnlyDictionary<string, AnimalKind> For(string storyId, IReadOnlyList<Character> characters)
    {
        var decided = new Dictionary<string, AnimalKind>();
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
