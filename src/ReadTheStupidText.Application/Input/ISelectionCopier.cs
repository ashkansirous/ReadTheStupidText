namespace ReadTheStupidText.Application.Input;

/// <summary>
/// Copies the focused application's current selection to the clipboard by
/// simulating a copy command, so the selection can then be read aloud — this is
/// the fallback path for apps that expose no selection API (terminals, CLI).
/// </summary>
public interface ISelectionCopier
{
    /// <summary>
    /// Sends a copy command to the focused window and waits for the clipboard
    /// to settle before returning.
    /// </summary>
    Task CopyAsync();
}
