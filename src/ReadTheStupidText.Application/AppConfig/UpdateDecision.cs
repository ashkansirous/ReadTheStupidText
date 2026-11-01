using ReadTheStupidText.Application.Usage;

namespace ReadTheStupidText.Application.AppConfig;

/// <summary>What the remote config asks of this build (Slice 217).</summary>
public enum UpdateState
{
    /// <summary>Nothing: up to date, no usable config, or a build that never updates (debug, unstamped).</summary>
    None,

    /// <summary>A newer release exists: only a quiet, dismissible note.</summary>
    Available,

    /// <summary>The installed version is below <c>minVersion</c>: the app must update before it reads again.</summary>
    Required,
}

/// <summary>
/// Decides the update state from the remote config (Slice 217, port of Dino Reader's Slice 132). Pure. A missing or bad config, an
/// unreadable installed version, a debug build and an unstamped version never force or announce an update, so tests and desk builds are not blocked.
/// </summary>
public static class UpdateDecision
{
    /// <summary>False for a debug build or an unstamped version (<see cref="UsageEligibility"/>'s first two rules); every other build, emulators and test installs included, may be asked to update.</summary>
    public static bool BuildMayUpdate(UsageFacts facts) =>
        UsageEligibility.Check(facts with { IsVirtual = false, IsTestLab = false, IsTestInstall = false, IsNonStoreInstall = false }) == UsageSkipReason.None;

    /// <summary>The state for <paramref name="installed"/> on <paramref name="platform"/>; <see cref="UpdateState.None"/> when <paramref name="buildMayUpdate"/> is false.</summary>
    public static UpdateState Evaluate(UsageConfig config, AppPlatform platform, string installed, bool buildMayUpdate)
    {
        if (!buildMayUpdate)
        {
            return UpdateState.None;
        }

        if (config.RequiresUpdate(platform, installed))
        {
            return UpdateState.Required;
        }

        return config.HasNewerVersion(platform, installed) ? UpdateState.Available : UpdateState.None;
    }

    /// <summary>True when the person closed the note for exactly this <c>latestVersion</c>; a later release shows it again.</summary>
    public static bool IsDismissed(string? dismissedFor, UsageConfig config, AppPlatform platform) =>
        dismissedFor is not null && string.Equals(dismissedFor, config.For(platform).LatestVersion, StringComparison.Ordinal);
}
