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
    ];

    private static StorytellerCharacter Kokoro(
        string id, string key, VoiceFlavour flavour, Gender gender, CharacterKind kind, string name, string hint) =>
        new(id, KokoroVoiceTable.IdPrefix + key, flavour, gender, kind, name, hint);
}
