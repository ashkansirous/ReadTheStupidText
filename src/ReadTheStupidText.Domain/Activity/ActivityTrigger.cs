namespace ReadTheStupidText.Domain.Activity;

/// <summary>How a read was triggered, shown as a tag in the activity log.</summary>
public enum ActivityTrigger
{
    /// <summary>Auto-read from a UI Automation selection change.</summary>
    AutoRead,

    /// <summary>The global hotkey (copies the selection, then reads).</summary>
    Hotkey,

    /// <summary>The Play button in the control panel.</summary>
    Manual,

    /// <summary>Auto-read from a clipboard copy — the path for apps with no UI
    /// Automation text selection, such as Windows Terminal / the console.</summary>
    Clipboard,
}
