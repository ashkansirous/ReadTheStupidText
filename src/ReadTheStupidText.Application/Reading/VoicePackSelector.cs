namespace ReadTheStupidText.Application.Reading;

/// <summary>Picks the voice pack that serves a language, or null when none exists yet (system voice reads).</summary>
public static class VoicePackSelector
{
    public static PackInfo? Select(IEnumerable<PackInfo> catalog, string languageCode) =>
        PackSelection.FromCatalog(catalog, PackKind.Voice, languageCode);
}
