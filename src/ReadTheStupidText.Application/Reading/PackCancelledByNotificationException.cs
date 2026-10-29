namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Thrown by the pack service when the user tapped Cancel on the download notification (Slice 183). It is an
/// <see cref="OperationCanceledException"/> the caller did not ask for: an installer that sees it reports
/// <c>PackRunEnd.Cancelled</c> and never retries. Any other cancellation (the app shutting down) is not final.
/// </summary>
public sealed class PackCancelledByNotificationException(string packId) : OperationCanceledException($"Download of {packId} cancelled from the notification.");
