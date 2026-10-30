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
        "https://github.com/ashkansirous/ReadTheStupidText/releases/download/packs-9/packs.json";

    public const string ManifestFileName = "pack.json";

    /// <summary>The multilingual Supertonic-3 voice pack (Slice 50).</summary>
    public const string SupertonicPackId = "supertonic-3";

    /// <summary>The Piper Persian voice pack (Slice 51); carries the GPL espeak-ng data (Decision 63).</summary>
    public const string PersianPackId = "piper-fa";

    /// <summary>The MeloTTS Chinese + English voice pack (Slice 51).</summary>
    public const string ChinesePackId = "melo-zh-en";

    /// <summary>The packs whose VITS voices are Piper (espeak-based) and so get the tokens.txt check (Slice 201); other VITS packs do not.</summary>
    public static readonly IReadOnlySet<string> PiperPackIds = new HashSet<string>(StringComparer.Ordinal) { PersianPackId, NorwegianPackId };

    /// <summary>The Piper Norwegian (nb) voice pack, ten nvcc speakers (Slice 104); carries the GPL espeak-ng data (Decision 108).</summary>
    public const string NorwegianPackId = "piper-nb";

    /// <summary>The Kokoro-82M English storyteller pack, Dino only (Slice 114); carries the GPL espeak-ng data (Decision 119).</summary>
    public const string StorytellerPackId = "kokoro-en";

    /// <summary>
    /// The app languages some voice pack declares (Supertonic-3 plus the Persian, Norwegian and Chinese packs).
    /// Keep it equal to the languages packs.yml publishes (NordicLanguageTests checks).
    /// </summary>
    public static readonly IReadOnlyList<AppLanguage> NaturallyReadable = Enum.GetValues<AppLanguage>();

    public const char LanguageSeparator = ',';
}
