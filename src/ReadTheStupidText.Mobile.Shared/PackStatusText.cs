using System.Globalization;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>Composes the user-facing voice-pack status line from the installer state (UI copy lives here, not in the services).</summary>
public static class PackStatusText
{
    /// <summary>
    /// Whether the phone has mobile data only, so a waiting download says "Waiting for Wi-Fi" (Slice 182). The app's connectivity
    /// watcher sets it at start; this file is also compiled without MAUI, where it stays false.
    /// </summary>
    public static Func<bool> CellularOnly { get; set; } = () => false;

    /// <summary>The status line, or null when there is nothing to show.</summary>
    public static string? Compose(PackInstallState state, double progress) => state switch
    {
        PackInstallState.Resolving => "Checking for natural voices…",
        PackInstallState.Downloading => string.Create(CultureInfo.InvariantCulture, $"Downloading natural voices… {progress:P0}"),
        PackInstallState.Offline => CellularOnly()
            ? "Waiting for Wi-Fi. It carries on by itself on Wi-Fi. Tap to download now."
            : "Waiting for the internet. It carries on by itself when you're back online. Tap to try now.",
        PackInstallState.HashMismatch => "The voice download was corrupted. Reading with the phone voice. Tap to retry.",
        PackInstallState.InvalidPack => "The natural voices pack is unusable. Reading with the phone voice.",
        PackInstallState.Failed => "Couldn't download natural voices. Reading with the phone voice. Tap to retry.",
        PackInstallState.Unavailable => "A natural voice for your language isn't available yet. Reading with the phone voice.",
        _ => null,
    };
}
