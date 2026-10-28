using ReadTheStupidText.Application.Usage;

namespace ReadTheStupidText.Mobile.Shared.Usage;

/// <summary>
/// Whether this build and device count (Slice 167): the facts are read once at start and decided by <see cref="UsageEligibility"/>.
/// Debug build, an unstamped version, an emulator or simulator (MAUI <c>DeviceType.Virtual</c>, both platforms) and Android Firebase Test
/// Lab (Play's pre-launch report) do not count. The emulator rule applies on Android and iOS only, so a real user in a Windows VM counts. A Test Lab setting that cannot be read means "not a test"; any other fact that cannot be read means the build is not counted.
/// </summary>
public static class UsageGate
{
    private const string TestLabSetting = "firebase.test.lab";
    private const string TestLabOn = "true";

    private static readonly Lazy<UsageSkipReason> Cached = new(Decide);

    /// <summary>Why counts are not sent from here, or <see cref="UsageSkipReason.None"/> when they are.</summary>
    public static UsageSkipReason Reason => Cached.Value;

    private static UsageSkipReason Decide()
    {
        try
        {
            return UsageEligibility.Check(new UsageFacts(IsDebug(), UsageEnvironment.InstalledVersion, IsVirtual(), IsTestLab()));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Usage eligibility unreadable, not counted: {ex.Message}");
            return UsageSkipReason.Unreadable;
        }
    }

    private static bool IsDebug()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    private static bool IsVirtual()
    {
#if ANDROID || IOS
        return DeviceInfo.Current.DeviceType == DeviceType.Virtual;
#else
        return false;
#endif
    }

    private static bool IsTestLab()
    {
#if ANDROID
        try
        {
            var resolver = Microsoft.Maui.ApplicationModel.Platform.AppContext.ContentResolver;
            return global::Android.Provider.Settings.System.GetString(resolver, TestLabSetting) == TestLabOn;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Test Lab setting unreadable, treated as not a test: {ex.Message}");
            return false;
        }
#else
        return false;
#endif
    }
}
