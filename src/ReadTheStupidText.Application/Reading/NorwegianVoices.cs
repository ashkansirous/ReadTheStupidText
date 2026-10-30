using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The ten speakers of the <c>piper-nb</c> pack (Slice 104). The speaker codes of Piper's
/// <c>no_NO-nvcc-medium</c> start with K (woman) or M (man). Keep the ids in sync with the voice
/// list in .github/workflows/packs.yml.
/// </summary>
public static class NorwegianVoices
{
    public const string IdPrefix = VoiceChoices.PiperIdPrefix + "nb-";

    /// <summary>The pack's voice ids in pack list order (not the speaker id order: Slice 195 put knv first); the first is the default.</summary>
    public static readonly IReadOnlyList<string> Ids =
    [
        IdPrefix + "knv", IdPrefix + "knn", IdPrefix + "ksv", IdPrefix + "mmn", IdPrefix + "kon",
        IdPrefix + "mnn", IdPrefix + "msv", IdPrefix + "mon", IdPrefix + "mnv", IdPrefix + "kmn",
    ];

    public static string DefaultId => Ids[0];

    /// <summary>
    /// <paramref name="voices"/> in <see cref="Ids"/> order when they are the Norwegian pack's (the code order, not the pack file's: the default
    /// is the first Id the installed pack has, and the picker and labels agree); any other list comes back untouched.
    /// </summary>
    public static IReadOnlyList<VoiceInfo> InCodeOrder(IReadOnlyList<VoiceInfo> voices)
    {
        if (!voices.Any(v => Position(v.Id) is not null))
        {
            return voices;
        }

        return [.. voices.OrderBy(v => Position(v.Id) ?? int.MaxValue)];
    }

    private const char WomanCode = 'k';
    private const char ManCode = 'm';

    /// <summary>True for a woman's voice, false for a man's, null when <paramref name="voiceId"/> is not a Norwegian pack voice.</summary>
    public static bool? IsFemale(string voiceId)
    {
        if (!voiceId.StartsWith(IdPrefix, StringComparison.Ordinal) || voiceId.Length <= IdPrefix.Length)
        {
            return null;
        }

        return char.ToLowerInvariant(voiceId[IdPrefix.Length]) switch
        {
            WomanCode => true,
            ManCode => false,
            _ => null,
        };
    }

    /// <summary>The voice's 1-based position in the pack (what the apps show instead of the code), or null when it is not a Norwegian pack voice.</summary>
    public static int? Position(string voiceId)
    {
        for (int i = 0; i < Ids.Count; i++)
        {
            if (string.Equals(Ids[i], voiceId, StringComparison.OrdinalIgnoreCase))
            {
                return i + 1;
            }
        }

        return null;
    }
}
