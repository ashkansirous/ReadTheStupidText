using Android.App;
using Android.Content;

namespace Sirous.Reading.Mobile.Shared.Crashes;

/// <summary>Android's part of the report facts (Slice 243 item 6): the memory headroom. The native <c>.so</c> files have no UUID here; they are matched by name in the release asset. Never throws.</summary>
internal static class AndroidRuntimeFacts
{
    /// <summary>
    /// Bytes the app can take before the system ends it, the same meaning as iOS's <c>os_proc_available_memory</c>: <c>AvailMem</c> minus the low-memory <c>Threshold</c>
    /// (<c>ActivityManager.MemoryInfo</c>), not below zero; -1 when unreadable.
    /// </summary>
    public static long MemoryHeadroomBytes()
    {
        try
        {
            if (global::Android.App.Application.Context.GetSystemService(Context.ActivityService) is not ActivityManager manager)
            {
                return -1;
            }

            using var info = new ActivityManager.MemoryInfo();
            manager.GetMemoryInfo(info);
            return Math.Max(0, info.AvailMem - info.Threshold);
        }
        catch (Exception ex)
        {
            // no-fallback: report facts only; the report simply has no memory-headroom bucket
            System.Diagnostics.Debug.WriteLine($"Memory headroom unreadable: {ex.Message}");
            return -1;
        }
    }
}
