namespace ReadTheStupidText.Application.Reading;

/// <summary>What made a stopped pack download try again by itself (Slice 182).</summary>
public enum PackResumeReason
{
    Network,
    Foreground,
}

/// <summary>
/// The pure decision behind "a pack download that stopped waiting for the internet carries on by itself":
/// only an Offline or Failed result is resumed, only when the device has a network the mobile-data rule allows
/// (Wi-Fi or Ethernet among the links, even when Android also lists cellular, unless the person tapped "Download now"), and at most once per trigger so a pack that
/// keeps failing never loops.
/// </summary>
public static class PackResumePolicy
{
    public static bool IsResumableResult(PackDownloadResult result) =>
        result is PackDownloadResult.Offline or PackDownloadResult.Failed;

    /// <summary>The download result a coordinator entry stands for (only Offline and Failed entries exist).</summary>
    public static PackDownloadResult ResultOf(PackRunEnd end) =>
        end == PackRunEnd.Offline ? PackDownloadResult.Offline : PackDownloadResult.Failed;

    public static bool ShouldResume(PackDownloadResult last, bool cancelledByUser, bool downloadNow, bool online, IEnumerable<NetworkProfile> profiles, bool alreadyResumedForTrigger)
    {
        if (cancelledByUser || alreadyResumedForTrigger || !online || !IsResumableResult(last))
        {
            return false;
        }

        return downloadNow || StorytellerNetworkPolicy.AllowsAutomaticDownload(profiles);
    }
}
