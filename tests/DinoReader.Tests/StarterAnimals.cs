using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>
/// The animals of the characters whose names do not say it (Slice 125, Decision 132): the builders store them in the book's cast, so the pigs Pip, Pop and Pim oink
/// and are cast as piglets. A character whose name has an animal word (Wolf, Papa Bear, Mrs. Rabbit) is found from the name and needs no entry here.
/// </summary>
internal static class StarterAnimals
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, AnimalKind>> ByStory = new Dictionary<string, IReadOnlyDictionary<string, AnimalKind>>
    {
        [StarterTexts.PigsId] = Named(AnimalKind.Pig, "Pip", "Pop", "Pim"),
        [StoreTexts.BillyGoatsId] = Named(AnimalKind.Goat, "Little Billy", "Middle Billy", "Big Billy"),
        [StoreTexts.PeterRabbitId] = Named(AnimalKind.Rabbit, "Peter"),
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
}
