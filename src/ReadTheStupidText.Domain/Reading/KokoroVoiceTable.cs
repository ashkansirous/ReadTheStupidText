namespace ReadTheStupidText.Domain.Reading;

/// <summary>
/// The Kokoro-82M voices Dino offers as storytellers (Slice 114, Decision 118; Slice 118 adds Spanish, Portuguese and Chinese), each with the
/// speaker id it has in sherpa-onnx's kokoro-multi-lang-v1_0 <c>voices.bin</c> (54 voices; ids 0-27 are English). The pack carries
/// all of them but Dino lists only these (no French: it has one Kokoro voice, so French stays on Supertonic-3). Voice ids are prefixed so a
/// persisted id can't collide with another engine's. The first English voice is the default narrator: a warm storyteller. The names are
/// cosmetic; the ids and sids are stable.
/// </summary>
public static class KokoroVoiceTable
{
    public const string IdPrefix = "kokoro:";
    public const string EnglishCode = "en";
    public const string SpanishCode = "es";
    public const string PortugueseCode = "pt";
    public const string ChineseCode = "zh";

    /// <summary>The Kokoro voice name's second letter says the gender: <c>f</c> for a woman or girl, <c>m</c> for a man or boy.</summary>
    private const int GenderLetterAt = 1;
    private const char FemaleLetter = 'f';

    // Order is the pick order: Heart first (the narrator), then the girls, then the boys.
    private static readonly (string Key, string DisplayName, int Sid)[] EnglishEntries =
    [
        ("af_heart", "Heart", 3),
        ("af_bella", "Bella", 2),
        ("af_nicole", "Nicole", 6),
        ("af_sky", "Sky", 10),
        ("af_nova", "Nova", 7),
        ("af_sarah", "Sarah", 9),
        ("af_kore", "Kore", 5),
        ("af_aoede", "Aoede", 1),
        ("bf_emma", "Emma", 21),
        ("bf_lily", "Lily", 23),
        ("bf_alice", "Alice", 20),
        ("am_puck", "Puck", 18),
        ("am_santa", "Santa", 19),
        ("bm_fable", "Fable", 25),
        ("am_liam", "Liam", 15),
        ("am_michael", "Michael", 16),
        ("bm_daniel", "Daniel", 24),
    ];

    // Spanish and Portuguese have a woman and two men each (Dora, Alex, Santa); Chinese has four women then four men.
    private static readonly (string Key, string DisplayName, int Sid)[] SpanishEntries =
    [
        ("ef_dora", "Dora", 28),
        ("em_alex", "Alex", 29),
        ("em_santa", "Santa", 53),
    ];

    private static readonly (string Key, string DisplayName, int Sid)[] PortugueseEntries =
    [
        ("pf_dora", "Dora", 42),
        ("pm_alex", "Alex", 43),
        ("pm_santa", "Santa", 44),
    ];

