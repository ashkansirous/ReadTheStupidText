using ReadTheStupidText.Application.Usage;
using Windows.ApplicationModel;

namespace ReadTheStupidText.Infrastructure.Usage;

/// <summary>
/// Builds the desktop's usage counts (Slice 140): the shared <see cref="UsageService"/> over the local store and Read The Stupid Text's own
/// host, and the coarse build facts every count carries (the app version, "windows", the Windows major version).
/// </summary>
public static class WindowsUsage
{
    private const string PlatformName = "windows";
    private const string FallbackVersion = "0.0.0";
    private const int Windows11FirstBuild = 22000;
    private const int Windows10Major = 10;
    private const int Windows11Major = 11;

    /// <summary>The running counts service; the app registers it as the one <see cref="IUsageRecorder"/>.</summary>
    public static UsageService Create() =>
        new(new UsageFileStore(), new UsageHttpTransport(), CurrentBuild());

    private static UsageBuild CurrentBuild()
    {
        string version = InstalledVersion();
        return new UsageBuild(version, PlatformName, OsMajor(Environment.OSVersion.Version.Build), ReadDevice(), UsageEligibility.Check(Facts(version)));
    }

    // Slice 167: a debug build or one whose package version was never stamped by CI counts nothing (a dry run). No emulator or test lab on a desktop.
    internal static UsageFacts Facts(string version)
    {
#if DEBUG
        const bool isDebug = true;
#else
        const bool isDebug = false;
#endif
        return new UsageFacts(isDebug, version, false, false);
    }

    // A machine that will not describe itself sends no device object (Slice 166); the counts still go.
    private static DeviceContext? ReadDevice()
    {
        try
        {
            return WindowsDeviceProbe.Read();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The package version without its trailing revision (the Store keeps it 0): "1.4.2" for 1.4.2.0. A build with no package identity reports "0.0.0".</summary>
    internal static string InstalledVersion()
    {
        try
        {
            PackageVersion version = Package.Current.Id.Version;
            return VersionText(version.Major, version.Minor, version.Build);
        }
        catch (InvalidOperationException)
        {
            return FallbackVersion;
        }
    }

    internal static string VersionText(int major, int minor, int build) => $"{major}.{minor}.{build}";

    /// <summary>11 from the first Windows 11 build on, else 10: both report major version 10, the build number tells them apart.</summary>
    internal static int OsMajor(int build) => build >= Windows11FirstBuild ? Windows11Major : Windows10Major;
}
