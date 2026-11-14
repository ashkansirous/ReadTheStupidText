namespace DinoReader.Core;

/// <summary>
/// The narrator is one voice per language, chosen once for every book (Slice 109, Decision 114). The choice is stored by the app;
/// this is the pure part: which voice a language narrates with, given what is stored and the voices its pack has.
/// </summary>
public static class NarratorSetting
{
    /// <summary>The stored pick while the language's voices still include it; otherwise <paramref name="defaultId"/> (a pack that was
    /// replaced, or a pick that was never made). An empty pool (no pack yet) keeps the stored pick, else the default.</summary>
    public static string Resolve(string? stored, IReadOnlyList<VoiceOption> voices, string defaultId)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return defaultId;
        }

        return voices.Count == 0 || voices.Any(v => v.Id == stored) ? stored : defaultId;
    }
}
