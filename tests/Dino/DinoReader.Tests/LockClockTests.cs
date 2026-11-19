using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 418 item 1: the lock's 30-second clock.</summary>
public class LockClockTests
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private static LockClock Unlocked()
    {
        var clock = new LockClock();
        clock.Unlock();
        return clock;
    }

    [Fact]
    public void It_starts_locked()
    {
        var clock = new LockClock();

        Assert.True(clock.Locked);
        Assert.Equal(0, clock.SecondsLeft);
    }

    [Fact]
    public void Unlock_starts_at_thirty_seconds()
    {
        Assert.Equal(30, Unlocked().SecondsLeft);
    }

    [Fact]
    public void Ticks_count_down_and_lock_at_zero_with_the_timer_reason()
    {
        var clock = Unlocked();

        for (int i = 0; i < 29; i++)
        {
            Assert.False(clock.Tick(Second));
        }

        Assert.Equal(1, clock.SecondsLeft);
        Assert.True(clock.Tick(Second));
        Assert.True(clock.Locked);
        Assert.Equal(LockReason.Timer, clock.LastReason);
    }

    [Fact]
    public void A_grown_up_tap_resets_to_thirty()
    {
        var clock = Unlocked();
        clock.Tick(TimeSpan.FromSeconds(20));

        clock.GrownUpTap();

        Assert.Equal(30, clock.SecondsLeft);
    }

    [Fact]
    public void Locking_by_hand_records_the_by_hand_reason()
    {
        var clock = Unlocked();

        clock.Lock(LockReason.ByHand);

        Assert.True(clock.Locked);
        Assert.Equal(LockReason.ByHand, clock.LastReason);
    }

    [Fact]
    public void Ticks_while_held_do_not_count()
    {
        var clock = Unlocked();
        clock.Tick(TimeSpan.FromSeconds(10));
        clock.Hold();

        clock.Tick(TimeSpan.FromSeconds(100));

        Assert.False(clock.Locked);
        Assert.Equal(20, clock.SecondsLeft);
    }

    [Fact]
    public void A_hold_opened_while_locked_does_not_survive_the_unlock()
    {
        var clock = new LockClock();
        int token = clock.Hold(); // an alert on the locked shelf

        clock.Unlock();

        Assert.False(clock.IsHeld);
        Assert.True(clock.Tick(TimeSpan.FromSeconds(30)));
        clock.Release(token); // a late release from before the unlock is ignored
        Assert.False(clock.IsHeld);
    }

    [Fact]
    public void Locking_while_already_locked_ends_open_holds()
    {
        var clock = new LockClock();
        clock.Hold();

        clock.Lock(LockReason.ByHand);

        Assert.False(clock.IsHeld);
    }

    [Theory]
    [InlineData(UiScreens.CornerStore, false, true)]
    [InlineData(UiScreens.CornerSettings, false, true)]
    [InlineData(UiScreens.Backup, false, true)]
    [InlineData(UiScreens.CornerBooks, false, false)] // arrows, Esc or Enter on the shelf
    [InlineData(UiScreens.CornerBooks, true, true)] // a ••• sheet or the reorder mode is open
    [InlineData(UiScreens.Reading, false, false)]
    [InlineData(UiScreens.Reading, true, false)]
    public void Only_keys_inside_a_grown_up_screen_reset_the_timer(string screen, bool sheetOrReorderOpen, bool expected) =>
        Assert.Equal(expected, LockTapRules.IsGrownUpKey(screen, sheetOrReorderOpen));

    [Fact]
    public void Holds_are_counted_and_release_restarts_the_full_time()
    {
        var clock = Unlocked();
        int first = clock.Hold();
        int second = clock.Hold();
        clock.Release(first);

        Assert.True(clock.IsHeld);
        clock.Release(second);
        Assert.False(clock.IsHeld);
        Assert.Equal(30, clock.SecondsLeft);
    }

    [Fact]
    public void An_extra_release_never_goes_below_zero()
    {
        var clock = Unlocked();

        clock.Release(0);
        clock.Hold();

        Assert.True(clock.IsHeld);
    }

    [Fact]
    public void A_release_from_before_a_lock_cannot_end_a_newer_hold()
    {
        var clock = Unlocked();
        int purchase = clock.Hold();
        clock.Lock(LockReason.ByHand); // the app left the foreground: every open hold ends
        clock.Unlock();
        int recording = clock.Hold();

        clock.Release(purchase); // the first purchase call returns late

        Assert.True(clock.IsHeld);
        Assert.False(clock.Tick(TimeSpan.FromSeconds(100)));
        clock.Release(recording);
        Assert.False(clock.IsHeld);
    }

    [Fact]
    public void A_lock_ends_every_open_hold()
    {
        var clock = Unlocked();
        clock.Hold();
        clock.Hold();

        clock.Lock(LockReason.ByHand);
        clock.Unlock();

        Assert.False(clock.IsHeld);
    }

    [Theory]
    [InlineData(UiScreens.CornerStore, true)]
    [InlineData(UiScreens.CornerSettings, true)]
    [InlineData(UiScreens.Backup, true)]
    [InlineData(UiScreens.Shelf, false)]
    [InlineData(UiScreens.Reading, false)]
    public void Any_press_toggle_drag_or_key_on_a_grown_up_screen_counts(string screen, bool expected) =>
        Assert.Equal(expected, LockTapRules.IsGrownUpScreen(screen));

    [Fact]
    public void A_tap_while_locked_does_nothing()
    {
        var clock = new LockClock();

        clock.GrownUpTap();

        Assert.True(clock.Locked);
        Assert.Equal(0, clock.SecondsLeft);
    }

    [Theory]
    [InlineData(UiScreens.CornerStore, UiControls.Download, true)]
    [InlineData(UiScreens.CornerSettings, UiControls.SettingsRow, true)]
    [InlineData(UiScreens.CornerBooks, UiControls.BookMore, true)] // the ••• menu
    [InlineData(UiScreens.Backup, UiControls.Save, true)] // inside a sub-page
    [InlineData(UiScreens.CornerBooks, UiControls.Book, false)] // a cover reads
    [InlineData(UiScreens.CornerBooks, UiControls.Language, false)] // the reading-language button
    [InlineData(UiScreens.Reading, UiControls.NextPage, false)]
    [InlineData(UiScreens.Reading, UiControls.Language, false)]
    [InlineData(UiScreens.Shelf, UiControls.Book, false)]
    [InlineData(UiScreens.Language, UiControls.Language, true)] // the Settings language page is a grown-up place
    public void Only_taps_inside_the_grown_up_places_reset_the_timer(string screen, string control, bool expected) =>
        Assert.Equal(expected, LockTapRules.IsGrownUpTap(screen, control));

    [Fact]
    public void A_shorter_total_is_used_by_the_next_unlock()
    {
        var clock = new LockClock();
        clock.SetTotalSeconds(3);
        clock.Unlock();

        Assert.Equal(3, clock.SecondsLeft);
        clock.Tick(Second);
        clock.Tick(Second);
        Assert.True(clock.Tick(Second));
    }
}
