namespace DinoReader.Tests;

/// <summary>Slice 225 item 16: the Debug-only forced book update must leave no lowered version and no stale mark behind.</summary>
public class ForcedUpdateRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_mark_written_a_minute_ago_forces_the_apply()
    {
        Assert.True(ForcedUpdateRules.IsMarkLive(Now.AddMinutes(-1).UtcTicks, Now));
    }

    [Fact]
    public void A_mark_left_by_a_failed_stage_expires_and_cannot_force_a_later_ordinary_update()
    {
        long stale = (Now - ForcedUpdateRules.MarkLifetime - TimeSpan.FromSeconds(1)).UtcTicks;

        Assert.False(ForcedUpdateRules.IsMarkLive(stale, Now));
    }

    [Fact]
    public void A_mark_at_the_edge_of_its_lifetime_still_counts()
    {
        Assert.True(ForcedUpdateRules.IsMarkLive((Now - ForcedUpdateRules.MarkLifetime).UtcTicks, Now));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    public void No_mark_does_not_force(long ticks)
    {
        Assert.False(ForcedUpdateRules.IsMarkLive(ticks, Now));
    }

    [Fact]
    public void A_mark_from_the_future_does_not_force()
    {
        Assert.False(ForcedUpdateRules.IsMarkLive(Now.AddMinutes(5).UtcTicks, Now));
    }

    [Fact]
    public void A_dropped_forced_entry_gets_its_original_version_back()
    {
        var tracker = new ForcedVersionTracker();
        tracker.Lowered("good-enough", 7);

        Assert.True(tracker.TryTakeRestore("good-enough", out int? original));
        Assert.Equal(7, original);
    }

    [Fact]
    public void A_forced_book_that_had_no_version_is_restored_to_none()
    {
        var tracker = new ForcedVersionTracker();
        tracker.Lowered("good-enough", null);

        Assert.True(tracker.TryTakeRestore("good-enough", out int? original));
        Assert.Null(original);
    }

    [Fact]
    public void A_book_that_was_not_lowered_has_nothing_to_restore()
    {
        var tracker = new ForcedVersionTracker();

        Assert.False(tracker.TryTakeRestore("another-book", out _));
    }

    [Fact]
    public void A_restore_happens_once()
    {
        var tracker = new ForcedVersionTracker();
        tracker.Lowered("good-enough", 7);
        tracker.TryTakeRestore("good-enough", out _);

        Assert.False(tracker.TryTakeRestore("good-enough", out _));
    }

    [Fact]
    public void A_swapped_book_is_forgotten_so_a_later_run_does_not_restore_the_old_original()
    {
        var tracker = new ForcedVersionTracker();
        tracker.Lowered("good-enough", 7);
        tracker.Forget("good-enough");

        Assert.False(tracker.TryTakeRestore("good-enough", out _));
    }

    [Fact]
    public void Lowering_twice_keeps_the_first_original_not_the_lowered_value()
    {
        var tracker = new ForcedVersionTracker();
        tracker.Lowered("good-enough", 7);
        tracker.Lowered("good-enough", 6);

        tracker.TryTakeRestore("good-enough", out int? original);

        Assert.Equal(7, original);
    }
}
