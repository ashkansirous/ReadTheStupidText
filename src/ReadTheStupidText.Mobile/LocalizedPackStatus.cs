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

    private const double BytesPerMegabyte = 1_000_000;

    /// <summary>Slice 373 (Missing States mock RT1): a language row's second line, "Voice pack · 38 MB", from the catalog's size; null while the size is not known.</summary>
    public static string? SizeLine(long? sizeBytes) => sizeBytes is { } bytes
        ? AppResources.Format(AppResources.Pack_RowSize, Math.Max(1, (long)Math.Round(bytes / BytesPerMegabyte)).ToString(CultureInfo.CurrentUICulture))
        : null;

    /// <summary>
    /// Slice 373 (RT1): what the chosen language's row shows for the installer's state. <see cref="RowView.Detail"/> is the second line (the size, or the status text where the
    /// row has nothing else to say), <see cref="RowView.EndText"/> the percent while the pack downloads or the retry label when it can be started again, <see cref="RowView.ErrorText"/>
    /// the red strip under the row. The shared status line under the list is gone: every state is read on its row.
    /// </summary>
    public static RowView ForRow(PackInstallState state, double progress, HttpErrorKind? failure, bool canRetry, string? sizeLine)
    {
        string? status = Compose(state, progress, failure);
        return state switch
        {
            PackInstallState.Resolving => new RowView(status, null, null, false), // looking the pack up: its own text, no percent yet
            PackInstallState.Downloading => new RowView(sizeLine, progress.ToString("P0", CultureInfo.CurrentUICulture), null, false),
            PackInstallState.Failed or PackInstallState.HashMismatch or PackInstallState.InvalidPack => new RowView(sizeLine, canRetry ? AppResources.Wait_Retry : null, status, canRetry),
            PackInstallState.Offline or PackInstallState.Stopped or PackInstallState.Unavailable => new RowView(status, canRetry ? RetryLabel(state) : null, null, canRetry),
            _ => new RowView(sizeLine, null, null, false),
        };
    }

    /// <summary>The pieces of one language row; a null end text means nothing sits at the row's end.</summary>
    public sealed record RowView(string? Detail, string? EndText, string? ErrorText, bool EndIsRetry);
}
