using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class StallReporterTests
{
    [Fact]
    public async Task A_chunk_that_is_ready_raises_nothing()
    {
        var reporter = new StallReporter(this);
        var raised = new List<bool>();
        reporter.Changed += (_, stalled) => raised.Add(stalled);

        string value = await reporter.WatchAsync(Task.FromResult("audio"), CancellationToken.None);

        Assert.Equal("audio", value);
        Assert.Empty(raised);
    }

    [Fact]
    public async Task A_chunk_that_is_late_stalls_until_it_arrives()
    {
        var reporter = new StallReporter(this);
        var raised = new List<bool>();
        var arrival = new TaskCompletionSource<string>();
        reporter.Changed += (_, stalled) => raised.Add(stalled);

        Task<string> waiting = reporter.WatchAsync(arrival.Task, CancellationToken.None);
        Assert.True(reporter.IsStalled);
        arrival.SetResult("audio");

        Assert.Equal("audio", await waiting);
        Assert.Equal([true, false], raised);
        Assert.False(reporter.IsStalled);
    }

    [Fact]
    public async Task An_earlier_read_ending_late_cannot_clear_the_stall_of_the_read_that_replaced_it()
    {
        var reporter = new StallReporter(this);
        var raised = new List<bool>();
        var first = new TaskCompletionSource<string>();
        var second = new TaskCompletionSource<string>();
        reporter.Changed += (_, stalled) => raised.Add(stalled);

        Task<string> oldRead = reporter.WatchAsync(first.Task, CancellationToken.None);
        Task<string> newRead = reporter.WatchAsync(second.Task, CancellationToken.None);
        first.SetResult("old");
        await oldRead;

        Assert.True(reporter.IsStalled);
        second.SetResult("new");
        await newRead;
        Assert.Equal([true, false], raised);
        Assert.False(reporter.IsStalled);
    }

    [Fact]
    public async Task A_newer_watch_finishing_first_leaves_the_stall_until_the_older_chunk_arrives()
    {
        var reporter = new StallReporter(this);
        var raised = new List<bool>();
        var first = new TaskCompletionSource<string>();
        var second = new TaskCompletionSource<string>();
        reporter.Changed += (_, stalled) => raised.Add(stalled);

        Task<string> oldRead = reporter.WatchAsync(first.Task, CancellationToken.None);
        Task<string> newRead = reporter.WatchAsync(second.Task, CancellationToken.None);
        second.SetResult("new");
        await newRead;

        Assert.True(reporter.IsStalled);
        first.SetResult("old");
        await oldRead;
        Assert.Equal([true, false], raised);
    }

    [Fact]
    public async Task A_cancelled_wait_ends_the_stall()
    {
        var reporter = new StallReporter(this);
        var raised = new List<bool>();
        var arrival = new TaskCompletionSource<string>();
        reporter.Changed += (_, stalled) => raised.Add(stalled);

        Task<string> waiting = reporter.WatchAsync(arrival.Task, CancellationToken.None);
        arrival.SetCanceled();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Equal([true, false], raised);
        Assert.False(reporter.IsStalled);
    }

    // Slice 314: a read that fails ends the wait too, so a page coming back never restores an overlay for a read that is gone.
    [Fact]
    public async Task A_failed_wait_ends_the_stall()
    {
        var reporter = new StallReporter(this);
        var arrival = new TaskCompletionSource<string>();

        Task<string> waiting = reporter.WatchAsync(arrival.Task, CancellationToken.None);
        Assert.True(reporter.IsStalled);
        arrival.SetException(new InvalidOperationException("synthesis failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => waiting);
        Assert.False(reporter.IsStalled);
    }

    // Slice 314: Stop ends the stall at once, though the native synthesis behind the task is still running.
    [Fact]
    public async Task Cancelling_the_token_ends_the_stall_while_the_task_is_still_pending()
    {
        var reporter = new StallReporter(this);
        var raised = new List<bool>();
        var arrival = new TaskCompletionSource<string>();
        using var cts = new CancellationTokenSource();
        reporter.Changed += (_, stalled) => raised.Add(stalled);

        Task<string> waiting = reporter.WatchAsync(arrival.Task, cts.Token);
        cts.Cancel();

        Assert.False(reporter.IsStalled);
        Assert.Equal([true, false], raised);
        arrival.SetResult("audio");
        Assert.Equal("audio", await waiting);
        Assert.Equal([true, false], raised);
    }

    [Fact]
    public async Task A_cancelled_watch_does_not_clear_the_stall_of_an_overlapping_watch()
    {
        var reporter = new StallReporter(this);
        var raised = new List<bool>();
        var first = new TaskCompletionSource<string>();
        var second = new TaskCompletionSource<string>();
        using var cts = new CancellationTokenSource();
        reporter.Changed += (_, stalled) => raised.Add(stalled);

        Task<string> oldRead = reporter.WatchAsync(first.Task, cts.Token);
        Task<string> newRead = reporter.WatchAsync(second.Task, CancellationToken.None);
        cts.Cancel();
        first.SetResult("old");
        await oldRead;

        Assert.True(reporter.IsStalled);
        second.SetResult("new");
        await newRead;
        Assert.Equal([true, false], raised);
    }
}
