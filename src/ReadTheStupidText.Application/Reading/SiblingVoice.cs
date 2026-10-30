using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Picks the voice that stands in when a voice returns silence for real text (Slice 149). The pick is explicit
/// per voice, never the pack's list order: a stand-in must keep the speaker's gender and role in a story, so a
/// voice with no entry has no sibling (it still gets its extra takes).
/// </summary>
public static class SiblingVoice
{
    private const string Ganji = VoiceChoices.PiperIdPrefix + "fa-ganji";
    private const string GanjiAdabi = VoiceChoices.PiperIdPrefix + "fa-ganji-adabi";
    private const string Mana = VoiceChoices.PiperIdPrefix + "fa-mana";
    private const string Gyro = VoiceChoices.PiperIdPrefix + "fa-gyro";

    // Slice 210: every shipped Kokoro voice has a stand-in of its own language and gender, chosen by the roster's descriptions.
    // Spanish and Portuguese have only one woman each (Dora), so those two have none.
    private static string K(string key) => VoiceChoices.KokoroIdPrefix + key;

    // Slice 144: mana's sibling is ganji (the Farsi voice that dropped nothing in Slice 145).
    // English men: Michael (gruff, low) -> Fable (low, growly); Puck (small, quick) -> Daniel (small, soft); Liam (bold, clear) -> Michael;
    // Santa (big, deep) -> Fable; Fable (deep, cosy) -> Michael; Daniel (small, soft) -> Puck.
    // English women: Heart (warm) <-> Sarah (warm, kind); Nicole (soft, slow) <-> Aoede (slow, cosy); Sky (bubbly) <-> Kore (bubbly);
    // Bella (clear, bright) <-> Nova (soft, light); Emma <-> Alice (British); Lily (tiny) -> Nova (small).
    private static readonly Dictionary<string, string> Preferred = new()
    {
        [K("am_liam")] = K("am_michael"),
        [K("am_michael")] = K("bm_fable"),
        [K("am_puck")] = K("bm_daniel"),
        [K("am_santa")] = K("bm_fable"),
        [K("bm_fable")] = K("am_michael"),
        [K("bm_daniel")] = K("am_puck"),
        [K("af_heart")] = K("af_sarah"),
        [K("af_sarah")] = K("af_heart"),
        [K("af_nicole")] = K("af_aoede"),
        [K("af_aoede")] = K("af_nicole"),
        [K("af_sky")] = K("af_kore"),
        [K("af_kore")] = K("af_sky"),
        [K("af_bella")] = K("af_nova"),
        [K("af_nova")] = K("af_bella"),
        [K("bf_emma")] = K("bf_alice"),
        [K("bf_alice")] = K("bf_emma"),
        [K("bf_lily")] = K("af_nova"),
        [K("em_alex")] = K("em_santa"),
        [K("em_santa")] = K("em_alex"),
        [K("pm_alex")] = K("pm_santa"),
        [K("pm_santa")] = K("pm_alex"),
        [K("zf_xiaoxiao")] = K("zf_xiaobei"),
        [K("zf_xiaobei")] = K("zf_xiaoxiao"),
        [K("zf_xiaoni")] = K("zf_xiaoyi"),
        [K("zf_xiaoyi")] = K("zf_xiaoni"),
        [K("zm_yunxi")] = K("zm_yunjian"),
        [K("zm_yunjian")] = K("zm_yunxi"),
        [K("zm_yunxia")] = K("zm_yunyang"),
        [K("zm_yunyang")] = K("zm_yunxia"),
        [Mana] = Ganji,
        [Gyro] = Ganji,
        [Ganji] = GanjiAdabi,
        [GanjiAdabi] = Ganji,
    };

    // Voices measured to drop short words without an error (Slice 145): their units are spoken phrase by phrase.
    private static readonly HashSet<string> DropProne = [Mana];

    /// <summary>True when the voice is known to drop words, so its units are split into phrases and each one guarded.</summary>
    public static bool IsDropProne(VoiceInfo voice) => DropProne.Contains(voice.Id);

    // Mana clips cached before the guard and the phrase split can hold a dropped word; the tag gives them a new key
    // (Slice 149), and other voices keep theirs.
    private const string GuardTag = "pg2";

    /// <summary>The cache tag of a voice's clips: the transform's tag, plus the guard's for a drop-prone voice.</summary>
    public static string CacheTag(VoiceInfo voice, IVoicedAudioTransform? transform)
    {
        string tag = VoicedAudio.TagOf(transform);
        if (!IsDropProne(voice))
        {
            return tag;
        }

        return tag.Length == 0 ? GuardTag : tag + "+" + GuardTag;
    }

    /// <summary>The voice's chosen stand-in when the pack offers it, else null.</summary>
    public static VoiceInfo? Pick(VoiceModelPaths model, VoiceInfo voice) =>
        Preferred.TryGetValue(voice.Id, out string? siblingId)
            ? model.AvailableVoices.FirstOrDefault(v => v.Id == siblingId && v.Engine == voice.Engine && v.Language == voice.Language)
            : null;
}
