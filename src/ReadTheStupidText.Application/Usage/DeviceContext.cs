using System.Runtime.InteropServices;

namespace ReadTheStupidText.Application.Usage;

/// <summary>
/// What a batch of counts says about the device (Slice 166): coarse, closed values only, sent once per batch next to the events. Never a
/// model, a manufacturer, an exact chip, exact memory or core numbers, or an id: those together with the country would fingerprint one phone
/// (the country is added by the Worker from the connection and is never sent by the app).
/// </summary>
/// <param name="OsMajor">The operating system's major version (15 for Android 15).</param>
/// <param name="FormFactor">A <see cref="DeviceFormFactor"/> value.</param>
/// <param name="Arch">A <see cref="DeviceArch"/> value.</param>
/// <param name="Cores">A <see cref="DeviceBuckets.Cores"/> value.</param>
/// <param name="Ram">A <see cref="DeviceBuckets.Ram"/> value.</param>
public sealed record DeviceContext(int OsMajor, string FormFactor, string Arch, string Cores, string Ram);

/// <summary>The closed list of form factors (the Worker's list must match).</summary>
public static class DeviceFormFactor
{
    public const string Phone = "phone";
    public const string Tablet = "tablet";
    public const string Desktop = "desktop";
    public const string Other = "other";
}

/// <summary>The closed list of CPU architectures (the Worker's list must match).</summary>
public static class DeviceArch
{
    public const string Arm64 = "arm64";
    public const string X64 = "x64";
    public const string X86 = "x86";
    public const string Arm = "arm";
    public const string Other = "other";
}

/// <summary>Turns a measured device value into one of a few coarse buckets.</summary>
public static class DeviceBuckets
{
    private const long BytesPerGb = 1024L * 1024 * 1024;

    private const string FewCores = "1-4";
    private const string MidCores = "5-8";
    private const string ManyCores = "9+";
    private const int FewCoresLimit = 4;
    private const int MidCoresLimit = 8;

    private const string RamUnder3 = "<3";
    private const string Ram3To4 = "3-4";
    private const string Ram4To6 = "4-6";
    private const string Ram6To8 = "6-8";
    private const string RamOver8 = "8+";

    /// <summary>The bucket of a core count: 4 or fewer is "1-4", 5 to 8 "5-8", more "9+".</summary>
    public static string Cores(int count) => count switch
    {
        <= FewCoresLimit => FewCores,
        <= MidCoresLimit => MidCores,
        _ => ManyCores,
    };

    /// <summary>
    /// The bucket of total physical memory in GB: the size in GiB (2^30 bytes) is rounded up to a whole number first, because a phone sold as 4 GB
    /// reports about 3.7 GiB (the system keeps some). So about 2.7 GiB is "3-4", 3.7 is "4-6", 5.6 is "6-8", 7.4 is "8+". Null for an unreadable (zero or negative) size.
    /// </summary>
    public static string? Ram(long totalBytes)
    {
        if (totalBytes <= 0)
        {
            return null;
        }
        long wholeGb = (totalBytes + BytesPerGb - 1) / BytesPerGb;
        return wholeGb switch
        {
            < 3 => RamUnder3,
            < 4 => Ram3To4,
            < 6 => Ram4To6,
            < 8 => Ram6To8,
            _ => RamOver8,
        };
    }

    /// <summary>The list value of a CPU architecture; any architecture the list does not name is "other".</summary>
    public static string Arch(Architecture architecture) => architecture switch
    {
        Architecture.Arm64 => DeviceArch.Arm64,
        Architecture.X64 => DeviceArch.X64,
        Architecture.X86 => DeviceArch.X86,
        Architecture.Arm => DeviceArch.Arm,
        _ => DeviceArch.Other,
    };

    /// <summary>The device context for the measured values; an unreadable memory size gives an empty RAM bucket (the Worker leaves that field empty).</summary>
    public static DeviceContext Build(int osMajor, string formFactor, Architecture architecture, int cores, long totalMemoryBytes) =>
        new(osMajor, formFactor, Arch(architecture), Cores(cores), Ram(totalMemoryBytes) ?? string.Empty);
}
