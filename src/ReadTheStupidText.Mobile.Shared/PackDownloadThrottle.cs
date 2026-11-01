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

    private static volatile bool _dropRequested;

    /// <summary>Slice 233: the <c>drop-pack-network</c> hook arms one dropped connection (a running dataSync service is exempt from the per-app network block, so adb cannot cut it).</summary>
    internal static void RequestDrop() => _dropRequested = true;

    /// <summary>A download copy starts with no leftover request from a hook sent while nothing ran.</summary>
    internal static void ClearDrop() => _dropRequested = false;

    /// <summary>Throws what a dropped connection throws, once, so the app's real failure path (classify, notice) runs.</summary>
    internal static void ThrowIfDropRequested()
    {
        if (_dropRequested)
        {
            _dropRequested = false;
            throw new HttpRequestException(HttpRequestError.ConnectionError, "Test hook drop-pack-network: connection dropped.");
        }
    }

    internal static Task PauseAsync(int bytes, CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromSeconds(bytes / BytesPerSecond), cancellationToken);
}
#endif
