using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Resolves which voice of a model speaks (Slice 51). The user's choice is remembered per
/// engine family, so picking a Piper or Melo voice for another language never overwrites the
/// Supertonic choice: switching back restores it. Pure, so it is testable without a device.
/// </summary>
public static class VoiceChoices
{
    public const string PiperIdPrefix = "piper:";
    public const string MeloIdPrefix = "melo:";
    public const string KokoroIdPrefix = KokoroVoiceTable.IdPrefix;

    /// <summary>The Persian pack's literary voice: no longer Dino's default or in its cast, but still a voice a grown-up can have picked (Slice 144).</summary>
    public const string PersianLiteraryVoiceId = PiperIdPrefix + "fa-ganji-adabi";

    public const string PersianLanguageCode = "fa";

    /// <summary>The Persian pack's default voice (Slice 144, Decision 150): the female storyteller, chosen in code so the pack's listing order stays as released.</summary>
    public const string PersianDefaultVoiceId = PiperIdPrefix + "fa-mana";

    /// <summary>The engine family a voice id belongs to, or null for an id of no known family
    /// (the "Phone voice" id, or garbage).</summary>
    public static VoiceEngineKind? KindOf(string? voiceId)
    {
        if (voiceId is null)
        {
            return null;
        }

        if (voiceId.StartsWith(SupertonicVoiceTable.IdPrefix, StringComparison.Ordinal))
        {
            return VoiceEngineKind.Supertonic;
        }

        if (voiceId.StartsWith(PiperIdPrefix, StringComparison.Ordinal))
        {
            return VoiceEngineKind.Vits;
        }

        if (voiceId.StartsWith(MeloIdPrefix, StringComparison.Ordinal))
        {
            return VoiceEngineKind.Melo;
        }

        return voiceId.StartsWith(KokoroIdPrefix, StringComparison.Ordinal) ? VoiceEngineKind.Kokoro : null;
    }

    /// <summary>
    /// The first candidate id the model offers, in order (typically the explicit voice, the
    /// latest pick, then the one remembered for this engine family); the model's default voice
    /// when none of them belongs to it.
    /// </summary>
    public static VoiceInfo Resolve(VoiceModelPaths model, params string?[] candidates)
    {
        foreach (string? candidate in candidates)
        {
            VoiceInfo? match = model.Find(candidate);
            if (match is not null)
            {
                return match;
            }
        }

        return model.ResolveVoice(null);
    }
}
