namespace ReadTheStupidText.Domain.Activity;

/// <summary>
/// One intercepted piece of text in the activity log. Identity, trigger and
/// originating <see cref="Window"/> are fixed; only <see cref="State"/> and
/// <see cref="Reason"/> change over the entry's life (pending → reading → read,
/// or → ignored / interrupted / failed, with the reason why it deviated), plus the
/// local timing diagnostics recorded when audio first plays.
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

    /// <summary>
    /// Local timing diagnostic: wall time from this entry's creation to the first
    /// audio (the reader's first Playing transition). Null until the read starts
    /// playing, or if it never does. Never leaves the device — see Decision 26.
    /// </summary>
    public TimeSpan? TimeToFirstAudio { get; set; }

    /// <summary>
    /// Local timing diagnostic: time spent synthesizing before audio began
    /// (GeneratingAudio → first Playing). Null until playing. This is the metric
    /// the startup warm-up (Slice 17) is meant to shrink. Never leaves the device.
    /// </summary>
    public TimeSpan? SynthesisDuration { get; set; }
}
