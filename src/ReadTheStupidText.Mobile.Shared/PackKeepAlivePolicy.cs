namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// The pure decisions behind the Android pack keep-alive service (Slice 183), kept free of Android types so they are unit-tested:
/// a pack that starts while the service still runs (active or in its grace period) joins it, and the service stops only after
/// the registry has stayed empty for the whole grace period.
/// </summary>
public static class PackKeepAlivePolicy
{
    /// <summary>How long the service outlives the last download, so the next pack of a chain starts protected.</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(15);

    /// <summary>A running service is reused without any visibility check; only a cold start needs the app on screen.</summary>
    public static bool ShouldStartService(bool serviceRunning, bool appVisible) => !serviceRunning && appVisible;

    /// <summary>
    /// True when the grace timer for <paramref name="timerGeneration"/> may stop the service: nothing is registered and no
    /// later Begin or End has superseded this timer.
    /// </summary>
    public static bool ShouldStopAfterGrace(int activeCount, int timerGeneration, int currentGeneration) =>
        activeCount == 0 && timerGeneration == currentGeneration;
}
