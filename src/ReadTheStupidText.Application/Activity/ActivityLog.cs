using ReadTheStupidText.Domain.Activity;

namespace ReadTheStupidText.Application.Activity;

/// <summary>
/// In-memory implementation of <see cref="IActivityLog"/>: a capped ring of
/// entries (oldest drop past the cap), with monotonic ids and change events.
/// Thread-safe for the add/state-set operations, since reads are driven from UI
/// Automation callback threads while the window observes on the UI thread.
/// </summary>
public sealed class ActivityLog : IActivityLog
{
    private const int Capacity = 200;
    private const int MaxTextLength = 500;

    private readonly object _gate = new();
    private readonly LinkedList<ActivityEntry> _entries = new();
    private int _nextId;

    public IReadOnlyList<ActivityEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToList();
            }
        }
    }

    public event EventHandler<ActivityEntry>? EntryAdded;

    public event EventHandler<ActivityEntry>? EntryChanged;

    public ActivityEntry Add(ActivityTrigger trigger, WindowSource? window, string text)
    {
        ActivityEntry entry;
        lock (_gate)
        {
            entry = new ActivityEntry(++_nextId, DateTimeOffset.Now, trigger, window, Truncate(text));
            _entries.AddLast(entry);
            while (_entries.Count > Capacity)
            {
                _entries.RemoveFirst();
            }
        }

        EntryAdded?.Invoke(this, entry);
        return entry;
    }

    public void SetState(ActivityEntry entry, ActivityState state, ActivityReason reason = ActivityReason.None)
    {
        if (entry.State == state && entry.Reason == reason)
        {
            return;
        }

        entry.State = state;
        entry.Reason = reason;
        EntryChanged?.Invoke(this, entry);
    }

    private static string Truncate(string text) =>
        text.Length <= MaxTextLength ? text : text[..MaxTextLength];
}
