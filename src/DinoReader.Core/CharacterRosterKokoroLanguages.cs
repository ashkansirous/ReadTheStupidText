using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

public static partial class CharacterRoster
{
    // Spanish, Portuguese and Chinese run on the Kokoro storyteller voices (Slice 118). The slots are the same eighteen ideas as TenVoiceSlots, so the names and
    // plain words (CharacterRosterWords.cs) keep their order; the voice key is F (a woman's voice) or M (a man's) and a place.

    // Spanish and Portuguese have three voices: one woman (F1) and two men (M1, M2: Santa is the deep one). Eighteen characters on three voices: no two share
    // a voice and a flavour. Dora plays the girls and women, Santa the big and grumpy, Alex the bright and quick.
    private static readonly Slot[] ThreeVoiceSlots =
    [
        new("granny", "F1", VoiceFlavour.Storyteller, CharacterKind.Narrator),
        new("grandad", "M2", VoiceFlavour.Storyteller, CharacterKind.Narrator),
        new("whisperer", "M1", VoiceFlavour.Storyteller, CharacterKind.Narrator),
        new("mummy", "F1", VoiceFlavour.Normal, CharacterKind.Speaker),
        new("mouse", "F1", VoiceFlavour.Squeaky, CharacterKind.Speaker),
        new("bunny", "F1", VoiceFlavour.Small, CharacterKind.Speaker),
        new("princess", "F1", VoiceFlavour.Whisper, CharacterKind.Speaker),
        new("bee", "F1", VoiceFlavour.Bouncy, CharacterKind.Speaker),
        new("witch", "F1", VoiceFlavour.Grumpy, CharacterKind.Speaker),
        new("hen", "F1", VoiceFlavour.Sleepy, CharacterKind.Speaker),
        new("troll", "M2", VoiceFlavour.Grumpy, CharacterKind.Speaker),
        new("giant", "M2", VoiceFlavour.Giant, CharacterKind.Speaker),
        new("bear", "M2", VoiceFlavour.Sleepy, CharacterKind.Speaker),
        new("fox", "M1", VoiceFlavour.Whisper, CharacterKind.Speaker),
        new("knight", "M1", VoiceFlavour.Normal, CharacterKind.Speaker),
        new("piglet", "M1", VoiceFlavour.Small, CharacterKind.Speaker),
        new("chick", "M1", VoiceFlavour.Bouncy, CharacterKind.Speaker),
        new("dragon", "M2", VoiceFlavour.Big, CharacterKind.Speaker),
    ];

    // Chinese has eight voices, four women (F1-F4) and four men (M1-M4). Three narrators, then the cast.
    private static readonly Slot[] ChineseSlots =
    [
        new("granny", "F1", VoiceFlavour.Storyteller, CharacterKind.Narrator),
        new("grandad", "M1", VoiceFlavour.Storyteller, CharacterKind.Narrator),
        new("whisperer", "F3", VoiceFlavour.Storyteller, CharacterKind.Narrator),
        new("mummy", "F2", VoiceFlavour.Normal, CharacterKind.Speaker),
        new("mouse", "F4", VoiceFlavour.Squeaky, CharacterKind.Speaker),
        new("bunny", "F3", VoiceFlavour.Small, CharacterKind.Speaker),
        new("princess", "F4", VoiceFlavour.Normal, CharacterKind.Speaker),
        new("bee", "F3", VoiceFlavour.Bouncy, CharacterKind.Speaker),
        new("witch", "F2", VoiceFlavour.Grumpy, CharacterKind.Speaker),
        new("hen", "F1", VoiceFlavour.Sleepy, CharacterKind.Speaker),
        new("troll", "M2", VoiceFlavour.Grumpy, CharacterKind.Speaker),
        new("giant", "M2", VoiceFlavour.Giant, CharacterKind.Speaker),
        new("bear", "M1", VoiceFlavour.Sleepy, CharacterKind.Speaker),
        new("fox", "M3", VoiceFlavour.Whisper, CharacterKind.Speaker),
        new("knight", "M3", VoiceFlavour.Normal, CharacterKind.Speaker),
        new("piglet", "M4", VoiceFlavour.Small, CharacterKind.Speaker),
        new("chick", "M4", VoiceFlavour.Bouncy, CharacterKind.Speaker),
        new("dragon", "M2", VoiceFlavour.Big, CharacterKind.Speaker),
    ];

    // The Kokoro key of a place, by the voice's language letter: Spanish voices start with e (ef_dora), Portuguese with p (pf_dora).
    private static string ThreeVoiceKey(char letter, string place) => place switch
    {
        "F1" => $"{letter}f_dora",
        "M1" => $"{letter}m_alex",
        _ => $"{letter}m_santa",
    };

    private static string ChineseVoiceKey(string place) => place switch
    {
        "F1" => "zf_xiaoxiao",
        "F2" => "zf_xiaobei",
        "F3" => "zf_xiaoni",
        "F4" => "zf_xiaoyi",
        "M1" => "zm_yunyang",
        "M2" => "zm_yunjian",
        "M3" => "zm_yunxi",
        _ => "zm_yunxia",
    };

    private static IReadOnlyList<StorytellerCharacter> KokoroRoster(AppLanguage language) => language switch
    {
        AppLanguage.Spanish => FromSlots(ThreeVoiceSlots, place => KokoroVoiceTable.IdPrefix + ThreeVoiceKey('e', place), SpanishWords()),
        AppLanguage.Portuguese => FromSlots(ThreeVoiceSlots, place => KokoroVoiceTable.IdPrefix + ThreeVoiceKey('p', place), PortugueseWords()),
        AppLanguage.Chinese => FromSlots(ChineseSlots, place => KokoroVoiceTable.IdPrefix + ChineseVoiceKey(place), ChineseWords()),
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "No Kokoro roster for this language."),
    };
}
