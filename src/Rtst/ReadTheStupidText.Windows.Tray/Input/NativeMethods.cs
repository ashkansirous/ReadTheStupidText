using System.Runtime.InteropServices;

namespace ReadTheStupidText.Windows.Tray.Input;

/// <summary>
/// Tray-only Win32 P/Invoke surface: system-wide hotkey and window subclassing (WinUI 3 has no
/// managed global-hotkey API), the clipboard-format listener, SendInput for the simulated Ctrl+C and
/// the foreground-window probe. Distinct from Infrastructure's internal NativeMethods (the clipboard
/// read); the two assemblies never see each other's.
/// </summary>
internal static class NativeMethods
{
    internal const uint WmHotkey = 0x0312;

    /// <summary>Posted to a clipboard-format listener window when the clipboard changes.</summary>
    internal const uint WmClipboardUpdate = 0x031D;

    internal const uint ModControl = 0x0002;
    internal const uint ModWin = 0x0008;

    /// <summary>Prevents auto-repeat from firing the hotkey while held down.</summary>
    internal const uint ModNoRepeat = 0x4000;

    /// <summary>Virtual-key code for the 'R' key.</summary>
    internal const uint VkR = 0x52;

    // Synthetic-input constants for simulating a Ctrl+C copy via SendInput.
    internal const uint InputKeyboard = 1;
    internal const uint KeyEventKeyUp = 0x0002;
    internal const ushort VkControl = 0x11;
    internal const ushort VkLeftWin = 0x5B;
    internal const ushort VkRightWin = 0x5C;
    internal const ushort VkC = 0x43;

    internal delegate nint SubclassProc(
        nint hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowSubclass(
        nint hWnd, SubclassProc pfnSubclass, nuint uIdSubclass, nuint dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RemoveWindowSubclass(
        nint hWnd, SubclassProc pfnSubclass, nuint uIdSubclass);

    [DllImport("comctl32.dll")]
    internal static extern nint DefSubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AddClipboardFormatListener(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RemoveClipboardFormatListener(nint hWnd);

    // Foreground-window probe — identifies the app/window a read originated from.
    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetWindowText(nint hWnd, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int GetWindowTextLength(nint hWnd);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    // The union is sized to the largest member (MouseInput) so the marshalled
    // struct matches the OS's INPUT size; SendInput rejects a wrong cbSize.
    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }
}
