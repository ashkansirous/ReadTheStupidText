using ReadTheStupidText.Domain.Activity;

namespace ReadTheStupidText.Application.Activity;

/// <summary>
/// A live, in-memory log of read activity. The read paths add entries and update
/// their state; the log window subscribes to the change events and renders them.
/// Bounded (oldest entries drop) and not persisted — it resets each launch.
/// </summary>
public interface IActivityLog
{
    /// <summary>A snapshot of the current entries, oldest first.</summary>
    IReadOnlyList<ActivityEntry> Entries { get; }

    /// <summary>Raised when a new entry is appended.</summary>
    event EventHandler<ActivityEntry>? EntryAdded;

    /// <summary>Raised when an existing entry's <see cref="ActivityEntry.State"/> changes.</summary>
    event EventHandler<ActivityEntry>? EntryChanged;

    /// <summary>Appends a new entry in the <see cref="ActivityState.Pending"/> state,
    /// tagged with how it was triggered and the window it came from.</summary>
    ActivityEntry Add(ActivityTrigger trigger, WindowSource? window, string text);

    /// <summary>Transitions an entry to a new state and reason (no-op if both
    /// unchanged). The reason explains a deviation from the read-to-completion
    /// path and defaults to <see cref="ActivityReason.None"/>.</summary>
    void SetState(ActivityEntry entry, ActivityState state, ActivityReason reason = ActivityReason.None);
}