    private static readonly (string Key, string DisplayName, int Sid)[] ChineseEntries =
    [
        ("zf_xiaoxiao", "Xiaoxiao", 47),
        ("zf_xiaobei", "Xiaobei", 45),
        ("zf_xiaoni", "Xiaoni", 46),
        ("zf_xiaoyi", "Xiaoyi", 48),
        ("zm_yunxi", "Yunxi", 50),
        ("zm_yunjian", "Yunjian", 49),
        ("zm_yunxia", "Yunxia", 51),
        ("zm_yunyang", "Yunyang", 52),
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<VoiceInfo>> ByLanguage = new Dictionary<string, IReadOnlyList<VoiceInfo>>
    {
        [EnglishCode] = Build(EnglishCode, EnglishEntries),
        [SpanishCode] = Build(SpanishCode, SpanishEntries),
        [PortugueseCode] = Build(PortugueseCode, PortugueseEntries),
        [ChineseCode] = Build(ChineseCode, ChineseEntries),
    };

    /// <summary>Whether the storyteller voices speak <paramref name="language"/>: English, Spanish, Portuguese and Chinese.</summary>
    public static bool Serves(string language) => ByLanguage.ContainsKey(language);

    /// <summary>The languages the storyteller voices serve (English, Spanish, Portuguese, Chinese).</summary>
    public static IEnumerable<string> Languages => ByLanguage.Keys;

    /// <summary>The English voices (the default narrator first); <see cref="VoicesFor"/> gives another language's.</summary>
    public static IReadOnlyList<VoiceInfo> Voices { get; } = ByLanguage[EnglishCode];

    /// <summary>The storyteller voices of <paramref name="language"/>, empty for a language Kokoro does not serve here.</summary>
    public static IReadOnlyList<VoiceInfo> VoicesFor(string language) => ByLanguage.GetValueOrDefault(language) ?? [];

    private static List<VoiceInfo> Build(string language, (string Key, string DisplayName, int Sid)[] entries) =>
        entries.Select(e => new VoiceInfo(IdPrefix + e.Key, e.DisplayName, language, VoiceEngineKind.Kokoro, e.Sid)).ToList();

    private const string ChineseEspeakVoice = "cmn";
    private const string BrazilianEspeakVoice = "pt-br";

    /// <summary>
    /// The espeak-ng voice the engine of <paramref name="language"/> is configured with (sherpa-onnx's Kokoro <c>lang</c>). Empty for English, which the
    /// English lexicon and the default espeak voice already speak. Chinese is <c>cmn</c> (espeak has no "zh" voice, and a Latin-script word in a Chinese
    /// sentence then fails), Portuguese is <c>pt-br</c> (the voices are Brazilian), any other language is its own code.
    /// </summary>
    public static string EngineLang(string language) => language switch
    {
        EnglishCode => string.Empty,
        ChineseCode => ChineseEspeakVoice,
        PortugueseCode => BrazilianEspeakVoice,
        _ => language,
    };

    /// <summary>The default narrator: Heart.</summary>
    public static VoiceInfo Default => Voices[0];

    /// <summary>Whether <paramref name="voiceId"/> is a woman's or girl's voice (any id that is not a known Kokoro voice reads as a man's).</summary>
    public static bool IsFemale(string voiceId) =>
        voiceId.StartsWith(IdPrefix, StringComparison.Ordinal)
        && voiceId.Length > IdPrefix.Length + GenderLetterAt
        && voiceId[IdPrefix.Length + GenderLetterAt] == FemaleLetter
        && ByLanguage.Values.Any(voices => voices.Any(v => v.Id == voiceId));

    /// <summary>The key of a voice id as the manifest and the sample use it (<c>af_heart</c>), or null for another engine's id.</summary>
    public static string? KeyOf(string voiceId) =>
        voiceId.StartsWith(IdPrefix, StringComparison.Ordinal) ? voiceId[IdPrefix.Length..] : null;

    /// <summary>
    /// The Kokoro voice that speaks a stored Supertonic pick while the storyteller pack is installed: the voice of the same gender
    /// at the same position among that gender's voices of <paramref name="language"/> (wrapping), so the five picks of a gender stay as different as the
    /// language's voices allow. Null when <paramref name="voiceId"/> is not a Supertonic voice or the language has no voice of that gender.
    /// </summary>
    public static VoiceInfo? MapFromSupertonic(string? voiceId, string language = EnglishCode)
    {
        if (SupertonicVoiceTable.Voices.All(v => v.Id != voiceId))
        {
            return null;
        }

        bool female = SupertonicVoiceTable.IsFemale(voiceId);
        int position = SupertonicVoiceTable.SpeakerIdFor(voiceId) - (female ? 0 : SupertonicVoiceTable.FemaleStyleCount);
        List<VoiceInfo> sameGender = VoicesFor(language).Where(v => IsFemale(v.Id) == female).ToList();
        return sameGender.Count == 0 ? null : sameGender[position % sameGender.Count];
    }
}
