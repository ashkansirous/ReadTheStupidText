using System.Text;
using Android.App;
using Android.Content;
using Android.Runtime;

namespace ReadTheStupidText.Mobile.Shared.Crashes;

/// <summary>
/// Android's side of crash reporting (Slice 227): the system's own history of why this app's process ended (<c>ActivityManager.GetHistoricalProcessExitReasons</c>, API 30+).
/// </summary>
internal static class AndroidCrashHooks
{
    private const int MaxExitReasons = 8;
    private const int MaxTextTraceBytes = 12 * 1024;

    // 9 KB of tombstone is 12 KB of base64: with the prefix it stays under the report's 16 KB stack cap, so the base64 is never cut mid-stream.
    private const int MaxTombstoneBytes = 9 * 1024;
    private const string CrashKindLowMemory = "low_memory";
    private const string TombstonePrefix = "tombstone-protobuf-base64:";
    private const int ForegroundOrVisible = 200; // ActivityManager.RunningAppProcessInfo.IMPORTANCE_VISIBLE; lower values are more important

    /// <summary>Native crashes, ANRs and low-memory kills of this app newer than <paramref name="lastSeen"/>, newest last. Empty below API 30 or on any failure.</summary>
    public static IReadOnlyList<NativeCrash> ReadExitReasons(DateTimeOffset lastSeen)
    {
        try
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                return [];
            }

            var manager = (ActivityManager?)Microsoft.Maui.ApplicationModel.Platform.AppContext.GetSystemService(Context.ActivityService);
            IList<ApplicationExitInfo>? exits = manager?.GetHistoricalProcessExitReasons(null, 0, MaxExitReasons);
            if (exits is null)
            {
                return [];
            }

            return exits.Select(ToNative).OfType<NativeCrash>().Where(c => c.Time > lastSeen).OrderBy(c => c.Time).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exit reasons unreadable: {ex.Message}");
            return [];
        }
    }

    private static NativeCrash? ToNative(ApplicationExitInfo info)
    {
        string? kind = KindOf((ApplicationExitInfoReason)info.Reason);
        if (kind is null || (kind == CrashKindLowMemory && (int)info.Importance > ForegroundOrVisible))
        {
            return null; // Android's low-memory killer routinely ends a backgrounded process: only a kill the person could have noticed is a report
        }

        var time = DateTimeOffset.FromUnixTimeMilliseconds(info.Timestamp);
        return new NativeCrash(kind, info.Reason.ToString(), info.Description ?? string.Empty, ReadTrace(info, kind), time);
    }

    private static string? KindOf(ApplicationExitInfoReason reason) => reason switch
    {
        ApplicationExitInfoReason.CrashNative => "native",
        ApplicationExitInfoReason.Anr => "anr",
        ApplicationExitInfoReason.LowMemory => CrashKindLowMemory,
        _ => null,
    };

    // A stream may return fewer bytes than are available; read until the buffer is full or the stream ends.
    private static int ReadFully(Stream stream, byte[] buffer)
    {
        int total = 0;
        int read;
        while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
        {
            total += read;
        }

        return total;
    }

    // An ANR trace is text; a native crash's trace is a tombstone in protobuf, so it is kept as base64 for an offline decoder.
    private static string ReadTrace(ApplicationExitInfo info, string kind)
    {
        try
        {
            using Stream? stream = info.TraceInputStream;
            if (stream is null)
            {
                return string.Empty;
            }

            bool tombstone = kind == "native";
            var buffer = new byte[tombstone ? MaxTombstoneBytes : MaxTextTraceBytes];
            int read = ReadFully(stream, buffer);
            return tombstone ? TombstonePrefix + Convert.ToBase64String(buffer, 0, read) : Encoding.UTF8.GetString(buffer, 0, read);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
