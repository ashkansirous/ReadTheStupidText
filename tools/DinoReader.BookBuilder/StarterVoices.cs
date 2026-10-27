using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.BookBuilder;

/// <summary>
/// The voice choices that books carry (Slice 156, Decision 161; held in each book's <c>cast</c> in <c>book.json</c> since Slice 157): for each character, a suggested roster id (<see cref="CharacterRoster"/>, the same in every language) and its traits, written
/// into the book's cast by <see cref="StarterBuilder.Build"/> instead of leaving the choice to the app's picker. An animal's kind is not here: it is the book's animal
/// (<see cref="StarterAnimals"/>, Slice 152), which is why a person-trait is null for an animal. A book carries a suggestion and traits only once a writer has really chosen them for its
/// characters: X Marks the Treasure! was chosen by hand first, then Good Enough; the <c>dino-story-writer</c> pass adds the others to <see cref="AuthoredBooks"/> one by one (Slice 156, "Every book"),
/// and <c>AuthoredVoiceTests</c> then requires every character of an authored book to have traits and a known suggestion. Every other book's characters are cast by the app as before.
/// </summary>
internal static class StarterVoices
{
    private static IEnumerable<BookInfo> Authored => Books.All.Select(b => b.Info).Where(i => i.Cast is { Length: > 0 });

    /// <summary>The books whose characters a writer has chosen voices for: each character has traits and a known suggestion no one else in the book shares.</summary>
    public static IReadOnlyCollection<string> AuthoredBooks => Authored.Select(i => i.Id).ToList();

    /// <summary>The voice choices of the story <paramref name="storyId"/>'s characters, by character id (matched by name); nothing for a story with no entry.</summary>
    public static IReadOnlyDictionary<string, AuthoredVoice> For(string storyId, IReadOnlyList<Character> characters)
    {
        if (Authored.FirstOrDefault(i => i.Id == storyId)?.Cast is not { } authored)
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
        if (Authored.FirstOrDefault(i => i.Id == storyId)?.Cast is not { } authored)
        {
            return members;
        }

        return members.Select(m => authored.FirstOrDefault(a => a.Name == m.Name) is { } a ? m with { Gender = a.Gender } : m).ToList();
    }
}
