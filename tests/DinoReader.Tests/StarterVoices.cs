using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Tests;

/// <summary>What a book's author decided for one character's voice (Slice 156, Decision 161): the suggested roster id, and the traits that stay true when the voice changes.</summary>
internal sealed record AuthoredCharacter(string Name, string Voice, Gender Gender, CharacterAge Age, CharacterBeing? Being, VoiceQuality[] Qualities);

/// <summary>
/// The voice choices that books carry (Slice 156, Decision 161): for each character, a suggested roster id (<see cref="CharacterRoster"/>, the same in every language) and its traits, written
/// into the book's cast by <see cref="StarterBuilder.Build"/> instead of leaving the choice to the app's picker. An animal's kind is not here: it is the book's animal
/// (<see cref="StarterAnimals"/>, Slice 152), which is why a person-trait is null for an animal. A book carries a suggestion and traits only once a writer has really chosen them for its
/// characters: X Marks the Treasure! was chosen by hand and is the only <see cref="AuthoredBooks"/> entry so far; the <c>dino-story-writer</c> pass adds the others one by one (Slice 156, "Every book"),
/// and <c>AuthoredVoiceTests</c> then requires every character of an authored book to have traits and a known suggestion. Every other book's characters are cast by the app as before.
/// </summary>
internal static class StarterVoices
{
    private static readonly IReadOnlyDictionary<string, AuthoredCharacter[]> ByStory = new Dictionary<string, AuthoredCharacter[]>
    {
        // X Marks the Treasure!
        [StoreTexts.TreasureId] =
        [
            new("Seagull", "duckling2", Gender.Female, CharacterAge.Adult, null, [VoiceQuality.Squawky, VoiceQuality.Bouncy]),
            new("Kathryn", "princess", Gender.Female, CharacterAge.Adult, CharacterBeing.Pirate, [VoiceQuality.Bold, VoiceQuality.Bright]),
            new("Izzy", "fairy", Gender.Female, CharacterAge.Young, CharacterBeing.Pirate, [VoiceQuality.Bright, VoiceQuality.Bouncy]),
            new("Billy", "puppy2", Gender.Male, CharacterAge.Child, CharacterBeing.Pirate, [VoiceQuality.Bouncy, VoiceQuality.Warm]),
            new("Pancake Polly", "mummy", Gender.Female, CharacterAge.Adult, CharacterBeing.Pirate, [VoiceQuality.Warm, VoiceQuality.Bouncy]),
        ],
    };

    /// <summary>The books whose characters a writer has chosen voices for: each character has traits and a known suggestion no one else in the book shares.</summary>
    public static IReadOnlyCollection<string> AuthoredBooks => ByStory.Keys.ToList();

    /// <summary>The voice choices of the story <paramref name="storyId"/>'s characters, by character id (matched by name); nothing for a story with no entry.</summary>
    public static IReadOnlyDictionary<string, AuthoredVoice> For(string storyId, IReadOnlyList<Character> characters)
    {
        if (!ByStory.TryGetValue(storyId, out AuthoredCharacter[]? authored))
        {
            return new Dictionary<string, AuthoredVoice>();
        }

        return characters
            .Select(c => (Character: c, Author: authored.FirstOrDefault(a => a.Name == c.Name)))
            .Where(x => x.Author is not null)
            .ToDictionary(x => x.Character.Id, x => new AuthoredVoice(x.Author!.Voice, new CharacterTraits(x.Author.Age, x.Author.Being, x.Author.Qualities.Length == 0 ? null : x.Author.Qualities)));
    }

    /// <summary>The members with the author's gender in place of the guess (the traits include the gender).</summary>
    public static IReadOnlyList<CastMember> WithGenders(string storyId, IReadOnlyList<CastMember> members)
    {
        if (!ByStory.TryGetValue(storyId, out AuthoredCharacter[]? authored))
        {
            return members;
        }

        return members.Select(m => authored.FirstOrDefault(a => a.Name == m.Name) is { } a ? m with { Gender = a.Gender } : m).ToList();
    }
}
