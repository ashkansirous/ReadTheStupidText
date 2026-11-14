#if DEBUG
using Sirous.Reading.Application.Reading;

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
        Task<string> waiting = reporter.WatchAsync(Task.FromResult("audio"), stop.Token, holdable: true);

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
        string value = await reporter.WatchAsync(Task.FromResult("audio"), CancellationToken.None, holdable: true);

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
        await reporter.WatchAsync(Task.FromResult("first"), CancellationToken.None, holdable: true);
        Task<string> second = reporter.WatchAsync(Task.FromResult("second"), CancellationToken.None, holdable: true);

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

        Task<string> holdableChunkOfTheRead = reporter.WatchAsync(Task.FromResult("chunk 1"), CancellationToken.None, holdable: true);

        Assert.False(holdableChunkOfTheRead.IsCompleted);
        Assert.True(reporter.IsStalled);
        Assert.Equal("chunk 1", await holdableChunkOfTheRead);
    }

    [Fact]
    public void A_read_played_to_its_end_without_the_holdable_chunk_clears_the_request_it_claimed()
    {
        var hook = new StallHook();
        var reporter = new StallReporter(this) { Hook = hook };

        hook.Arm();
        long claim = reporter.ClaimArmedRequest();
        reporter.ReadPlayedToEnd(claim);

        Assert.False(hook.Take());
    }

    [Fact]
    public void A_fresh_request_survives_an_earlier_reads_natural_end()
    {
        var hook = new StallHook();
        var reporter = new StallReporter(this) { Hook = hook };

        hook.Arm();
        long oldReadClaim = reporter.ClaimArmedRequest();
        hook.Arm(); // armed for the next read while the old one is still finishing
        reporter.ReadPlayedToEnd(oldReadClaim);

        Assert.True(hook.Take());
    }

    [Fact]
    public void A_read_that_started_with_nothing_armed_cannot_clear_a_later_request()
    {
        var hook = new StallHook();
        var reporter = new StallReporter(this) { Hook = hook };

        long claim = reporter.ClaimArmedRequest();
        hook.Arm();
        reporter.ReadPlayedToEnd(claim);

        Assert.True(hook.Take());
    }

    [Fact]
    public void Only_the_second_chunk_of_a_read_is_holdable()
    {
        Assert.False(StallReporter.IsHoldable(0));
        Assert.True(StallReporter.IsHoldable(1));
        Assert.False(StallReporter.IsHoldable(2));
    }

    [Fact]
    public void The_flows_three_sentence_text_has_a_holdable_chunk_under_the_readers_planner_and_head_policies()
    {
        const string text = "The quick brown fox jumps over the lazy dog today while the sun shines on the quiet green hill. Then the fox walks slowly home through the tall grass, and it curls up by the warm fire to sleep until the morning comes again. The end.";
        string hint = "The quick brown fox jumps over the lazy dog today while the sun shines on the quiet green hill.";
        SpeechTextChunker.HeadPolicy[] policies =
        [
            SpeechTextChunker.HeadPolicy.Cached, // Windows reader
            SpeechTextChunker.HeadPolicy.Cached.WithShortFirstPiece(), // Android and iOS readers
        ];

        foreach (SpeechTextChunker.HeadPolicy policy in policies)
        {
            foreach (string? firstChunkHint in new string?[] { null, hint })
            {
                int count = SpeechChunkPlanner.ForText(text, policy, firstChunkHint: firstChunkHint).Count;

                Assert.True(count > StallReporter.HoldableChunkIndex, $"{count} chunks for {policy} with hint {firstChunkHint ?? "none"}");
            }
        }
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
