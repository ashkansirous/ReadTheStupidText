using Android.Content;
using Android.Content.PM;

namespace ReadTheStupidText.Mobile.Shared.Crashes;

/// <summary>Which package installed this app (Slice 227): Google Play's is <c>com.android.vending</c>; see <c>InstallChannels.FromAndroidInstaller</c>.</summary>
internal static class AndroidInstallSource
{
    /// <summary>The installing package's name, or null when there is none or it cannot be read (an adb or sideload install has none; unreadable counts as not Play).</summary>
    public static string? InstallerPackage()
    {
        try
        {
            Context context = Microsoft.Maui.ApplicationModel.Platform.AppContext;
            PackageManager? packages = context.PackageManager;
            string? self = context.PackageName;
            if (packages is null || self is null)
            {
                return null;
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                return packages.GetInstallSourceInfo(self)?.InstallingPackageName;
            }

#pragma warning disable CA1422 // the only way below API 30
            return packages.GetInstallerPackageName(self);
#pragma warning restore CA1422
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Installer package unreadable, treated as not Play: {ex.Message}");
            return null;
        }
    }
}
