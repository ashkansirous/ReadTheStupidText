#if DEBUG
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

// Slice 320 item 7: the Debug-only stall-read hook holds one chunk wait so the stall overlay can be reached on a device.
public class StallHookTests
{
    [Fact]
    public async Task An_armed_hook_holds_a_ready_chunk_until_the_read_is_stopped()
    {
        var markers = new List<string>();
        var hook = new StallHook { Log = markers.Add };
        var reporter = new StallReporter(this) { Hook = hook };
        var raised = new List<bool>();
        reporter.Changed += (_, stalled) => raised.Add(stalled);
        using var stop = new CancellationTokenSource();

        hook.Arm();
        Task<string> waiting = reporter.WatchAsync(Task.FromResult("audio"), stop.Token, firstChunk: true);

        Assert.False(waiting.IsCompleted);
        Assert.True(reporter.IsStalled);

        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);

        Assert.Equal([true, false], raised);
        Assert.False(reporter.IsStalled);
        Assert.Equal([StallHook.BeginsMarker, StallHook.ReleasedMarker], markers);
    }

    [Fact]
    public void A_request_is_taken_once()
    {
        var hook = new StallHook();
        hook.Arm();

        Assert.True(hook.Take());
        Assert.False(hook.Take());
    }

    [Fact]
    public async Task The_hold_ends_by_itself_when_its_time_runs_out()
    {
        var markers = new List<string>();
        var hook = new StallHook { HoldFor = TimeSpan.FromMilliseconds(50), Log = markers.Add };
        var reporter = new StallReporter(this) { Hook = hook };

        hook.Arm();
        string value = await reporter.WatchAsync(Task.FromResult("audio"), CancellationToken.None, firstChunk: true);

        Assert.Equal("audio", value);
        Assert.False(reporter.IsStalled);
        Assert.Equal([StallHook.BeginsMarker, StallHook.ReleasedMarker], markers);
    }

    [Fact]
    public async Task A_request_is_used_up_by_the_first_chunk_wait_and_the_next_one_is_not_held()
    {
        var hook = new StallHook { HoldFor = TimeSpan.FromMilliseconds(50) };
        var reporter = new StallReporter(this) { Hook = hook };

        hook.Arm();
        await reporter.WatchAsync(Task.FromResult("first"), CancellationToken.None, firstChunk: true);
        Task<string> second = reporter.WatchAsync(Task.FromResult("second"), CancellationToken.None, firstChunk: true);

        Assert.True(second.IsCompletedSuccessfully);
        Assert.False(reporter.IsStalled);
    }

    [Fact]
    public async Task A_later_chunk_wait_of_another_read_does_not_use_up_the_request()
    {
        var hook = new StallHook { HoldFor = TimeSpan.FromMilliseconds(50) };
        var reporter = new StallReporter(this) { Hook = hook };

        hook.Arm();
        Task<string> laterChunk = reporter.WatchAsync(Task.FromResult("chunk 3"), CancellationToken.None);

        Assert.True(laterChunk.IsCompletedSuccessfully);
        Assert.False(reporter.IsStalled);

        Task<string> firstChunkOfTheRead = reporter.WatchAsync(Task.FromResult("chunk 0"), CancellationToken.None, firstChunk: true);

        Assert.False(firstChunkOfTheRead.IsCompleted);
        Assert.True(reporter.IsStalled);
        Assert.Equal("chunk 0", await firstChunkOfTheRead);
    }

    [Fact]
    public async Task Without_a_request_a_ready_chunk_is_not_held()
    {
        var reporter = new StallReporter(this) { Hook = new StallHook() };

        string value = await reporter.WatchAsync(Task.FromResult("audio"), CancellationToken.None);

        Assert.Equal("audio", value);
        Assert.False(reporter.IsStalled);
    }
}
#endif
