using System.Runtime.InteropServices;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Infrastructure.Input;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Reads text from the Windows clipboard via the Win32 clipboard API. This is
/// deliberately not the WinRT clipboard, which Microsoft documents as readable
/// only while the calling app is focused — this app's tray window is never
/// activated, so the Win32 path (focus-independent) is the reliable one. The read
/// runs off the UI thread; <see cref="OpenClipboard"/> can briefly fail while
/// another process holds the clipboard, so it is retried.
/// </summary>
public sealed class ClipboardReader : IClipboardReader
{
    private const int OpenAttempts = 5;
    private const int RetryDelayMs = 15;

    public Task<string?> GetTextAsync() => Task.Run(ReadUnicodeText);

    private static string? ReadUnicodeText()
    {
        for (int attempt = 0; attempt < OpenAttempts; attempt++)
        {
            if (NativeMethods.OpenClipboard(0))
            {
                try
                {
                    return ExtractText();
                }
                finally
                {
                    NativeMethods.CloseClipboard();
                }
            }

            Thread.Sleep(RetryDelayMs);
        }

        return null;
    }

    private static string? ExtractText()
    {
        if (!NativeMethods.IsClipboardFormatAvailable(NativeMethods.CfUnicodeText))
        {
            return null;
        }

        nint handle = NativeMethods.GetClipboardData(NativeMethods.CfUnicodeText);
        if (handle == 0)
        {
            return null;
        }

        nint pointer = NativeMethods.GlobalLock(handle);
        if (pointer == 0)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(pointer);
        }
        finally
        {
            NativeMethods.GlobalUnlock(handle);
        }
    }
}
