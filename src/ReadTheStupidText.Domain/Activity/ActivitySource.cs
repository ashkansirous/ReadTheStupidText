namespace ReadTheStupidText.Domain.Activity;

/// <summary>What triggered a read, shown as a tag in the activity log.</summary>
public enum ActivitySource
{
    /// <summary>Auto-read from a UI Automation selection change.</summary>
    AutoRead,

    /// <summary>The global hotkey (copies the selection, then reads).</summary>
    Hotkey,

    /// <summary>The Play button in the control panel.</summary>
    Manual,
}
