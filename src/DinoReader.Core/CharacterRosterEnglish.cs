using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

public static partial class CharacterRoster
{
    // English runs on the Kokoro storyteller voices (Slice 114). Three narrators, then ten boys and seven girls (the voice's gender).
    private static IReadOnlyList<StorytellerCharacter> English() =>
    [
        Kokoro("granny", "af_heart", VoiceFlavour.Storyteller, Gender.Female, CharacterKind.Narrator, "Story Granny", "warm and gentle"),
        Kokoro("grandad", "bm_fable", VoiceFlavour.Storyteller, Gender.Male, CharacterKind.Narrator, "Story Grandad", "deep and cosy"),
        Kokoro("whisperer", "af_nicole", VoiceFlavour.Storyteller, Gender.Female, CharacterKind.Narrator, "Bedtime Whisperer", "soft and slow"),
        Kokoro("troll", "am_michael", VoiceFlavour.Grumpy, Gender.Male, CharacterKind.Speaker, "Grumpy Troll", "gruff and low"),
        Kokoro("giant", "am_santa", VoiceFlavour.Giant, Gender.Male, CharacterKind.Speaker, "Friendly Giant", "big, deep and slow"),
        Kokoro("bear", "bm_daniel", VoiceFlavour.Sleepy, Gender.Male, CharacterKind.Speaker, "Grandpa Bear", "deep and kind"),
        Kokoro("fox", "bm_fable", VoiceFlavour.Whisper, Gender.Male, CharacterKind.Speaker, "Sly Fox", "quick and whispery"),
        Kokoro("knight", "am_liam", VoiceFlavour.Normal, Gender.Male, CharacterKind.Speaker, "Brave Knight", "bold and clear"),
        Kokoro("piglet", "am_puck", VoiceFlavour.Small, Gender.Male, CharacterKind.Speaker, "Cheeky Piglet", "small and quick"),
        Kokoro("owl", "am_liam", VoiceFlavour.Sleepy, Gender.Male, CharacterKind.Speaker, "Sleepy Owl", "slow and soft"),
        Kokoro("mouse", "am_puck", VoiceFlavour.Squeaky, Gender.Male, CharacterKind.Speaker, "Squeaky Mouse", "tiny and high"),
        Kokoro("jester", "am_puck", VoiceFlavour.Bouncy, Gender.Male, CharacterKind.Speaker, "Jolly Jester", "bubbly and bright"),
        Kokoro("dragon", "am_santa", VoiceFlavour.Big, Gender.Male, CharacterKind.Speaker, "Rumbly Dragon", "rumbly and loud"),
        Kokoro("mummy", "bf_emma", VoiceFlavour.Normal, Gender.Female, CharacterKind.Speaker, "Kind Mummy", "warm and kind"),
        Kokoro("bunny", "af_nova", VoiceFlavour.Small, Gender.Female, CharacterKind.Speaker, "Little Bunny", "soft and hoppy"),
        Kokoro("bee", "af_sky", VoiceFlavour.Bouncy, Gender.Female, CharacterKind.Speaker, "Busy Bee", "bubbly and quick"),
        Kokoro("princess", "af_bella", VoiceFlavour.Normal, Gender.Female, CharacterKind.Speaker, "Brave Princess", "clear and bright"),
        Kokoro("witch", "bf_alice", VoiceFlavour.Grumpy, Gender.Female, CharacterKind.Speaker, "Crafty Witch", "croaky and sly"),
        Kokoro("fairy", "bf_lily", VoiceFlavour.Squeaky, Gender.Female, CharacterKind.Speaker, "Tiny Fairy", "tiny and twinkly"),
        Kokoro("hen", "bf_emma", VoiceFlavour.Sleepy, Gender.Female, CharacterKind.Speaker, "Granny Hen", "slow and cosy"),
        Kokoro("lamb", "af_nova", VoiceFlavour.Squeaky, Gender.Female, CharacterKind.Speaker, "Woolly Lamb", "soft and bleaty"),
        Kokoro("puppy", "am_liam", VoiceFlavour.Bouncy, Gender.Male, CharacterKind.Speaker, "Playful Puppy", "bouncy and bright"),
        Kokoro("kitten", "bf_lily", VoiceFlavour.Small, Gender.Female, CharacterKind.Speaker, "Curious Kitten", "tiny and purry"),
        Kokoro("duckling", "af_sky", VoiceFlavour.Squeaky, Gender.Female, CharacterKind.Speaker, "Little Duckling", "peepy and quick"),
        Kokoro("cub", "bm_daniel", VoiceFlavour.Small, Gender.Male, CharacterKind.Speaker, "Bear Cub", "small and soft"),
        Kokoro("bigbear", "am_michael", VoiceFlavour.Big, Gender.Male, CharacterKind.Speaker, "Big Brown Bear", "deep and slow"),
        Kokoro("wolf", "bm_fable", VoiceFlavour.Grumpy, Gender.Male, CharacterKind.Speaker, "Hungry Wolf", "low and growly"),
        Kokoro("ewe", "af_bella", VoiceFlavour.Sleepy, Gender.Female, CharacterKind.Speaker, "Woolly Sheep", "soft and slow"),
    ];

    private static StorytellerCharacter Kokoro(
        string id, string key, VoiceFlavour flavour, Gender gender, CharacterKind kind, string name, string hint) =>
        new(id, KokoroVoiceTable.IdPrefix + key, flavour, gender, kind, name, hint);
}
