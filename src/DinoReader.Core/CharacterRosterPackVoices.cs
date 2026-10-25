using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

public static partial class CharacterRoster
{
    // The Persian pack's four Piper voices (Slice 89, ids as .github/workflows/packs.yml lists them) and the Chinese Melo voice (Slice 90). The Persian genders come
    // from the voices' sources (docs/plan/slices/89-four-farsi-voices.md): gyro was trained on a male dataset, ganji and ganji_adabi descend from the male amir voice, mana is a
    // woman's voice, so Farsi characters filter by Girls and Boys. The one Chinese voice is not known to be either, so those characters are for everyone (Gender.Unknown).
    private const string FaGanjiAdabi = VoiceChoices.PiperIdPrefix + "fa-ganji-adabi";
    private const string FaGanji = VoiceChoices.PiperIdPrefix + "fa-ganji";
    private const string FaMana = VoiceChoices.PiperIdPrefix + "fa-mana";
    private const string FaGyro = VoiceChoices.PiperIdPrefix + "fa-gyro";
    private const string MeloChinese = VoiceChoices.MeloIdPrefix + "zh";

    /// <summary>The voice ids of the Persian pack, which the roster test checks against the pack's manifest in packs.yml.</summary>
    internal static IReadOnlyList<string> PersianVoiceIds { get; } = [FaGanjiAdabi, FaGanji, FaMana, FaGyro];

    /// <summary>The voice id of the Chinese pack.</summary>
    internal static string ChineseVoiceId => MeloChinese;

    // Four voices and nine flavours: three narrators (one voice each), then sixteen characters, three to five to a voice; Mana (the one woman's voice) plays the female characters: the mum, chick, dragon, fairy, bee, witch and princess. Names are written in Persian.
    private static IReadOnlyList<StorytellerCharacter> Persian() =>
    [
        Man("storyteller", FaGanjiAdabi, VoiceFlavour.Storyteller, CharacterKind.Narrator, "قصه‌گوی مهربان", "گرم و آروم"),
        Woman("night-storyteller", FaMana, VoiceFlavour.Storyteller, CharacterKind.Narrator, "قصه‌گوی شب", "نرم و کند"),
        Man("deep-storyteller", FaGanji, VoiceFlavour.Storyteller, CharacterKind.Narrator, "قصه‌گوی خوش‌صدا", "عمیق و دلنشین"),
        Woman("mummy", FaMana, VoiceFlavour.Normal, CharacterKind.Speaker, "مامان مهربون", "گرم و مهربون"),
        Man("bunny", FaGanji, VoiceFlavour.Small, CharacterKind.Speaker, "خرگوش شیطون", "کوچولو و تند"),
        Man("mouse", FaGanji, VoiceFlavour.Squeaky, CharacterKind.Speaker, "موش کوچولو", "ریز و نازک"),
        Man("owl", FaGanji, VoiceFlavour.Sleepy, CharacterKind.Speaker, "جغد خوابالو", "آروم و نرم"),
        Man("gardener", FaGyro, VoiceFlavour.Grumpy, CharacterKind.Speaker, "باغبان اخمو", "خشن و کلفت"),
        Man("giant", FaGyro, VoiceFlavour.Giant, CharacterKind.Speaker, "غول مهربون", "بزرگ و آروم"),
        Man("knight", FaGyro, VoiceFlavour.Normal, CharacterKind.Speaker, "شوالیه شجاع", "محکم و رسا"),
        Man("fox", FaGyro, VoiceFlavour.Whisper, CharacterKind.Speaker, "روباه حیله‌گر", "تند و پچ‌پچی"),
        Woman("chick", FaMana, VoiceFlavour.Bouncy, CharacterKind.Speaker, "جوجه شاد", "شاد و جیک‌جیکی"),
        Woman("dragon", FaMana, VoiceFlavour.Big, CharacterKind.Speaker, "اژدهای غرنده", "بم و بلند"),
        Man("bear", FaGyro, VoiceFlavour.Sleepy, CharacterKind.Speaker, "پدربزرگ خرسه", "بم و مهربون"),
        Woman("fairy", FaMana, VoiceFlavour.Squeaky, CharacterKind.Speaker, "پری کوچولو", "ریز و درخشان"),
        Man("piglet", FaGanjiAdabi, VoiceFlavour.Small, CharacterKind.Speaker, "بچه‌خوک شیطون", "کوچولو و تند"),
        Woman("bee", FaMana, VoiceFlavour.Small, CharacterKind.Speaker, "زنبور وزوزی", "شاد و سریع"),
        Woman("witch", FaMana, VoiceFlavour.Grumpy, CharacterKind.Speaker, "جادوگر حیله‌گر", "خش‌دار و زیرک"),
        Woman("princess", FaMana, VoiceFlavour.Whisper, CharacterKind.Speaker, "شاهزاده خانم شجاع", "روشن و شاد"),
    ];

    // One Melo voice, so only the flavours tell the characters apart: one narrator and nine characters, the most one voice can give.
    private static IReadOnlyList<StorytellerCharacter> Chinese() =>
    [
        Unsure("granny", MeloChinese, VoiceFlavour.Storyteller, CharacterKind.Narrator, "故事奶奶", "温暖又温柔"),
        Unsure("mummy", MeloChinese, VoiceFlavour.Normal, CharacterKind.Speaker, "温柔妈妈", "温暖又亲切"),
        Unsure("bunny", MeloChinese, VoiceFlavour.Small, CharacterKind.Speaker, "小兔子", "软软的，蹦蹦跳"),
        Unsure("mouse", MeloChinese, VoiceFlavour.Squeaky, CharacterKind.Speaker, "小老鼠", "又小又尖"),
        Unsure("chick", MeloChinese, VoiceFlavour.Bouncy, CharacterKind.Speaker, "快乐小鸡", "欢快又清脆"),
        Unsure("dragon", MeloChinese, VoiceFlavour.Big, CharacterKind.Speaker, "大恐龙", "低沉又响亮"),
        Unsure("giant", MeloChinese, VoiceFlavour.Giant, CharacterKind.Speaker, "友好的大巨人", "又大又慢又低"),
        Unsure("dwarf", MeloChinese, VoiceFlavour.Grumpy, CharacterKind.Speaker, "坏脾气小矮人", "粗声粗气"),
        Unsure("bear", MeloChinese, VoiceFlavour.Sleepy, CharacterKind.Speaker, "熊爷爷", "低低的，慢慢的"),
        Unsure("fox", MeloChinese, VoiceFlavour.Whisper, CharacterKind.Speaker, "狡猾的小狐狸", "又快又悄悄"),
    ];

    private static StorytellerCharacter Man(string id, string voiceId, VoiceFlavour flavour, CharacterKind kind, string name, string hint) =>
        new(id, voiceId, flavour, Gender.Male, kind, name, hint);

    private static StorytellerCharacter Woman(string id, string voiceId, VoiceFlavour flavour, CharacterKind kind, string name, string hint) =>
        new(id, voiceId, flavour, Gender.Female, kind, name, hint);

    private static StorytellerCharacter Unsure(string id, string voiceId, VoiceFlavour flavour, CharacterKind kind, string name, string hint) =>
        new(id, voiceId, flavour, Gender.Unknown, kind, name, hint);
}
