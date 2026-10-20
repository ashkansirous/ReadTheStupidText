namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Where packs live and what they are called (Decision 62). The catalog is the
/// <c>packs.json</c> asset of a pinned <c>packs-&lt;n&gt;</c> GitHub release, never
/// the app's <c>v&lt;x.y.z&gt;</c> releases; bump the pinned release here to ship
/// a new catalog (see docs/voice-packs.md).
/// </summary>
public static class PackConstants
{
    public const string CatalogUrl =
        "https://github.com/ashkansirous/ReadTheStupidText/releases/download/packs-1/packs.json";

    public const string ManifestFileName = "pack.json";

    /// <summary>The multilingual Supertonic-3 voice pack, the only one Slice 50 ships.</summary>
    public const string SupertonicPackId = "supertonic-3";

    public const char LanguageSeparator = ',';
}
