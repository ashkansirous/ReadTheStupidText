namespace ReadTheStupidText.Application.Input;

/// <summary>
/// Watches for text-selection changes in apps that expose their selection (via
/// UI Automation) and raises the newly selected text. This is the auto-read
/// path; apps with no selection API fall back to the global hotkey.
/// </summary>
public interface ISelectionMonitor : IDisposable
{
    /// <summary>Raised with the selected text when a selection changes.</summary>
    event EventHandler<string>? SelectionChanged;

    bool IsRunning { get; }

    void Start();

    void Stop();
}
