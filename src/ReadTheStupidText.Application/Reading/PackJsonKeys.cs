namespace ReadTheStupidText.Application.Reading;

/// <summary>JSON property names of <c>packs.json</c> and a pack's <c>pack.json</c>.</summary>
public static class PackJsonKeys
{
    public const string Id = "id";
    public const string Language = "language";
    public const string Kind = "kind";
    public const string Version = "version";
    public const string SizeBytes = "sizeBytes";
    public const string Sha256 = "sha256";
    public const string License = "license";
    public const string Url = "url";
    public const string Engine = "engine";
    public const string Files = "files";

    /// <summary>Pack manifest only: the <c>VoiceEngineKind</c> (absent means Supertonic, the Slice 50 packs).</summary>
    public const string Model = "model";

    /// <summary>Pack manifest only: the voices the pack offers (absent for Supertonic).</summary>
    public const string Voices = "voices";

    public const string VoiceName = "name";
    public const string SpeakerId = "sid";

    /// <summary>Voice entry only: the pack-relative folder with the voice's own model (Slice 89).</summary>
    public const string VoiceModel = "model";
}
