using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// The fixed voice styles shipped in the Supertonic-3 model, in the speaker-id
/// order sherpa-onnx packs them: the bundle's <c>voice.bin</c> is built from
/// <c>sorted(*.json)</c>, i.e. F1..F5 then M1..M5, so sids 0–4 are female and
/// 5–9 male. Voice ids are prefixed so a persisted id can't collide.
/// Display names are <i>Overlord</i> characters (5 female, 5 male); only the
/// names are cosmetic — the <see cref="IdPrefix"/>-prefixed ids and sid order
/// are stable, so a persisted choice survives the rename.
/// </summary>
internal static class SupertonicVoiceTable
{
    public const string IdPrefix = "supertonic:";

    // Index == speaker id (sid). Order matches sorted voice-style filenames.
    // Display names map to Overlord characters (female F1..F5, male M1..M5).
    private static readonly (string Key, string DisplayName)[] Entries =
    [
        ("F1", "Albedo"),
        ("F2", "Shalltear Bloodfallen"),
        ("F3", "Yuri Alpha"),
        ("F4", "Lupusregina Beta"),
        ("F5", "Narberal Gamma"),
        ("M1", "Momonga"),
        ("M2", "Demiurge"),
        ("M3", "Cocytus"),
        ("M4", "Sebas Tian"),
        ("M5", "Pandora's Actor"),
    ];

    /// <summary>The default voice: the first male style (M1, "Momonga").</summary>
    public const string DefaultKey = "M1";

    public static IReadOnlyList<VoiceInfo> Voices { get; } =
        Entries.Select(e => new VoiceInfo(IdPrefix + e.Key, e.DisplayName, "en")).ToList();

    public static VoiceInfo Default { get; } = Voices.First(v => v.Id == IdPrefix + DefaultKey);

    /// <summary>Maps a voice id to its speaker id, or the default's sid when unknown.</summary>
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
