namespace ReadTheStupidText.Application.Input;

/// <summary>
/// Watches the system clipboard and raises <see cref="ContentChanged"/> whenever
/// its contents change. This is the auto-read path for apps that expose no UI
/// Automation selection — notably the console (Windows Terminal / PowerShell),
/// where the natural flow is select-then-copy. Registered against a host window
/// so the OS notification arrives on the UI thread, like the global hotkey.
/// </summary>
public interface IClipboardMonitor : IDisposable
{
    /// <summary>Raised on the UI thread after the clipboard contents change.</summary>
    event EventHandler? ContentChanged;

    bool IsRegistered { get; }

    /// <summary>Begins listening for clipboard changes on the given host window.</summary>
    void Register(nint windowHandle);

    /// <summary>Stops listening.</summary>
    void Unregister();
}
