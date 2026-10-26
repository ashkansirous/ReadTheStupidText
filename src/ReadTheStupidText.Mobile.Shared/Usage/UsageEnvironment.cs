namespace ReadTheStupidText.Mobile.Shared.Usage;

/// <summary>What a count says about the phone and the build: coarse, closed values only (Slice 137).</summary>
public static class UsageEnvironment
{
    private const string AndroidName = "android";
    private const string IosName = "ios";
    private const string WindowsName = "windows";

    /// <summary>The version this build reports (<c>ApplicationDisplayVersion</c>, set from the release version in CI).</summary>
    public static string InstalledVersion => AppInfo.Current.VersionString;

    /// <summary>"android", "ios" or "windows".</summary>
    public static string Platform =>
        DeviceInfo.Platform == DevicePlatform.Android ? AndroidName
        : DeviceInfo.Platform == DevicePlatform.iOS ? IosName
        : WindowsName;

    /// <summary>The operating system's major version (15 for Android 15); 0 when the system's version text is unreadable.</summary>
    public static int OsMajor => DeviceInfo.Version.Major;
}
