using System.Runtime.InteropServices;

namespace ReadTheStupidText.Infrastructure.Input;

/// <summary>
/// Win32 clipboard-read P/Invoke surface for <c>ClipboardReader</c>. The tray-only Win32 surface
/// (hotkey, subclassing, SendInput, foreground probe) moved to Windows.Tray (Slice 128).
/// </summary>
internal static class NativeMethods
{
    /// <summary>Clipboard format for null-terminated UTF-16 text (CF_UNICODETEXT).</summary>
    internal const uint CfUnicodeText = 13;

    // Win32 clipboard read — focus-independent, unlike the WinRT clipboard API,
    // which matters because this app's window is never activated.
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint GetClipboardData(uint uFormat);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nint GlobalLock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalUnlock(nint hMem);
}
