using System.Globalization;
using ReadTheStupidText.Mobile.Resources.Strings;
using ReadTheStupidText.Mobile.Shared;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The localized voice-pack status line (Slice 53). Mirrors <c>PackStatusText</c> in
/// Mobile.Shared, whose literals are English-only and shared with another app.
/// </summary>
public static class LocalizedPackStatus
{
    /// <summary>The status line, or null when there is nothing to show.</summary>
    public static string? Compose(PackInstallState state, double progress) => state switch
    {
        PackInstallState.Resolving => AppResources.PackStatus_Resolving,
        PackInstallState.Downloading => AppResources.Format(AppResources.PackStatus_Downloading, progress.ToString("P0", CultureInfo.CurrentUICulture)),
        PackInstallState.Offline => PackStatusText.CellularOnly() ? AppResources.PackStatus_WaitingWifi : AppResources.PackStatus_Waiting,
        PackInstallState.HashMismatch => AppResources.PackStatus_HashMismatch,
        PackInstallState.InvalidPack => AppResources.PackStatus_InvalidPack,
        PackInstallState.Failed => AppResources.PackStatus_Failed,
        PackInstallState.Unavailable => AppResources.PackStatus_Unavailable,
        _ => null,
    };
}
