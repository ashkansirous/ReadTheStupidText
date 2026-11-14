using System.Runtime.InteropServices;
using Foundation;
using Sirous.Reading.Application.Crashes;

namespace Sirous.Reading.Mobile.Shared.Crashes;

/// <summary>iOS's part of the report facts (Slice 243 item 6): the main executable's UUID (matches the dSYM) and the memory headroom before the system ends the app. Never throws.</summary>
internal static class IosRuntimeFacts
{
    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern nuint os_proc_available_memory();

    /// <summary>The Mach-O UUID of the app's executable (32 hex digits), or null.</summary>
    public static string? BinaryUuid()
    {
        try
        {
            string? path = NSBundle.MainBundle.ExecutablePath;
            if (path is null)
            {
                return null;
            }

            using FileStream file = File.OpenRead(path);
            return MachOUuid.Read(file);
        }
        catch (Exception ex)
        {
            // no-fallback: report facts only; the report simply has no UUID
            System.Diagnostics.Debug.WriteLine($"Executable UUID unreadable: {ex.Message}");
            return null;
        }
    }

    /// <summary>The main executable's file name, which MetricKit names as <c>binaryName</c> in its call-stack trees; null when unknown.</summary>
    public static string? ExecutableName()
    {
        try
        {
            string? path = NSBundle.MainBundle.ExecutablePath;
            return path is null ? null : Path.GetFileName(path);
        }
        catch (Exception ex)
        {
            // no-fallback: report facts only; the report simply has no UUID
            System.Diagnostics.Debug.WriteLine($"Executable name unreadable: {ex.Message}");
            return null;
        }
    }

    /// <summary>Bytes the app can take before the system ends it (<c>os_proc_available_memory</c>), the same meaning as Android's <c>AvailMem</c> minus <c>Threshold</c>; -1 when unreadable or reported as 0.</summary>
    public static long MemoryHeadroomBytes()
    {
        try
        {
            long bytes = (long)os_proc_available_memory();
            return bytes > 0 ? bytes : -1; // 0 means "not meaningful" (simulator, old OS), not "no memory left": no bucket rather than "<128"
        }
        catch (Exception ex)
        {
            // no-fallback: report facts only; the report simply has no memory-headroom bucket
            System.Diagnostics.Debug.WriteLine($"Memory headroom unreadable: {ex.Message}");
            return -1;
        }
    }
}
