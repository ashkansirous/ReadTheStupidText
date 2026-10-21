namespace ReadTheStupidText.Mobile;

/// <summary>User-facing reasons the voice picker offers only the phone voice (UI copy lives here, not in the services).</summary>
public static class VoiceFallbackText
{
    public const string PackNotInstalled =
        "The natural voice for your language isn't installed yet, so the phone voice reads.";

    public const string NoNeuralEngine =
        "Natural voices aren't available on this device yet, so the phone voice reads.";
}
