using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Infrastructure.Reading;

namespace ReadTheStupidText_App;

/// <summary>
/// Composes the user-facing copy for the voice pack download (Slice 56). The Application layer only
/// reports <see cref="PackSetupState"/>; the words live here in the UI layer.
/// </summary>
internal static class PackSetupText
{
    private const int PercentScale = 100;
    private const string Windows = PackVoiceCatalog.WindowsVoiceName;

    public static readonly string Intro =
        $"The natural voice for your language downloads once (about 145 MB, Wi-Fi advised). The {Windows} reads until it arrives. Your text never leaves this device.";

    public static string For(PackSetupState state, double progress) => state switch
    {
        PackSetupState.Resolving => "Looking for the voice for your language...",
        PackSetupState.Downloading => $"Downloading the natural voice... {(int)(progress * PercentScale)}%",
        PackSetupState.Ready => "Natural voice ready.",
        PackSetupState.Unavailable => $"No natural voice for this language yet. The {Windows} reads.",
        PackSetupState.Offline => $"You are offline. The {Windows} reads, and the download starts when you are back online.",
        PackSetupState.HashMismatch => "The download was damaged. Retry to fetch it again.",
        PackSetupState.InvalidPack => $"This voice pack cannot be used. The {Windows} reads.",
        PackSetupState.Failed => "The download failed. Retry to try again.",
        _ => string.Empty,
    };

    /// <summary>The control panel's line while the natural voice is not ready.</summary>
    public static string ForPanel(PackSetupState state, double progress) =>
        state == PackSetupState.Idle ? Windows : For(state, progress);
}
