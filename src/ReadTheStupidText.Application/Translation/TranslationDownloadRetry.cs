using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Application.Translation;

/// <summary>Where a translation model download stands (Slice 187). Done, Failed and Cancelled are final.</summary>
public enum TranslationDownloadState
{
    Downloading,
    WaitingForInternet,
    Done,
    Failed,
    Cancelled,
}

/// <summary>
/// The pure rules of "a translation model download waits for the internet instead of failing" (Slice 187): what an ended attempt
/// turns into, when a network change restarts a waiting download, and that a final state never moves again.
/// </summary>
public static class TranslationDownloadRetry
{
    /// <summary>True for Done, Failed and Cancelled.</summary>
    public static bool IsFinal(TranslationDownloadState state) =>
        state is TranslationDownloadState.Done or TranslationDownloadState.Failed or TranslationDownloadState.Cancelled;

    /// <summary>
    /// What one ended attempt makes of the download: done on success; on failure the internet decides (offline: wait, online: the
    /// error stands). <paramref name="online"/> is the connectivity at the moment the attempt ended, also for an attempt a network restart began, so
    /// a flapping Wi-Fi keeps waiting. Only a download that is running changes; a Cancel that landed meanwhile stays.
    /// </summary>
    public static TranslationDownloadState AfterAttempt(TranslationDownloadState current, bool succeeded, bool online)
    {
        if (current != TranslationDownloadState.Downloading)
        {
            return current;
        }

        if (succeeded)
        {
            return TranslationDownloadState.Done;
        }

        return online ? TranslationDownloadState.Failed : TranslationDownloadState.WaitingForInternet;
    }

    /// <summary>
    /// Whether a network change restarts the download by itself: only a waiting one, only with internet, and only on Wi-Fi or Ethernet unless the
    /// person already went ahead on mobile data.
    /// </summary>
    public static bool ShouldRestartOnNetwork(TranslationDownloadState state, bool online, IEnumerable<NetworkProfile> profiles, bool mobileDataAllowed)
    {
        if (state != TranslationDownloadState.WaitingForInternet || !online)
        {
            return false;
        }

        return mobileDataAllowed || StorytellerNetworkPolicy.AllowsAutomaticDownload(profiles);
    }

    /// <summary>Retry works on a waiting download, on any network.</summary>
    public static bool CanRetry(TranslationDownloadState state) => state == TranslationDownloadState.WaitingForInternet;

    /// <summary>Cancel ends a running or waiting download for good; a final state stays as it was.</summary>
    public static TranslationDownloadState AfterCancel(TranslationDownloadState current) =>
        IsFinal(current) ? current : TranslationDownloadState.Cancelled;
}
