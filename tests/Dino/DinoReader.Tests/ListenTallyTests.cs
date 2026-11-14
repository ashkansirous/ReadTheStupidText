using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 288: heard seconds per reading, the 30 second listen rule, and the count's JSON shape.</summary>
public class ListenTallyTests
{
    private const long Second = 1000;

    private static readonly DateTimeOffset Now = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    private static (ListenTally Tally, Func<long, long> Advance) NewTally()
    {
        long clock = 1_000_000;
        var tally = new ListenTally(() => clock);
        return (tally, ms => clock += ms);
    }

    [Fact]
    public void A_scripted_reading_gives_35_then_10_seconds_and_one_listen()
    {
        var (tally, advance) = NewTally();

        tally.Start("a");
        tally.Resume();
        advance(35 * Second);
        Assert.True(tally.TryMarkListened());
        tally.Pause();
        int? firstPause = tally.TakeSeconds();
        advance(20 * Second); // paused: not counted
        tally.Resume();
        advance(10 * Second);
        int? stop = tally.TakeSeconds();
        tally.Stop();

        Assert.Equal(35, firstPause);
        Assert.Equal(10, stop);
        Assert.False(tally.TryMarkListened()); // the reading is over
    }

    [Fact]
    public void Time_before_audio_is_heard_is_not_counted()
    {
        var (tally, advance) = NewTally();

        tally.Start("a");
        advance(45 * Second); // loading, or a menu: nothing is playing

        Assert.False(tally.TryMarkListened());
        Assert.Null(tally.MillisecondsUntilListen());
        Assert.Equal(0, tally.TakeSeconds());
    }

    [Fact]
    public void Under_30_seconds_is_not_a_listen()
    {
        var (tally, advance) = NewTally();

        tally.Start("a");
        tally.Resume();
        advance(29_999);

        Assert.False(tally.TryMarkListened());
        Assert.Equal(1, tally.MillisecondsUntilListen());
    }

    [Fact]
    public void Exactly_30_seconds_is_a_listen_and_only_once()
    {
        var (tally, advance) = NewTally();

        tally.Start("a");
        tally.Resume();
        advance(30 * Second);

        Assert.True(tally.TryMarkListened());
        Assert.False(tally.TryMarkListened());
        Assert.True(tally.Listened);
        Assert.Null(tally.MillisecondsUntilListen());
    }

    [Fact]
    public void A_listen_that_crosses_30_seconds_at_the_pause_is_still_marked_once()
    {
        var (tally, advance) = NewTally();

        tally.Start("a");
        tally.Resume();
        advance(30 * Second + 20); // the timer has not fired yet
        tally.Pause();

        Assert.True(tally.TryMarkListened());
        Assert.False(tally.TryMarkListened());
    }

    [Fact]
    public void Played_time_across_a_pause_adds_up_to_a_listen_and_a_pause_stops_the_clock()
    {
        var (tally, advance) = NewTally();

        tally.Start("a");
        tally.Resume();
        advance(20 * Second);
        tally.Pause();
        advance(60 * Second);
        Assert.False(tally.TryMarkListened());
        Assert.Null(tally.MillisecondsUntilListen()); // not playing: nothing to wait for
        tally.Resume();

        Assert.Equal(10 * Second, tally.MillisecondsUntilListen());
        advance(10 * Second);
        Assert.True(tally.TryMarkListened());
    }

    [Fact]
    public void A_restart_is_a_new_reading_with_its_own_total_and_its_own_listen()
    {
        var (tally, advance) = NewTally();
        tally.Start("a");
        tally.Resume();
        advance(40 * Second);
        Assert.True(tally.TryMarkListened());
        Assert.Equal(40, tally.TakeSeconds());
        tally.Stop();

        tally.Start("a");
        tally.Resume();
        advance(5 * Second);

        Assert.False(tally.Listened);
        Assert.False(tally.TryMarkListened());
        advance(25 * Second);
        Assert.True(tally.TryMarkListened());
        Assert.Equal(30, tally.TakeSeconds());
    }

    [Fact]
    public void No_open_reading_has_no_seconds_and_a_paused_one_has_none_to_add()
    {
        var (tally, advance) = NewTally();
        Assert.Null(tally.TakeSeconds());

        tally.Start("a");
        tally.Resume();
        advance(7 * Second);
        tally.Pause();
        Assert.Equal(7, tally.TakeSeconds());
        advance(9 * Second);

        Assert.Equal(0, tally.TakeSeconds());
        tally.Stop();
        Assert.Null(tally.TakeSeconds());
    }

    [Fact]
    public void A_reading_that_starts_while_audio_is_already_heard_runs_at_once_and_a_stop_does_not_clear_it()
    {
        var (tally, advance) = NewTally();

        tally.Start("a");
        tally.SetAudible(true);
        advance(6 * Second);
        Assert.Equal(6, tally.TakeSeconds());
        tally.Stop();
        Assert.True(tally.Audible); // a stop is a usage verb: only the audio output changes this

        tally.Start("a"); // the next reading, no false-to-true flip in between
        advance(3 * Second);
        Assert.Equal(3, tally.TakeSeconds());
    }

