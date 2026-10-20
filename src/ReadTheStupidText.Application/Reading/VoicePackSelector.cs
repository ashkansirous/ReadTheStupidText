namespace ReadTheStupidText.Application.Reading;

/// <summary>Picks the voice pack that serves a language, or null when none exists yet (system voice reads).</summary>
public static class VoicePackSelector
{
    public static PackInfo? Select(IEnumerable<PackInfo> catalog, string languageCode) =>
        catalog
            .Where(p => p.Kind == PackKind.Voice)
            .Where(p => p.Languages.Contains(languageCode, StringComparer.OrdinalIgnoreCase))
            .OrderBy(p => p.Languages.Count) // the most specific (fewest-language) pack wins
            .FirstOrDefault();
}
