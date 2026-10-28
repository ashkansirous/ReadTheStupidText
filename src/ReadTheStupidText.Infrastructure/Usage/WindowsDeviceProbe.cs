using System.Runtime.InteropServices;
using ReadTheStupidText.Application.Usage;

namespace ReadTheStupidText.Infrastructure.Usage;

/// <summary>
/// The desktop's coarse device description (Slice 166), shared by the tray app and Dino Reader's Windows head: always <c>desktop</c>,
/// the architecture and core bucket from the runtime, and the RAM bucket from the machine's total physical memory
/// (<c>GlobalMemoryStatusEx</c>; the GC's available-memory figure shrinks under a job or container limit, so it is not used).
/// </summary>
public static class WindowsDeviceProbe
{
    /// <summary>The device context of this machine.</summary>
    public static DeviceContext Read() => Build(WindowsUsage.OsMajor(Environment.OSVersion.Version.Build), PhysicalMemoryBytes());

    internal static DeviceContext Build(int osMajor, long totalMemoryBytes) =>
        DeviceBuckets.Build(osMajor, DeviceFormFactor.Desktop, RuntimeInformation.OSArchitecture, Environment.ProcessorCount, totalMemoryBytes);

    /// <summary>The total physical memory in bytes, or 0 when the system will not say.</summary>
    internal static long PhysicalMemoryBytes()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status) ? (long)status.TotalPhys : 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}
