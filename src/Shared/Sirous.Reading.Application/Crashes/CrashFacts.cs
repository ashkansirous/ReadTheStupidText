namespace Sirous.Reading.Application.Crashes;

/// <summary>
/// What a report says about the build and the moment (Slice 243 item 6), so a crash can be tied to its debug symbols and to the phone's state. Coarse buckets only,
/// no exact memory figure (same rule as <see cref="Sirous.Reading.Application.Usage.DeviceContext"/>). Every field is null when it could not be read or does not apply
/// (the memory and state fields only describe a report about the launch that is running, never one about an earlier launch).
/// </summary>
/// <param name="Build">The build number: iOS <c>CFBundleVersion</c>, Android <c>versionCode</c>.</param>
/// <param name="BinaryUuid">The main executable's Mach-O UUID (32 uppercase hex digits, no dashes) that the dSYM is matched by; null on Android (its natives are matched by file name and build id in the release asset).</param>
/// <param name="MemoryHeadroom">A <see cref="CrashMemory.Bucket"/> value: how much more memory the app can take before the system ends it, the same meaning on both phones. iOS: <c>os_proc_available_memory</c>. Android: <c>ActivityManager.MemoryInfo.AvailMem</c> minus its low-memory <c>Threshold</c> (not below zero).</param>
/// <param name="MemoryApp">A <see cref="CrashMemory.Bucket"/> value: the app process's own resident memory (<c>Environment.WorkingSet</c>) on both phones.</param>
/// <param name="AppState">A <see cref="CrashAppState"/> value.</param>
public sealed record CrashFacts(string? Build = null, string? BinaryUuid = null, string? MemoryHeadroom = null, string? MemoryApp = null, string? AppState = null)
{
    /// <summary>
    /// <paramref name="build"/> with the moment's memory and state filled in, for a report about the launch that is running. A negative size or an unknown
    /// state leaves that field null.
    /// </summary>
    public static CrashFacts Snapshot(CrashFacts? build, long headroomBytes, long appBytes, bool? inForeground) =>
        (build ?? new CrashFacts()) with
        {
            MemoryHeadroom = CrashMemory.Bucket(headroomBytes),
            MemoryApp = CrashMemory.Bucket(appBytes),
            AppState = inForeground is null ? null : inForeground.Value ? CrashAppState.Foreground : CrashAppState.Background,
        };
}

/// <summary>The closed list of app states (the Worker's list must match).</summary>
public static class CrashAppState
{
    public const string Foreground = "foreground";
    public const string Background = "background";
}

/// <summary>Turns a memory size into one of a few labelled ranges in MB (the Worker's list must match).</summary>
public static class CrashMemory
{
    public static readonly IReadOnlyList<string> Buckets = ["<128", "128-256", "256-512", "512-1024", "1024-2048", "2048+"];

    private const long BytesPerMb = 1024L * 1024;
    private static readonly long[] Limits = [128, 256, 512, 1024, 2048];

    /// <summary>The range of <paramref name="bytes"/>; null for a negative size (unreadable). Zero is "&lt;128".</summary>
    public static string? Bucket(long bytes)
    {
        if (bytes < 0)
        {
            return null;
        }

        long mb = bytes / BytesPerMb;
        for (int i = 0; i < Limits.Length; i++)
        {
            if (mb < Limits[i])
            {
                return Buckets[i];
            }
        }

        return Buckets[^1];
    }
}
