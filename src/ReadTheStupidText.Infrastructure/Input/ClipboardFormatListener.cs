using ReadTheStupidText.Application.Input;

namespace ReadTheStupidText.Infrastructure.Input;

/// <summary>
/// Registers the host window as a clipboard-format listener and raises
/// <see cref="ContentChanged"/> on the UI thread when the OS posts
/// <see cref="NativeMethods.WmClipboardUpdate"/>. Subclasses the window with its
/// own id so it coexists with the hotkey's subclass on the same handle. This is
/// focus-independent (unlike the WinRT <c>Clipboard.ContentChanged</c> event),
/// which matters because the tray app's window is never activated.
/// </summary>
public sealed class ClipboardFormatListener : IClipboardMonitor
{
    private const nuint SubclassId = 2;

    // Held as a field so the delegate isn't garbage-collected while the native
    // window keeps a pointer to it.
    private NativeMethods.SubclassProc? _subclassProc;
    private nint _windowHandle;
    private bool _registered;

    public event EventHandler? ContentChanged;

    public bool IsRegistered => _registered;

    public void Register(nint windowHandle)
    {
        if (_registered)
        {
            return;
        }

        _windowHandle = windowHandle;
        _subclassProc = HandleMessage;

        // Without the subclass the WM_CLIPBOARDUPDATE message is never observed,
        // so don't register the listener against a window we can't watch.
        if (!NativeMethods.SetWindowSubclass(_windowHandle, _subclassProc, SubclassId, dwRefData: 0))
        {
            _subclassProc = null;
            return;
        }

        _registered = NativeMethods.AddClipboardFormatListener(_windowHandle);
        if (!_registered)
        {
            NativeMethods.RemoveWindowSubclass(_windowHandle, _subclassProc, SubclassId);
            _subclassProc = null;
        }
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        NativeMethods.RemoveClipboardFormatListener(_windowHandle);
        if (_subclassProc is not null)
        {
            NativeMethods.RemoveWindowSubclass(_windowHandle, _subclassProc, SubclassId);
        }

        _registered = false;
    }

    private nint HandleMessage(
        nint hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData)
    {
        if (uMsg == NativeMethods.WmClipboardUpdate)
        {
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    public void Dispose() => Unregister();
}
