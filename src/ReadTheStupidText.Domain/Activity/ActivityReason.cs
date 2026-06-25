namespace ReadTheStupidText.Domain.Activity;

/// <summary>
/// Why an entry left the normal pending → reading → read path, shown alongside
/// the state in the activity log. <see cref="None"/> for entries still pending or
/// reading, and for those read through to completion.
/// </summary>
public enum ActivityReason
{
    /// <summary>No deviation — pending, reading, or read to the end.</summary>
    None,

    /// <summary>A newer read superseded this one (ignored if still pending,
    /// interrupted if already reading).</summary>
    NewSelection,

    /// <summary>The selection was cleared (deselected), dropping the pending read
    /// or stopping the one in progress.</summary>
    Deselected,

    /// <summary>Synthesis or playback failed.</summary>
    Error,
}
