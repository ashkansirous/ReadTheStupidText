#if DEBUG
namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Debug-only device-test hook (Slice 183): when the marker file <c>throttle-pack-downloads</c> exists in the app's external files
/// folder (Android; adb can write there) or the environment variable <c>RTST_THROTTLE_PACK_DOWNLOADS</c> is set, the HTTP pack copy runs at
/// about 200 KB/s, so a test can reach the notification's Cancel on a pack that would otherwise finish in seconds. Compiled out of Release.
/// </summary>
internal static class PackDownloadThrottle
{
    internal const string MarkerFile = "throttle-pack-downloads";
    private const string Variable = "RTST_THROTTLE_PACK_DOWNLOADS";
    private const double BytesPerSecond = 200 * 1024;

    internal static bool IsOn()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable)))
        {
            return true;
        }

#if ANDROID
        try
        {
            string? folder = Microsoft.Maui.ApplicationModel.Platform.AppContext.GetExternalFilesDir(null)?.AbsolutePath;
            return folder is not null && File.Exists(Path.Combine(folder, MarkerFile));
        }
        catch (Exception)
        {
            return false;
        }
#else
        return false;
#endif
    }

    internal static Task PauseAsync(int bytes, CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromSeconds(bytes / BytesPerSecond), cancellationToken);
}
#endif
