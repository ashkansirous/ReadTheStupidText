namespace ReadTheStupidText.Application.Reading;

/// <summary>What a ticked story language does when its voice download stopped for good or for now.</summary>
public enum PackFailureAction
{
    /// <summary>Stay ticked; the download carries on by itself (offline, or the one automatic retry is still to come).</summary>
    KeepTickedAndWait,

    /// <summary>Untick the language and show the failure with its Retry.</summary>
    UntickAndShowRetry,
}

/// <summary>
/// The pure rule for a failed story-language voice download: no network keeps the language ticked and waiting (Slice 182, frame 95);
/// any other failure unticks it once the automatic retry is spent, or at once when no retry exists (a bad file, an exception).
/// </summary>
public static class PackFailureDecision
{
    public static PackFailureAction Decide(PackDownloadResult last, bool needsUserRetry)
    {
        if (last == PackDownloadResult.Offline)
        {
            return PackFailureAction.KeepTickedAndWait;
        }

        bool retryPending = last == PackDownloadResult.Failed && !needsUserRetry;
        return retryPending ? PackFailureAction.KeepTickedAndWait : PackFailureAction.UntickAndShowRetry;
    }

    /// <summary>Whether an exception from a download means the connection was lost (so the language waits, as for <see cref="PackDownloadResult.Offline"/>) and not a real failure.</summary>
    public static bool IsNetworkLoss(Exception ex) =>
        (ex is HttpRequestException http && http.StatusCode is null) // a status code means the server answered (4xx/5xx): a real failure, not a lost connection
        || ex is System.Net.Sockets.SocketException or System.Net.WebException
        || (ex is System.IO.IOException && ex.InnerException is not null && IsNetworkLoss(ex.InnerException));
}
