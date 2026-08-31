using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Infrastructure.Reading;

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
}
