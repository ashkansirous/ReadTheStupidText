using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

public static partial class CharacterRoster
{
    // The Persian pack's four Piper voices (Slice 89, ids as .github/workflows/packs.yml lists them) and the Chinese Melo voice (Slice 90, now only the fallback
    // when the storyteller pack is not installed: the Chinese roster is Kokoro's, Slice 118). The Persian genders come
    // from the voices' sources (docs/plan/slices/89-four-farsi-voices.md): gyro was trained on a male dataset, ganji and ganji_adabi descend from the male amir voice, mana is a
    // woman's voice, so Farsi characters filter by Girls and Boys.
    private const string FaGanjiAdabi = VoiceChoices.PiperIdPrefix + "fa-ganji-adabi";
    private const string FaGanji = VoiceChoices.PiperIdPrefix + "fa-ganji";
    private const string FaMana = VoiceChoices.PiperIdPrefix + "fa-mana";
    private const string FaGyro = VoiceChoices.PiperIdPrefix + "fa-gyro";
    private const string MeloChinese = VoiceChoices.MeloIdPrefix + "zh";

    /// <summary>The voice ids of the Persian pack, which the roster test checks against the pack's manifest in packs.yml.</summary>
    internal static IReadOnlyList<string> PersianVoiceIds { get; } = [FaGanjiAdabi, FaGanji, FaMana, FaGyro];

    /// <summary>The voice id of the Chinese Melo pack (the fallback voice while the storyteller pack is not installed).</summary>
    internal static string ChineseVoiceId => MeloChinese;

    // Four voices and nine flavours: three narrators (one voice each), then sixteen characters, (Slice 125) eight animals and (Slice 135) six more young animals, many to a voice (Farsi has only four voices, so the later young animals sound Normal, Whisper or Sleepy rather than small: a voice-count limit, not a missing flavour); Mana (the one woman's voice) plays the female characters: the mum, chick, dragon, fairy, bee, witch and princess. Names are written in Persian.
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
        Man("lamb", FaGanjiAdabi, VoiceFlavour.Squeaky, CharacterKind.Speaker, "بره کوچولو", "نرم و بع‌بعی"),
        Man("puppy", FaGyro, VoiceFlavour.Bouncy, CharacterKind.Speaker, "توله‌سگ بازیگوش", "شاد و بازیگوش"),
        Man("kitten", FaGanjiAdabi, VoiceFlavour.Bouncy, CharacterKind.Speaker, "بچه‌گربه کنجکاو", "ریز و خرخری"),
        Man("duckling", FaGanji, VoiceFlavour.Bouncy, CharacterKind.Speaker, "جوجه‌اردک کوچولو", "ریز و تند"),
        Man("cub", FaGyro, VoiceFlavour.Small, CharacterKind.Speaker, "بچه‌خرس", "کوچولو و نرم"),
        Man("bigbear", FaGyro, VoiceFlavour.Big, CharacterKind.Speaker, "خرس قهوه‌ای بزرگ", "بم و آروم"),
        Man("wolf", FaGanji, VoiceFlavour.Grumpy, CharacterKind.Speaker, "گرگ گرسنه", "بم و غرغرو"),
        Woman("ewe", FaMana, VoiceFlavour.Sleepy, CharacterKind.Speaker, "میش پشمالو", "نرم و آروم"),
        // Slice 135: more than one of each young farm animal. Four voices leave few young places free, so the later ones take a calmer flavour.
        Man("piglet2", FaGyro, VoiceFlavour.Squeaky, CharacterKind.Speaker, "خوکچه ریزه", "ریز و نازک"),
        Man("piglet3", FaGanjiAdabi, VoiceFlavour.Normal, CharacterKind.Speaker, "خوکچه آرام", "ساده و مهربون"),
        Man("lamb2", FaGanji, VoiceFlavour.Normal, CharacterKind.Speaker, "بره پشمالو", "نرم و ساده"),
        Man("duckling2", FaGanjiAdabi, VoiceFlavour.Whisper, CharacterKind.Speaker, "اردکچه پچ‌پچی", "آروم و پچ‌پچی"),
        Man("puppy2", FaGanji, VoiceFlavour.Whisper, CharacterKind.Speaker, "توله‌سگ آروم", "مهربون و دوستانه"),
        Man("kitten2", FaGanjiAdabi, VoiceFlavour.Sleepy, CharacterKind.Speaker, "بچه‌گربه خوابالو", "نرم و خواب‌آلود"),
    ];

    private static StorytellerCharacter Man(string id, string voiceId, VoiceFlavour flavour, CharacterKind kind, string name, string hint) =>
        new(id, voiceId, flavour, Gender.Male, kind, name, hint);

    private static StorytellerCharacter Woman(string id, string voiceId, VoiceFlavour flavour, CharacterKind kind, string name, string hint) =>
        new(id, voiceId, flavour, Gender.Female, kind, name, hint);

}
