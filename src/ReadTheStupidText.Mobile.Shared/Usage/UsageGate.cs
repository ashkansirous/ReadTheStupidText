using System.Runtime.InteropServices;
using ReadTheStupidText.Application.Usage;

namespace ReadTheStupidText.Mobile.Shared.Usage;

/// <summary>
/// Whether this build and device count (Slice 167, widened by Slice 179): the facts are read once at start and decided by <see cref="UsageEligibility"/>.
/// Debug build, an unstamped version, an emulator or simulator (MAUI <c>DeviceType.Virtual</c>), Android emulator and robot signs (build fingerprint,
/// hardware, model, an Intel process, a monkey), Firebase Test Lab (a setting that cannot be read counts as a test: fail closed), our harnesses'
/// off switch, an iOS build from TestFlight or App Review and a Windows install not signed by the Store do not count. Windows is one head
/// (Dino Reader's); a real user in a Windows VM with a Store install still counts. Any other fact that cannot be read means the build is not counted.
/// </summary>
public static class UsageGate
{
    private const string TestLabSetting = "firebase.test.lab";
    private const string TestLabOn = "true";
    private const string IosSandboxReceipt = "sandboxReceipt";

    private static readonly Lazy<UsageSkipReason> Cached = new(Decide);

    /// <summary>Why counts are not sent from here, or <see cref="UsageSkipReason.None"/> when they are.</summary>
    public static UsageSkipReason Reason => Cached.Value;

    private static UsageSkipReason Decide()
    {
        try
        {
            var facts = new UsageFacts(IsDebug(), UsageEnvironment.InstalledVersion, IsVirtual() || IsRobotDevice(), IsTestLab(), IsTestInstall(), IsNonStoreInstall());
            UsageSkipReason reason = UsageEligibility.Check(facts);
            System.Diagnostics.Debug.WriteLine($"Usage gate: {reason} ({facts})");
            return reason;
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
            System.Diagnostics.Debug.WriteLine($"Test Lab setting unreadable, treated as a test (fail closed): {ex.Message}");
            return true;
        }
#else
        return false;
#endif
    }

    // Slice 179: emulator and robot signs beyond MAUI's DeviceType.Virtual. Real phones run ARM processes; Play's pre-launch robot is an Intel image.
    private static bool IsRobotDevice()
    {
#if ANDROID
        var arch = RuntimeInformation.ProcessArchitecture;
        return arch is Architecture.X86 or Architecture.X64
            || UsageEligibility.IsEmulatorBuild(global::Android.OS.Build.Fingerprint, global::Android.OS.Build.Hardware, global::Android.OS.Build.Model, global::Android.OS.Build.Product)
            || global::Android.App.ActivityManager.IsUserAMonkey;
#else
        return false;
#endif
    }

    // Slice 179: our harnesses switch counting off for Release runs: the env var everywhere, and on Android a marker file in the app's external files folder (adb can write there).
    private static bool IsTestInstall()
    {
        if (UsageEligibility.IsNoUsageValue(Environment.GetEnvironmentVariable(UsageEligibility.NoUsageVariable)))
        {
            return true;
        }

#if ANDROID
        try
        {
            string? folder = Microsoft.Maui.ApplicationModel.Platform.AppContext.GetExternalFilesDir(null)?.AbsolutePath;
            return folder is not null && File.Exists(Path.Combine(folder, UsageEligibility.NoUsageMarkerFile));
        }
        catch (Exception)
        {
            return false; // an unreadable marker means no marker: a real user still counts (the harness verifies its own write)
        }
#else
        return false;
#endif
    }

    // Slice 179: only store installs count. iOS: TestFlight and App Review builds carry a sandboxReceipt. Windows: the package must be Store-signed.
    private static bool IsNonStoreInstall()
    {
#if IOS
        return Foundation.NSBundle.MainBundle.AppStoreReceiptUrl?.LastPathComponent == IosSandboxReceipt;
#elif WINDOWS
        return !ReadTheStupidText.Infrastructure.Usage.WindowsUsage.IsStoreInstall();
#else
        return false;
#endif
    }
}
