using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Tests;

/// <summary>What a book's author decided for one character's voice (Slice 156, Decision 161): the suggested roster id, and the traits that stay true when the voice changes.</summary>
internal sealed record AuthoredCharacter(string Name, string Voice, Gender Gender, CharacterAge Age, CharacterBeing? Being, VoiceQuality[] Qualities);

/// <summary>
/// The voice choices that books carry (Slice 156, Decision 161): for each character, a suggested roster id (<see cref="CharacterRoster"/>, the same in every language) and its traits, written
/// into the book's cast by <see cref="StarterBuilder.Build"/> instead of leaving the choice to the app's picker. An animal's kind is not here: it is the book's animal
/// (<see cref="StarterAnimals"/>, Slice 152), which is why a person-trait is null for an animal. A book carries a suggestion and traits only once a writer has really chosen them for its
/// characters: X Marks the Treasure! was chosen by hand first, then Good Enough; the <c>dino-story-writer</c> pass adds the others to <see cref="AuthoredBooks"/> one by one (Slice 156, "Every book"),
/// and <c>AuthoredVoiceTests</c> then requires every character of an authored book to have traits and a known suggestion. Every other book's characters are cast by the app as before.
/// </summary>
internal static class StarterVoices
{
    private static readonly IReadOnlyDictionary<string, AuthoredCharacter[]> ByStory = new Dictionary<string, AuthoredCharacter[]>
    {
        // Goldilocks and the Three Bears: a bright, bouncy girl; big booming Papa Bear; warm Mama Bear (a bear again, with a woman's voice in every language); soft little Baby Bear.
        [StoreTexts.GoldilocksId] =
        [
            new("Goldilocks", "bee", Gender.Female, CharacterAge.Child, CharacterBeing.Person, [VoiceQuality.Bright, VoiceQuality.Bouncy]),
            new("Papa Bear", "bigbear", Gender.Male, CharacterAge.Adult, null, [VoiceQuality.Booming, VoiceQuality.Warm]),
            new("Mama Bear", "mummy", Gender.Female, CharacterAge.Adult, null, [VoiceQuality.Warm, VoiceQuality.Soft]),
            new("Baby Bear", "cub", Gender.Male, CharacterAge.Child, null, [VoiceQuality.Soft, VoiceQuality.Bouncy]),
        ],

        // The Ugly Duckling: warm Mother Duck (the kind mummy); the Duckling, a gentle, curious boy (a boy's young voice in every language, so he is a duck again); Dilly, the giggly
        // littlest sister (the little bunny, small and light; the duckling voices are a boy's in some languages); kind, cheery Hen (the busy bee, bright and bouncy, not the slow Granny Hen;
        // the rosters have no adult woman's bright place but the mummy); and Swan, a big, warm, welcoming grown-up.
        [StoreTexts.UglyDucklingId] =
        [
            new("Mother Duck", "mummy", Gender.Female, CharacterAge.Adult, null, [VoiceQuality.Warm, VoiceQuality.Soft]),
            new("Duckling", "piglet", Gender.Male, CharacterAge.Child, null, [VoiceQuality.Soft, VoiceQuality.Warm]),
            new("Dilly", "bunny", Gender.Female, CharacterAge.Child, null, [VoiceQuality.Bouncy, VoiceQuality.Bright]),
            new("Hen", "bee", Gender.Female, CharacterAge.Adult, null, [VoiceQuality.Warm, VoiceQuality.Bright]),
            new("Swan", "knight", Gender.Male, CharacterAge.Adult, null, [VoiceQuality.Warm, VoiceQuality.Bold]),
        ],
        // The Lion and the Mouse: a big, booming but kind king of a Lion (the Friendly Giant: big, deep and slow); a tiny, quick, squeaky girl Mouse. The mouse voice is a boy in
        // English and Farsi, so there her suggestion clashes on gender and the traits give her a girl's young, squeaky voice; everywhere else she is the Squeaky Mouse.
        [StoreTexts.LionAndMouseId] =
        [
            new("Lion", "giant", Gender.Male, CharacterAge.Adult, null, [VoiceQuality.Booming, VoiceQuality.Warm]),
            new("Mouse", "mouse", Gender.Female, CharacterAge.Young, null, [VoiceQuality.Squeaky, VoiceQuality.Bright]),
        ],

        // X Marks the Treasure!
        [StoreTexts.TreasureId] =
        [
            new("Seagull", "duckling2", Gender.Female, CharacterAge.Adult, null, [VoiceQuality.Squawky, VoiceQuality.Bouncy]),
            new("Kathryn", "princess", Gender.Female, CharacterAge.Adult, CharacterBeing.Pirate, [VoiceQuality.Bold, VoiceQuality.Bright]),
            new("Izzy", "fairy", Gender.Female, CharacterAge.Young, CharacterBeing.Pirate, [VoiceQuality.Bright, VoiceQuality.Bouncy]),
            new("Billy", "puppy2", Gender.Male, CharacterAge.Child, CharacterBeing.Pirate, [VoiceQuality.Bouncy, VoiceQuality.Warm]),
            new("Pancake Polly", "bee", Gender.Female, CharacterAge.Adult, CharacterBeing.Pirate, [VoiceQuality.Warm, VoiceQuality.Bouncy]),
        ],

        // The Three Little Pigs: Pip is the hasty one (Cheeky Piglet, small and quick), Pop the playful one (Curly Piglet, bright and squeaky), Pim the careful builder (Snuffly
        // Piglet, tiny and snorty: the voice left once the quick and the bright ones are taken); the Wolf is silly and huffy, never scary.
        [StarterTexts.PigsId] =
        [
            new("Pip", "piglet", Gender.Male, CharacterAge.Young, null, [VoiceQuality.Bouncy, VoiceQuality.Squeaky]),
            new("Pop", "piglet3", Gender.Male, CharacterAge.Young, null, [VoiceQuality.Bright, VoiceQuality.Bouncy]),
            new("Pim", "piglet2", Gender.Male, CharacterAge.Young, null, [VoiceQuality.Soft, VoiceQuality.Warm]),
            new("Wolf", "wolf", Gender.Male, CharacterAge.Adult, null, [VoiceQuality.Gruff, VoiceQuality.Booming]),
        ],
        // The Three Billy Goats Gruff: three brothers ("his hooves"), so boys' voices rising in size; the bleat comes from their book animal (Goat), not the voice.
        // The picker gave the Troll the Rumbly Dragon and the brothers girls' lamb and sheep voices (Big Billy, "a big, deep voice", the sleepy sheep). Farsi has no troll: its trait match picks.
        [StoreTexts.BillyGoatsId] =
        [
            new("Little Billy", "piglet", Gender.Male, CharacterAge.Child, null, [VoiceQuality.Squeaky, VoiceQuality.Bright]),
            new("Middle Billy", "knight", Gender.Male, CharacterAge.Young, null, [VoiceQuality.Bold, VoiceQuality.Bright]),
            new("Big Billy", "giant", Gender.Male, CharacterAge.Adult, null, [VoiceQuality.Booming, VoiceQuality.Warm]),
            new("Troll", "troll", Gender.Male, CharacterAge.Adult, CharacterBeing.Troll, [VoiceQuality.Gruff]),
        ],
        // The Elves and the Shoemaker: an elderly, kind shoemaker and his wife in plain everyday voices, and two tiny, giggly elves (a girl and a boy) in small, bouncy ones.
        // The picker gave Nob the Grumpy Troll in most languages, Nib the whispering princess in Spanish and Portuguese and the Big dragon in Farsi. No roster has an elf, so they are fairy-kind.
        [StoreTexts.ElvesId] =
        [
            new("Hans", "knight", Gender.Male, CharacterAge.Old, CharacterBeing.Person, [VoiceQuality.Warm]),
            new("Greta", "mummy", Gender.Female, CharacterAge.Old, CharacterBeing.Person, [VoiceQuality.Warm, VoiceQuality.Soft]),
            new("Nib", "bee", Gender.Female, CharacterAge.Young, CharacterBeing.Fairy, [VoiceQuality.Bright, VoiceQuality.Bouncy]),
            new("Nob", "piglet", Gender.Male, CharacterAge.Young, CharacterBeing.Fairy, [VoiceQuality.Bouncy, VoiceQuality.Squeaky]),
        ],

        // Good Enough: Hazelnut, the squirrel best friend who says the last line (a squirrel has no animal sound, so no kind).
        [StarterBuilder.GoodEnoughId] =
        [
            new("Hazelnut", "bee", Gender.Female, CharacterAge.Young, null, [VoiceQuality.Bright, VoiceQuality.Warm]),
        ],

        // The Tortoise and the Hare: three boys and three girls, as the text's pronouns say. Owl and Frog are animals with a sound (their kind is the book's animal); the Hare, Tortoise,
        // Squirrel and Hedgehog have no sound word, so no kind either. Owl is Grandpa Bear, not the Sleepy Owl: only en and fa have an owl, and elsewhere the trait match ranks a
        // bird kind above age and gave the wise old Owl the young Chick; Grandpa Bear (old, deep and kind, sleepy) is in every roster, and Owl still hoots (his animal is the book's).
        [StarterTexts.TortoiseId] =
        [
            new("Hare", "puppy", Gender.Male, CharacterAge.Young, null, [VoiceQuality.Bouncy, VoiceQuality.Bold]),
            new("Tortoise", "hen", Gender.Female, CharacterAge.Adult, null, [VoiceQuality.Sleepy, VoiceQuality.Warm]),
            new("Owl", "bear", Gender.Male, CharacterAge.Old, null, [VoiceQuality.Sleepy, VoiceQuality.Warm]),
            new("Squirrel", "bee", Gender.Female, CharacterAge.Young, null, [VoiceQuality.Bouncy, VoiceQuality.Bright]),
            new("Hedgehog", "piglet", Gender.Male, CharacterAge.Young, null, [VoiceQuality.Bouncy, VoiceQuality.Warm]),
            new("Frog", "lamb2", Gender.Female, CharacterAge.Young, null, [VoiceQuality.Bouncy, VoiceQuality.Squeaky]),
        ],
        // The Little Red Hen: four farm animals with their own animal (and sound), Hen and Cat "she", Dog and Duck "he". A kitten is a boy in fa/es/pt and a duckling a girl in
        // most rosters, so Cat suggests the kitten and Duck the jolly jester (English only): where those don't fit, the traits give Cat a girl's voice and Duck a boy bird's.
        [StoreTexts.RedHenId] =
        [
            new("Hen", "mummy", Gender.Female, CharacterAge.Adult, null, [VoiceQuality.Bright, VoiceQuality.Bouncy, VoiceQuality.Warm]),
            new("Dog", "puppy", Gender.Male, CharacterAge.Young, null, [VoiceQuality.Bouncy, VoiceQuality.Warm]),
            new("Cat", "kitten", Gender.Female, CharacterAge.Young, null, [VoiceQuality.Soft]),
            new("Duck", "jester", Gender.Male, CharacterAge.Young, null, [VoiceQuality.Squawky, VoiceQuality.Squeaky]),
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
