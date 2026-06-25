namespace ReadTheStupidText.Domain.Activity;

/// <summary>
/// One intercepted piece of text in the activity log. Identity, trigger and
/// originating <see cref="Window"/> are fixed; only <see cref="State"/> and
/// <see cref="Reason"/> change over the entry's life (pending → reading → read,
/// or → ignored / interrupted / failed, with the reason why it deviated).
/// Mutated only by the activity log, which raises a change event so the UI can
/// update the row in place.
/// </summary>
public sealed class ActivityEntry
{
    public ActivityEntry(int id, DateTimeOffset timestamp, ActivityTrigger trigger, WindowSource? window, string text)
    {
        Id = id;
        Timestamp = timestamp;
        Trigger = trigger;
        Window = window;
        Text = text;
        State = ActivityState.Pending;
        Reason = ActivityReason.None;
    }

    public int Id { get; }

    public DateTimeOffset Timestamp { get; }

    /// <summary>How the read was triggered (auto-read / hotkey / manual / clipboard).</summary>
    public ActivityTrigger Trigger { get; }

    /// <summary>The foreground window the text came from, or null if unknown.</summary>
    public WindowSource? Window { get; }

    public string Text { get; }

    public ActivityState State { get; set; }

    public ActivityReason Reason { get; set; }
}