    [Fact]
    public void Audio_heard_with_no_reading_open_is_ignored_so_a_demo_read_adds_nothing()
    {
        var (tally, advance) = NewTally();

        tally.SetAudible(true); // the intro's demo read: no counted reading is open
        Assert.False(tally.Audible);
        Assert.Null(tally.TakeSeconds());

        tally.Start("a"); // a real reading begins later: nothing runs until its own audio is heard
        advance(9 * Second);
        Assert.Equal(0, tally.TakeSeconds());
        Assert.Null(tally.MillisecondsUntilListen());
    }

    [Fact]
    public void A_language_switch_start_for_the_same_story_keeps_the_time_the_listen_and_does_not_reset()
    {
        var (tally, advance) = NewTally();
        tally.Start("a");
        tally.SetAudible(true);
        advance(20 * Second);
        tally.SetAudible(false); // the switch stops the audio; the reading stays open
        Assert.Equal(20, tally.TakeSeconds());

        Assert.False(tally.Start("a")); // the same story again: not a new reading
        tally.SetAudible(true);
        advance(10 * Second);

        Assert.True(tally.TryMarkListened()); // 20 s + 10 s in one reading
        Assert.Equal(10, tally.TakeSeconds());
    }

    [Fact]
    public void Audio_that_stops_while_no_reading_is_open_keeps_the_next_reading_paused()
    {
        var (tally, advance) = NewTally();
        tally.SetAudible(true);
        tally.SetAudible(false);

        tally.Start("a");
        advance(9 * Second);

        Assert.Equal(0, tally.TakeSeconds());
    }

    [Fact]
    public void A_start_while_a_reading_is_open_keeps_its_time_and_its_listen()
    {
        var (tally, advance) = NewTally();
        tally.Start("a");
        tally.SetAudible(true);
        advance(12 * Second);

        tally.Start("a"); // a stray second start: not a reset

        advance(18 * Second);
        Assert.True(tally.TryMarkListened());
        Assert.Equal(30, tally.TakeSeconds());
        tally.Start("a");
        Assert.False(tally.TryMarkListened()); // and not counted twice
    }

    [Fact]
    public void A_start_for_a_different_story_ends_the_open_reading_and_drops_its_unsent_seconds()
    {
        var (tally, advance) = NewTally();
        Assert.True(tally.Start("a"));
        tally.SetAudible(true);
        advance(40 * Second);
        Assert.True(tally.TryMarkListened());

        Assert.True(tally.Start("b")); // a new reading: A's 40 s are never billed to B
        advance(5 * Second);

        Assert.False(tally.Listened);
        Assert.Equal(5, tally.TakeSeconds());
        advance(25 * Second);
        Assert.True(tally.TryMarkListened()); // B's own listen, at its own 30 s
        Assert.False(tally.Start("b")); // the same story again changes nothing
    }

    [Fact]
    public void Take_seconds_gives_null_with_no_reading_so_a_pause_or_stop_row_sends_zero()
    {
        var (tally, advance) = NewTally();
        tally.SetAudible(true);
        advance(9 * Second);

        Assert.Null(tally.TakeSeconds()); // the counter turns this into seconds=0: those rows always carry seconds
        tally.Start("a");
        tally.Stop();
        Assert.Null(tally.TakeSeconds());
    }

    [Fact]
    public void A_resume_while_playing_or_with_no_reading_changes_nothing()
    {
        var (tally, advance) = NewTally();
        tally.Resume();
        Assert.False(tally.Active);

        tally.Start("a");
        tally.Resume();
        advance(4 * Second);
        tally.Resume(); // already playing: the segment is not restarted
        advance(4 * Second);

        Assert.Equal(8, tally.TakeSeconds());
    }

    [Fact]
    public void Seconds_are_rounded_once_so_many_short_segments_add_up()
    {
        var (tally, advance) = NewTally();
        tally.Start("a");
        int total = 0;

        for (int i = 0; i < 10; i++)
        {
            tally.Resume();
            advance(400); // a 0.4 s listen between pauses
            tally.Pause();
            total += tally.TakeSeconds() ?? 0;
        }

        Assert.Equal(4, total); // 4.0 s heard in all, not ten rows of 0
    }

    [Fact]
    public void The_count_carries_seconds_and_leaves_them_out_when_there_are_none()
    {
        var count = new UsageEvent(UsageEventName.ReadPaused, "own", "en", "own", false, "1.0", "android", "2026-11-07", 1);

        Assert.DoesNotContain("seconds", UsageBatch.ToJson([count]), StringComparison.Ordinal);
        Assert.Contains("\"seconds\":35", UsageBatch.ToJson([count with { Seconds = 35 }]), StringComparison.Ordinal);
        Assert.Equal(35, UsageBatch.ParseQueue(UsageBatch.QueueToJson([count with { Seconds = 35 }]))[0].Seconds);
    }

    [Fact]
    public void A_demo_read_counts_no_listen()
    {
        Assert.False(DemoReadCounting.Counts(UsageEventName.BookListened, true));
        Assert.True(DemoReadCounting.Counts(UsageEventName.BookListened, false));
        Assert.Equal("book_listened", UsageEventName.BookListened);
    }
}
