namespace ReadTheStupidText.Domain.Reading;

/// <summary>
/// The Kokoro-82M English voices Dino offers as storytellers (Slice 114, Decision 118), each with the speaker id
/// it has in sherpa-onnx's kokoro-multi-lang-v1_0 <c>voices.bin</c> (53 voices; ids 0-27 are English). The pack carries
/// all of them but Dino lists only these. Voice ids are prefixed so a persisted id can't collide with another engine's.
/// The first voice is the default narrator: a warm storyteller. The names are cosmetic; the ids and sids are stable.
/// </summary>
public static class KokoroVoiceTable
{
    public const string IdPrefix = "kokoro:";
    public const string EnglishCode = "en";

    /// <summary>The Kokoro voice name's second letter says the gender: <c>f</c> for a woman or girl, <c>m</c> for a man or boy.</summary>
    private const int GenderLetterAt = 1;
    private const char FemaleLetter = 'f';

    // Order is the pick order: Heart first (the narrator), then the girls, then the boys.
    private static readonly (string Key, string DisplayName, int Sid)[] Entries =
    [
        ("af_heart", "Heart", 3),
        ("af_bella", "Bella", 2),
        ("af_nicole", "Nicole", 6),
        ("af_sky", "Sky", 10),
        ("af_nova", "Nova", 7),
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

    /// <summary>Whether the storyteller voices speak <paramref name="language"/> (English only, for now).</summary>
    public static bool Serves(string language) => language == EnglishCode;

    public static IReadOnlyList<VoiceInfo> Voices { get; } = Entries
        .Select(e => new VoiceInfo(IdPrefix + e.Key, e.DisplayName, EnglishCode, VoiceEngineKind.Kokoro, e.Sid))
        .ToList();

    /// <summary>The default narrator: Heart.</summary>
    public static VoiceInfo Default => Voices[0];

    /// <summary>Whether <paramref name="voiceId"/> is a woman's or girl's voice (any id that is not a known Kokoro voice reads as a man's).</summary>
    public static bool IsFemale(string voiceId) =>
        voiceId.StartsWith(IdPrefix, StringComparison.Ordinal)
        && Entries.Any(e => IdPrefix + e.Key == voiceId && e.Key[GenderLetterAt] == FemaleLetter);

    /// <summary>The key of a voice id as the manifest and the sample use it (<c>af_heart</c>), or null for another engine's id.</summary>
    public static string? KeyOf(string voiceId) =>
        voiceId.StartsWith(IdPrefix, StringComparison.Ordinal) ? voiceId[IdPrefix.Length..] : null;

    /// <summary>
    /// The Kokoro voice that speaks a stored Supertonic pick while the storyteller pack is installed: the voice of the same gender
    /// at the same position among that gender's voices (wrapping), so the five picks of a gender stay five different voices.
    /// Null when <paramref name="voiceId"/> is not a Supertonic voice.
    /// </summary>
    public static VoiceInfo? MapFromSupertonic(string? voiceId)
    {
        if (SupertonicVoiceTable.Voices.All(v => v.Id != voiceId))
        {
            return null;
        }

        bool female = SupertonicVoiceTable.IsFemale(voiceId);
        int position = SupertonicVoiceTable.SpeakerIdFor(voiceId) - (female ? 0 : SupertonicVoiceTable.FemaleStyleCount);
        List<VoiceInfo> sameGender = Voices.Where(v => IsFemale(v.Id) == female).ToList();
        return sameGender[position % sameGender.Count];
    }
}
