using ReadTheStupidText.Application.Crashes;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Mobile.Shared.Usage;

namespace ReadTheStupidText.Mobile.Shared.Crashes;

/// <summary>
/// Whether this install may send crash reports (Slice 227): the usage gate's facts (<see cref="UsageGate.Facts"/>) decided by <see cref="CrashEligibility"/>,
/// which allows a TestFlight install and still drops debug builds, unstamped versions, emulators, Firebase Test Lab, our harnesses' off switch and every
/// install that is neither the store's nor TestFlight. Facts that cannot be read mean no reports (fail closed).
/// </summary>
public static class CrashGate
{
    /// <summary>Why reports are not sent from here, or <see cref="UsageSkipReason.None"/> when they may be.</summary>
    public static UsageSkipReason Reason => UsageGate.Facts is { } facts ? CrashEligibility.Check(facts) : UsageSkipReason.Unreadable;

    /// <summary>Where this install came from; <see cref="InstallChannel.Dev"/> when the facts could not be read.</summary>
    public static InstallChannel Channel => UsageGate.Facts?.Channel ?? InstallChannel.Dev;
}
