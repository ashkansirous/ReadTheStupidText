namespace ReadTheStupidText.Domain.Activity;

/// <summary>
/// One intercepted piece of text in the activity log. Identity is fixed; only
/// <see cref="State"/> changes over the entry's life (pending → reading → read,
/// or → ignored / interrupted / failed). Mutated only by the activity log, which
/// raises a change event so the UI can update the row in place.
/// </summary>
public sealed class ActivityEntry
{
    public ActivityEntry(int id, DateTimeOffset timestamp, ActivitySource source, string text)
    {
        Id = id;
        Timestamp = timestamp;
        Source = source;
        Text = text;
        State = ActivityState.Pending;
    }

    public int Id { get; }

    public DateTimeOffset Timestamp { get; }

    public ActivitySource Source { get; }

    public string Text { get; }

    public ActivityState State { get; set; }
}
