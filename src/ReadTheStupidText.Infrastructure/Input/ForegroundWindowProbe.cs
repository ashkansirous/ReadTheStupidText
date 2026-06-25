using System.Diagnostics;
using System.Text;
using ReadTheStupidText.Application.Input;
using ReadTheStupidText.Domain.Activity;

namespace ReadTheStupidText.Infrastructure.Input;

/// <summary>
/// Reads the foreground window's owning process and title via Win32, mapping them
/// to a <see cref="WindowSource"/>. Best-effort: a window that vanishes mid-probe
/// or a process we can't open yields whatever fields could be read (possibly
/// empty), never an exception.
/// </summary>
public sealed class ForegroundWindowProbe : IForegroundWindow
{
    private const int MaxTitleLength = 200;

    public WindowSource? Capture()
    {
        nint handle = NativeMethods.GetForegroundWindow();
        if (handle == 0)
        {
            return null;
        }

        return new WindowSource(ReadProcessName(handle), ReadTitle(handle));
    }

    private static string ReadProcessName(nint handle)
    {
        try
        {
            NativeMethods.GetWindowThreadProcessId(handle, out uint processId);
            if (processId == 0)
            {
                return string.Empty;
            }

            using Process process = Process.GetProcessById((int)processId);
            return Prettify(process.ProcessName);
        }
        catch (Exception)
        {
            // The process may have exited, or be protected; the title still helps.
            return string.Empty;
        }
    }

    private static string ReadTitle(nint handle)
    {
        int length = NativeMethods.GetWindowTextLength(handle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(length + 1);
        int copied = NativeMethods.GetWindowText(handle, buffer, buffer.Capacity);
        string title = copied > 0 ? buffer.ToString() : string.Empty;
        return title.Length <= MaxTitleLength ? title : title[..MaxTitleLength];
    }

    // "chrome" → "Chrome"; process names are otherwise shown as-is.
    private static string Prettify(string processName) =>
        string.IsNullOrEmpty(processName)
            ? processName
            : char.ToUpperInvariant(processName[0]) + processName[1..];
}
