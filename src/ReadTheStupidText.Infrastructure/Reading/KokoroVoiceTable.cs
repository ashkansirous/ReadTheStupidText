using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// The fixed set of speakers shipped in the Kokoro <c>kokoro-en-v0_19</c> model,
/// in the speaker-id order the model's <c>voices.bin</c> uses. The model is a
/// known, closed bundle, so the list is static rather than enumerated. Voice ids
/// are prefixed so a persisted neural id can't collide with a Windows voice id.
/// </summary>
internal static class KokoroVoiceTable
{
    public const string IdPrefix = "kokoro:";

    // Index == Kokoro speaker id (sid). Order matches kokoro-en-v0_19/voices.bin.
    private static readonly (string Key, string DisplayName, string Language)[] Entries =
    [
        ("af", "Default (US, Female)", "en-US"),
        ("af_bella", "Bella (US, Female)", "en-US"),
        ("af_nicole", "Nicole (US, Female)", "en-US"),
        ("af_sarah", "Sarah (US, Female)", "en-US"),
        ("af_sky", "Sky (US, Female)", "en-US"),
        ("am_adam", "Adam (US, Male)", "en-US"),
        ("am_michael", "Michael (US, Male)", "en-US"),
        ("bf_emma", "Emma (UK, Female)", "en-GB"),
        ("bf_isabella", "Isabella (UK, Female)", "en-GB"),
        ("bm_george", "George (UK, Male)", "en-GB"),
        ("bm_lewis", "Lewis (UK, Male)", "en-GB"),
    ];

    /// <summary>The default neural voice: a natural US male (Michael).</summary>
    public const string DefaultKey = "am_michael";

    public static IReadOnlyList<VoiceInfo> Voices { get; } =
        Entries.Select(e => new VoiceInfo(IdPrefix + e.Key, e.DisplayName, e.Language)).ToList();

    public static VoiceInfo Default { get; } =
        Voices.First(v => v.Id == IdPrefix + DefaultKey);

    /// <summary>Maps a voice id to its Kokoro speaker id, or the default's sid when unknown.</summary>
    public static int SpeakerIdFor(string? voiceId)
    {
        for (int sid = 0; sid < Entries.Length; sid++)
        {
            if (IdPrefix + Entries[sid].Key == voiceId)
            {
                return sid;
            }
        }

        return DefaultSpeakerId;
    }

    public static int DefaultSpeakerId { get; } = Array.FindIndex(Entries, e => e.Key == DefaultKey);
}
