using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>The voice picker's card headings, one per engine (UI copy lives here, not in the services).</summary>
public static class VoiceEngineNames
{
    public static string Heading(VoiceEngineKind kind) => kind switch
    {
        VoiceEngineKind.Supertonic => "SUPERTONIC",
        VoiceEngineKind.Vits => "PIPER",
        VoiceEngineKind.Melo => "MELOTTS",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown voice engine."),
    };
}
