using System.Text.RegularExpressions;
using ReadTheStupidText.Application.Crashes;

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
    TestInstall,
    NonStoreInstall,
}

/// <summary>The facts one head gathers once at start to decide whether it counts.</summary>
/// <param name="IsDebug">True for a debug build.</param>
/// <param name="DisplayVersion">The version the app reports; CI stamps every shipped build, so the project default means a desk build.</param>
/// <param name="IsVirtual">True on an emulator or simulator.</param>
/// <param name="IsTestLab">True on Android Firebase Test Lab (Play's pre-launch report).</param>
/// <param name="IsTestInstall">True when one of our own test harnesses switched counting off (Slice 179, <see cref="UsageEligibility.NoUsageVariable"/>).</param>
/// <param name="IsNonStoreInstall">True when the install did not come from the store: a Windows MSIX not signed by the Store, an iOS build from TestFlight or App Review (Slice 179).</param>
/// <param name="Channel">Where the install came from (Slice 227): only crash reports read it (<see cref="CrashEligibility"/>); usage counts decide on <paramref name="IsNonStoreInstall"/> alone, as before.</param>
public readonly record struct UsageFacts(bool IsDebug, string DisplayVersion, bool IsVirtual, bool IsTestLab, bool IsTestInstall = false, bool IsNonStoreInstall = false, InstallChannel Channel = InstallChannel.Store);

/// <summary>
/// Decides whether a build or device may send usage counts (Slice 167): test and development builds never do, so a store dashboard holds
/// real people only. Pure; the reason is for a debug line, never sent. A test build still builds its batches (a dry run).
/// </summary>
public static class UsageEligibility
{
    /// <summary>The environment variable our harnesses (desktop VM, emulator, phone, simulator) set to 1 so a Release run counts nothing (Slice 179).</summary>
    public const string NoUsageVariable = "READTHESTUPIDTEXT_NO_USAGE";

    /// <summary>The marker file name an Android harness drops in the app's external files folder for the same purpose (Slice 179).</summary>
    public const string NoUsageMarkerFile = "no-usage";

    private const string NoUsageOn = "1";
    private const char VersionSeparator = '.';
    private const int PreReleaseMajor = 0;

    // The worker's VERSION_PATTERN: 1 to 6 ASCII digits per part, 2 to 4 parts. [0-9] not \d (.NET \d matches Unicode digits) and \z not $ (.NET $ allows a final newline).
    private static readonly Regex WellFormedVersion = new(@"^[0-9]{1,6}(\.[0-9]{1,6}){1,3}\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly string[] UnstampedVersions = ["1.0", "1.0.0", "1.0.0.0"];

    /// <summary>True when the value of <see cref="NoUsageVariable"/> switches counting off.</summary>
    public static bool IsNoUsageValue(string? value) => value?.Trim() == NoUsageOn;

    private static readonly string[] EmulatorFingerprintPrefixes = ["generic/", "generic_"];
    private static readonly string[] EmulatorFingerprintParts = ["robo"];
    private static readonly string[] EmulatorHardware = ["ranchu", "goldfish", "emu"];
    private static readonly string[] EmulatorModelParts = ["sdk_gphone", "Emulator", "Android SDK built for"];

    /// <summary>True when the Android build strings (as <c>Build.Fingerprint</c>, <c>Hardware</c>, <c>Model</c>, <c>Product</c>) are an emulator's or a test robot's (Slice 179). Pure, so the patterns are tested; a null string never matches.</summary>
    public static bool IsEmulatorBuild(string? fingerprint, string? hardware, string? model, string? product) =>
        (fingerprint is not null && Array.Exists(EmulatorFingerprintPrefixes, p => fingerprint.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        || ContainsAny(fingerprint, EmulatorFingerprintParts)
        || (hardware is not null && Array.Exists(EmulatorHardware, h => string.Equals(h, hardware, StringComparison.OrdinalIgnoreCase)))
        || ContainsAny(model, EmulatorModelParts)
        || ContainsAny(product, EmulatorModelParts);

    private static bool ContainsAny(string? text, string[] parts) =>
        text is not null && Array.Exists(parts, part => text.Contains(part, StringComparison.OrdinalIgnoreCase));

    /// <summary>The first rule that applies, in the order debug, unstamped, emulator, test lab, test install, non-store install; <see cref="UsageSkipReason.None"/> when none does.</summary>
    public static UsageSkipReason Check(UsageFacts facts)
    {
        UsageSkipReason build = CheckBuild(facts);
        if (build != UsageSkipReason.None)
        {
            return build;
        }

        return facts.IsNonStoreInstall ? UsageSkipReason.NonStoreInstall : UsageSkipReason.None;
    }

    /// <summary>The build and device rules both usage counts and crash reports share (Slice 227): debug, unstamped, emulator, test lab, test install. The install channel is each caller's own rule.</summary>
    public static UsageSkipReason CheckBuild(UsageFacts facts)
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

        if (facts.IsTestLab)
        {
            return UsageSkipReason.TestLab;
        }

        return facts.IsTestInstall ? UsageSkipReason.TestInstall : UsageSkipReason.None;
    }

    /// <summary>Whether this build and device count.</summary>
    public static bool Counts(UsageFacts facts) => Check(facts) == UsageSkipReason.None;

    // Slice 219 and 223: the same rule as the worker (events.ts isUnstampedVersion). A version that is not well formed, the project defaults, and any major part 0
    // (including 0.0.0 and 0.0.0.0) is a dev build; "10.1" and "100.0" still count.
    private static bool IsUnstamped(string? version) =>
        IsMalformedOrPreRelease(version) || Array.IndexOf(UnstampedVersions, version) >= 0;

    // No trimming: the worker drops a padded version as malformed, so the client must too.
    private static bool IsMalformedOrPreRelease(string? version) =>
        string.IsNullOrEmpty(version)
        || !WellFormedVersion.IsMatch(version)
        || int.Parse(version.Split(VersionSeparator)[0], System.Globalization.CultureInfo.InvariantCulture) == PreReleaseMajor;
}
