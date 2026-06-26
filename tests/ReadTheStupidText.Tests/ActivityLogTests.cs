using ReadTheStupidText.Application.Activity;
using ReadTheStupidText.Domain.Activity;

namespace ReadTheStupidText.Tests;

public class ActivityLogTests
{
    private static ActivityEntry AddSample(ActivityLog log, string text = "hello") =>
        log.Add(ActivityTrigger.Manual, new WindowSource("Notepad", "Untitled"), text);

    [Fact]
    public void Add_returns_a_pending_entry_with_monotonic_ids()
    {
        var log = new ActivityLog();

        ActivityEntry first = AddSample(log);
        ActivityEntry second = AddSample(log);

        Assert.Equal(ActivityState.Pending, first.State);
        Assert.Equal(1, first.Id);
        Assert.Equal(2, second.Id);
    }

    [Fact]
    public void Add_raises_EntryAdded()
    {
        var log = new ActivityLog();
        ActivityEntry? raised = null;
        log.EntryAdded += (_, e) => raised = e;

        ActivityEntry added = AddSample(log);

        Assert.Same(added, raised);
    }

    [Fact]
    public void SetState_changes_state_and_raises_EntryChanged()
    {
        var log = new ActivityLog();
        ActivityEntry entry = AddSample(log);
        int changes = 0;
        log.EntryChanged += (_, _) => changes++;

        log.SetState(entry, ActivityState.Reading);

        Assert.Equal(ActivityState.Reading, entry.State);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void SetState_is_a_noop_when_state_and_reason_are_unchanged()
    {
        var log = new ActivityLog();
        ActivityEntry entry = AddSample(log);
        log.SetState(entry, ActivityState.Reading);
        int changes = 0;
        log.EntryChanged += (_, _) => changes++;

        log.SetState(entry, ActivityState.Reading);

        Assert.Equal(0, changes);
    }

    [Fact]
    public void Entries_are_capped_at_200_dropping_the_oldest()
    {
        var log = new ActivityLog();
        for (int i = 0; i < 250; i++)
        {
            AddSample(log, $"text {i}");
        }

        IReadOnlyList<ActivityEntry> entries = log.Entries;
        Assert.Equal(200, entries.Count);
        // Oldest (id 1..50) dropped; the window is the last 200 ids.
        Assert.Equal(51, entries[0].Id);
        Assert.Equal(250, entries[^1].Id);
    }

    [Fact]
    public void Long_text_is_truncated_to_500_characters()
    {
        var log = new ActivityLog();
        ActivityEntry entry = AddSample(log, new string('x', 900));

        Assert.Equal(500, entry.Text.Length);
    }
}
