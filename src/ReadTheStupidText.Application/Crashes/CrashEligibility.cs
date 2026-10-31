using ReadTheStupidText.Application.Usage;

namespace ReadTheStupidText.Application.Crashes;

/// <summary>
/// Decides whether a build may send crash reports (Slice 227). It is the usage gate with one difference: a TestFlight install sends (the user's
/// exception to the production-only rule, for crash reports only). Debug builds, unstamped versions, emulators and simulators, Firebase Test Lab,
/// our harnesses' off switch and every non-store, non-TestFlight install (<see cref="InstallChannel.Dev"/>) still send nothing. Pure.
/// </summary>
public static class CrashEligibility
{
    /// <summary>The first rule that applies, or <see cref="UsageSkipReason.None"/> when reports may go out.</summary>
    public static UsageSkipReason Check(UsageFacts facts)
    {
        UsageSkipReason build = UsageEligibility.CheckBuild(facts);
        if (build != UsageSkipReason.None)
        {
            return build;
        }

        return facts.Channel == InstallChannel.Dev ? UsageSkipReason.NonStoreInstall : UsageSkipReason.None;
    }

    /// <summary>Whether crash reports may be sent from this build and install.</summary>
    public static bool Allows(UsageFacts facts) => Check(facts) == UsageSkipReason.None;
}
