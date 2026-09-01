using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class ReadTimingTrackerTests
{
    [Fact]
    public void Fresh_start_reports_zero_elapsed_and_unknown_total()
    {
        var tracker = new ReadTimingTracker();

        tracker.Start(chunkCount: 2);

        ReadTiming timing = tracker.CurrentTiming(TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, timing.Elapsed);
        Assert.Null(timing.Total);
    }

    [Fact]
    public void Elapsed_tracks_position_within_the_first_chunk_before_total_is_known()
    {
        var tracker = new ReadTimingTracker();
        tracker.Start(chunkCount: 2);
        tracker.RecordChunkDuration(0, TimeSpan.FromSeconds(60));

        ReadTiming timing = tracker.CurrentTiming(TimeSpan.FromSeconds(3));

        Assert.Equal(TimeSpan.FromSeconds(3), timing.Elapsed);
        Assert.Null(timing.Total); // chunk 1's duration hasn't landed yet
    }

    [Fact]
    public void Total_becomes_known_the_instant_the_last_chunk_duration_lands()
    {
        // The user's own worked example: 00:00/--:-- -> ticks -> 00:03/02:23 the
        // instant the last chunk lands (even though only 3s has actually played).
        var tracker = new ReadTimingTracker();
        tracker.Start(chunkCount: 2);
        tracker.RecordChunkDuration(0, TimeSpan.FromSeconds(60));
        tracker.RecordChunkDuration(1, TimeSpan.FromSeconds(83));

        ReadTiming timing = tracker.CurrentTiming(TimeSpan.FromSeconds(3));

        Assert.Equal(TimeSpan.FromSeconds(3), timing.Elapsed);
        Assert.Equal(TimeSpan.FromSeconds(143), timing.Total);
    }

    [Fact]
    public void Advancing_past_a_finished_chunk_folds_its_duration_into_elapsed()
    {
        var tracker = new ReadTimingTracker();
        tracker.Start(chunkCount: 2);
        tracker.RecordChunkDuration(0, TimeSpan.FromSeconds(60));
        tracker.RecordChunkDuration(1, TimeSpan.FromSeconds(83));

        tracker.AdvancePastChunk(0);
        ReadTiming timing = tracker.CurrentTiming(TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(65), timing.Elapsed);
        Assert.Equal(TimeSpan.FromSeconds(143), timing.Total);
    }

    [Fact]
    public void Reset_returns_to_zero_elapsed_and_unknown_total()
    {
        var tracker = new ReadTimingTracker();
        tracker.Start(chunkCount: 1);
        tracker.RecordChunkDuration(0, TimeSpan.FromSeconds(10));
        tracker.AdvancePastChunk(0);

        tracker.Reset();

        ReadTiming timing = tracker.CurrentTiming(TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, timing.Elapsed);
        Assert.Null(timing.Total);
    }

    [Fact]
    public void ComputeSkipTarget_returns_null_when_no_read_is_in_progress()
    {
        var tracker = new ReadTimingTracker();
        tracker.Start(chunkCount: 0);

        SkipTarget? target = tracker.ComputeSkipTarget(TimeSpan.Zero, TimeSpan.FromSeconds(10));

        Assert.Null(target);
    }

    [Fact]
    public void ComputeSkipTarget_forward_snaps_to_the_next_known_chunk_boundary()
    {
        // Three 60s chunks; 3s into chunk 0, +10s (13s) is still inside chunk 0's
        // 60s span, so the nearest reachable boundary at/after it is chunk 1's start.
        var tracker = new ReadTimingTracker();
        tracker.Start(chunkCount: 3);
        tracker.RecordChunkDuration(0, TimeSpan.FromSeconds(60));
        tracker.RecordChunkDuration(1, TimeSpan.FromSeconds(60));
        tracker.RecordChunkDuration(2, TimeSpan.FromSeconds(60));

        SkipTarget? target = tracker.ComputeSkipTarget(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10));

        Assert.Equal(new SkipTarget(1, TimeSpan.FromSeconds(60)), target);
    }

    [Fact]
    public void ComputeSkipTarget_forward_clamps_to_the_furthest_synthesized_boundary()
    {
        // Only chunk 0 has synthesized; nothing beyond its end (60s) is reachable yet,
        // even though the read has 3 chunks in total.
        var tracker = new ReadTimingTracker();
        tracker.Start(chunkCount: 3);
        tracker.RecordChunkDuration(0, TimeSpan.FromSeconds(60));

        SkipTarget? target = tracker.ComputeSkipTarget(TimeSpan.FromSeconds(55), TimeSpan.FromSeconds(10));

        Assert.Equal(new SkipTarget(1, TimeSpan.FromSeconds(60)), target);
    }

    [Fact]
    public void ComputeSkipTarget_backward_snaps_to_the_previous_known_chunk_boundary()
    {
        // 65s in is 5s into chunk 1 (which starts at 60s); -10s (55s) falls back
        // inside chunk 0's span, so the nearest reachable boundary at/before it is
        // chunk 0's own start.
        var tracker = new ReadTimingTracker();
        tracker.Start(chunkCount: 2);
        tracker.RecordChunkDuration(0, TimeSpan.FromSeconds(60));
        tracker.RecordChunkDuration(1, TimeSpan.FromSeconds(83));

        SkipTarget? target = tracker.ComputeSkipTarget(TimeSpan.FromSeconds(65), TimeSpan.FromSeconds(-10));

        Assert.Equal(new SkipTarget(0, TimeSpan.Zero), target);
    }

    [Fact]
    public void ComputeSkipTarget_backward_clamps_to_zero()
    {
        var tracker = new ReadTimingTracker();
        tracker.Start(chunkCount: 2);
        tracker.RecordChunkDuration(0, TimeSpan.FromSeconds(60));
        tracker.RecordChunkDuration(1, TimeSpan.FromSeconds(83));

        SkipTarget? target = tracker.ComputeSkipTarget(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(-10));

        Assert.Equal(new SkipTarget(0, TimeSpan.Zero), target);
    }
}
