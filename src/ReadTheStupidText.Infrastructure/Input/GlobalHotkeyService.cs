using ReadTheStupidText.Application.Input;

namespace ReadTheStupidText.Infrastructure.Input;

/// <summary>
/// Registers the default global hotkey (Ctrl+Win+R) against a host window and
/// raises <see cref="Pressed"/> on the UI thread when it fires. It subclasses
/// the window so the WM_HOTKEY message can be observed without owning the loop.
/// </summary>
public sealed class GlobalHotkeyService : IHotkeyService
{
    private const int HotkeyId = 1;
    private const nuint SubclassId = 1;
    private const uint Modifiers =
        NativeMethods.ModControl | NativeMethods.ModWin | NativeMethods.ModNoRepeat;

    // Held as a field so the delegate is not garbage-collected while the native
    // window keeps a pointer to it.
    private NativeMethods.SubclassProc? _subclassProc;
    private nint _windowHandle;
    private bool _registered;

    public event EventHandler? Pressed;

    public bool Register(nint windowHandle)
    {
        if (_registered)
        {
            return true;
        }

        _windowHandle = windowHandle;
        _subclassProc = HandleMessage;
        NativeMethods.SetWindowSubclass(_windowHandle, _subclassProc, SubclassId, dwRefData: 0);
        _registered = NativeMethods.RegisterHotKey(
            _windowHandle, HotkeyId, Modifiers, NativeMethods.VkR);
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_windowHandle, HotkeyId);
        if (_subclassProc is not null)
        {
            NativeMethods.RemoveWindowSubclass(_windowHandle, _subclassProc, SubclassId);
        }

        _registered = false;
    }

    private nint HandleMessage(
        nint hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData)
    {
        if (uMsg == NativeMethods.WmHotkey && (int)wParam == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    public void Dispose() => Unregister();
}
