using System.Globalization;
using ReadTheStupidText.Application.Net;
using ReadTheStupidText.Mobile.Resources.Strings;
using ReadTheStupidText.Mobile.Shared;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The localized voice-pack status line (Slice 53). Mirrors <c>PackStatusText</c> in
/// Mobile.Shared, whose literals are English-only and shared with another app.
/// </summary>
public static class LocalizedPackStatus
{
    /// <summary>The label of the button that starts the voice download again: a deliberate stop reads Download, a failure Retry (Slice 204).</summary>
    public static string RetryLabel(PackInstallState state) =>
        state == PackInstallState.Stopped ? AppResources.Language_Download : AppResources.Language_Retry;

    /// <summary>The status line, or null when there is nothing to show.</summary>
    public static string? Compose(PackInstallState state, double progress, HttpErrorKind? failure = null) => state switch
    {
        PackInstallState.Failed when failure is { } kind => FailureText(kind),
        PackInstallState.Resolving => AppResources.PackStatus_Resolving,
        PackInstallState.Downloading => AppResources.Format(AppResources.PackStatus_Downloading, progress.ToString("P0", CultureInfo.CurrentUICulture)),
        PackInstallState.Offline => PackStatusText.CellularOnly() ? AppResources.PackStatus_WaitingWifi : AppResources.PackStatus_Waiting,
        PackInstallState.HashMismatch => AppResources.PackStatus_HashMismatch,
        PackInstallState.InvalidPack => AppResources.PackStatus_InvalidPack,
        PackInstallState.Failed => AppResources.PackStatus_Failed,
        PackInstallState.Stopped => AppResources.PackStatus_Stopped,
        PackInstallState.Unavailable => AppResources.PackStatus_Unavailable,
        _ => null,
    };

    /// <summary>
    /// Slice 357 / 364 (Missing States mock RT1): the line for a failed pack download by what the server answered, in the app language. Offline never reaches here (it is its own state); a 404 says the
    /// voice is gone, every other answer (5xx, any other 4xx, a 400) the general server text. A pack download has no message from the server, nothing to buy, and 429 never happens.
    /// </summary>
    public static string FailureText(HttpErrorKind kind) => kind == HttpErrorKind.NotFound ? AppResources.Error_VoiceGone : AppResources.Error_Server;
}
