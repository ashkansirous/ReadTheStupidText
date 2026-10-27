using System.Diagnostics;
using System.Runtime.InteropServices;
using ReadTheStupidText.Application.Usage;
#if WINDOWS
using ReadTheStupidText.Infrastructure.Usage;
#endif

namespace ReadTheStupidText.Mobile.Shared.Usage;

/// <summary>
/// Reads the coarse device description a batch of counts carries (Slice 166), once it reads: form factor, architecture, core count and
/// total memory, each reduced to a closed list or bucket by <see cref="DeviceBuckets"/>. Never the model, the manufacturer or an exact number.
/// A probe that fails answers null (the batch carries no device object) and is tried again with the next batch.
/// </summary>
public static class DeviceProbe
{
    private static volatile DeviceContext? cached;

    /// <summary>The device description, or null when it could not be read. A good reading is kept; a failed one is retried on the next call.</summary>
    public static DeviceContext? Current => cached ??= ReadOrNull();

    private static DeviceContext? ReadOrNull()
    {
        try
        {
            return Read();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Device context unreadable: {ex.Message}");
            return null;
        }
    }

    private static DeviceContext Read()
    {
#if WINDOWS
        return WindowsDeviceProbe.Read();
#else
        return DeviceBuckets.Build(
            UsageEnvironment.OsMajor, FormFactor(), RuntimeInformation.OSArchitecture, Environment.ProcessorCount, TotalMemoryBytes());
#endif
    }

#if !WINDOWS
    // Idiom is the MAUI view of the device class; TV, watch and anything unknown are "other".
    private static string FormFactor()
    {
        DeviceIdiom idiom = DeviceInfo.Idiom;
        return idiom == DeviceIdiom.Phone ? DeviceFormFactor.Phone
            : idiom == DeviceIdiom.Tablet ? DeviceFormFactor.Tablet
            : idiom == DeviceIdiom.Desktop ? DeviceFormFactor.Desktop
            : DeviceFormFactor.Other;
    }

    private static long TotalMemoryBytes()
    {
#if ANDROID
        var manager = (global::Android.App.ActivityManager?)Microsoft.Maui.ApplicationModel.Platform.AppContext.GetSystemService(global::Android.Content.Context.ActivityService);
        var info = new global::Android.App.ActivityManager.MemoryInfo();
        manager?.GetMemoryInfo(info);
        return info.TotalMem;
#elif IOS
        return (long)Foundation.NSProcessInfo.ProcessInfo.PhysicalMemory;
#else
        return 0;
#endif
    }
#endif
}
