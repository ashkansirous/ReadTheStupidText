using ReadTheStupidText.Domain.Activity;

namespace ReadTheStupidText.Application.Input;

/// <summary>
/// Reads the current foreground window so a read can record where its text came
/// from (the app and window title shown in the activity log's Source column).
/// </summary>
public interface IForegroundWindow
{
    /// <summary>The foreground window right now, or null if none can be read.</summary>
    WindowSource? Capture();
}
