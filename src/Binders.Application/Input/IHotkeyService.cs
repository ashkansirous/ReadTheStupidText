namespace Binders.Application.Input;

/// <summary>
/// Registers the application's global hotkey and raises an event when it is
/// pressed. Registration is bound to a host window by the implementation.
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>Raised on the UI thread each time the global hotkey is pressed.</summary>
    event EventHandler? Pressed;

    /// <summary>
    /// Registers the hotkey against the given native window handle. Returns
    /// <c>false</c> if the OS refused the registration (e.g. already taken).
    /// </summary>
    bool Register(nint windowHandle);

    void Unregister();
}
