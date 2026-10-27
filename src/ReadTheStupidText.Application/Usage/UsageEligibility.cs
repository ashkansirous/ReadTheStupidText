namespace ReadTheStupidText.Application.Usage;

/// <summary>Why a build or device does not count (Slice 167). <see cref="None"/> means it counts.</summary>
public enum UsageSkipReason
{
    None,
    DebugBuild,
    UnstampedBuild,
    Emulator,
    TestLab,
    Unreadable,
}

/// <summary>The facts one head gathers once at start to decide whether it counts.</summary>
/// <param name="IsDebug">True for a debug build.</param>
/// <param name="DisplayVersion">The version the app reports; CI stamps every shipped build, so the project default means a desk build.</param>
/// <param name="IsVirtual">True on an emulator or simulator.</param>
/// <param name="IsTestLab">True on Android Firebase Test Lab (Play's pre-launch report).</param>
public readonly record struct UsageFacts(bool IsDebug, string DisplayVersion, bool IsVirtual, bool IsTestLab);

/// <summary>
/// Decides whether a build or device may send usage counts (Slice 167): test and development builds never do, so a store dashboard holds
/// real people only. Pure; the reason is for a debug line, never sent. A test build still builds its batches (a dry run).
/// </summary>
public static class UsageEligibility
{
    private static readonly string[] UnstampedVersions = ["1.0", "1.0.0", "1.0.0.0", "0.0.0", "0.0.0.0"];

    /// <summary>The first rule that applies, in the order debug, unstamped, emulator, test lab; <see cref="UsageSkipReason.None"/> when none does.</summary>
    public static UsageSkipReason Check(UsageFacts facts)
    {
        if (facts.IsDebug)
        {
            return UsageSkipReason.DebugBuild;
        }

        if (IsUnstamped(facts.DisplayVersion))
        {
            return UsageSkipReason.UnstampedBuild;
        }

        if (facts.IsVirtual)
        {
            return UsageSkipReason.Emulator;
        }

        return facts.IsTestLab ? UsageSkipReason.TestLab : UsageSkipReason.None;
    }

    /// <summary>Whether this build and device count.</summary>
    public static bool Counts(UsageFacts facts) => Check(facts) == UsageSkipReason.None;

    private static bool IsUnstamped(string? version) =>
        string.IsNullOrWhiteSpace(version) || Array.IndexOf(UnstampedVersions, version.Trim()) >= 0;
}
