using System.Globalization;

namespace ReadTheStupidText.Mobile;

/// <summary>Composes the user-facing voice-pack status line from the installer state (UI copy lives here, not in the services).</summary>
public static class PackStatusText
{
    /// <summary>The status line, or null when there is nothing to show.</summary>
    public static string? Compose(PackInstallState state, double progress) => state switch
    {
        PackInstallState.Resolving => "Checking for natural voices…",
        PackInstallState.Downloading => string.Create(CultureInfo.CurrentCulture, $"Downloading natural voices… {progress:P0}"),
        PackInstallState.Offline => "No connection. Reading with the phone voice. Tap to retry the voice download.",
        PackInstallState.HashMismatch => "The voice download was corrupted. Reading with the phone voice. Tap to retry.",
        PackInstallState.InvalidPack => "The natural voices pack is unusable. Reading with the phone voice.",
        PackInstallState.Failed => "Couldn't download natural voices. Reading with the phone voice. Tap to retry.",
        PackInstallState.Unavailable => "A natural voice for your language isn't available yet. Reading with the phone voice.",
        _ => null,
    };
}
