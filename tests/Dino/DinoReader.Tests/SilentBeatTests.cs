using DinoReader.Core;
using Xunit;

namespace DinoReader.Tests;

/// <summary>Slice 278 part B: one clock for the silent piece's beat. Every order of begin, hide, show, pause, resume and end leaves the timer running exactly when it should.</summary>
public class SilentBeatTests
{
    public enum Event
    {
        Begin,
        Hide,
        Show,
        Pause,
        Resume,
        End,
    }

    private static readonly Event[] AllEvents = Enum.GetValues<Event>();

    // The same rules, written as the simplest model: three flags, and the timer runs when the beat exists and neither of the other two holds it.
    private sealed class Model
    {
        public bool Active;
        public bool Hidden;
        public bool Held;

        public bool Running => Active && !Hidden && !Held;

        public void Apply(Event e)
        {
            switch (e)
            {
                case Event.Begin: Active = true; Held = false; break;
                case Event.Hide: Hidden = true; break;
                case Event.Show: Hidden = false; break;
                case Event.Pause: Held |= Active; break;
                case Event.Resume: Held = false; break;
                case Event.End: Active = false; Held = false; break;
            }
        }
    }

    private static bool Apply(SilentBeat beat, Event e) => e switch
    {
        Event.Begin => beat.Begin(),
        Event.Hide => beat.SetHidden(true),
        Event.Show => beat.SetHidden(false),
        Event.Pause => beat.Pause(),
        Event.Resume => beat.Resume(),
        _ => EndBeat(beat),
    };

    private static bool EndBeat(SilentBeat beat)
    {
        beat.End();
        return false;
    }

    public static IEnumerable<object[]> Sequences()
    {
        foreach (Event a in AllEvents)
        {
            foreach (Event b in AllEvents)
            {
                yield return [new[] { a, b }];
                foreach (Event c in AllEvents)
                {
                    yield return [new[] { a, b, c }];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sequences))]
    public void The_timer_runs_exactly_when_the_beat_is_active_shown_and_not_held_in_every_order(Event[] events)
    {
        var beat = new SilentBeat();
        var model = new Model();
        bool timerRunning = false;
        foreach (Event e in events)
        {
            bool wasRunning = beat.Running;
            bool result = Apply(beat, e);
            model.Apply(e);
            bool stops = e is Event.Pause or Event.Hide; // these return "stop the timer"; Begin, Resume and Show return "start it"
            if (stops)
            {
                Assert.Equal(wasRunning, result);
            }

            if (wasRunning && !beat.Running)
            {
                timerRunning = false; // the owner stops the timer on every true-to-false change
            }

            if (!stops && result)
            {
                Assert.False(wasRunning && e != Event.Begin, "a timer already running is never started a second time");
                timerRunning = true;
            }

            Assert.Equal(model.Running, beat.Running);
            Assert.Equal(model.Running, timerRunning);
            Assert.Equal(model.Held, beat.HeldByPerson);
        }
    }

    [Fact]
    public void A_person_pause_survives_hide_then_show()
    {
        var beat = new SilentBeat();
        beat.Begin();
        beat.Pause();

        beat.SetHidden(true);

        Assert.False(beat.SetHidden(false));
        Assert.True(beat.HeldByPerson);
        Assert.False(beat.Running);
        Assert.True(beat.Resume());
    }

    [Fact]
    public void Begin_under_a_covered_page_waits_and_show_starts_a_whole_beat()
    {
        var beat = new SilentBeat();
        beat.SetHidden(true);

        Assert.False(beat.Begin());
        Assert.True(beat.Active);
        Assert.True(beat.SetHidden(false));
    }

    [Fact]
    public void Resume_while_covered_waits_for_show()
    {
        var beat = new SilentBeat();
        beat.Begin();
        beat.Pause();
        beat.SetHidden(true);

        Assert.False(beat.Resume());
        Assert.True(beat.SetHidden(false));
    }

    [Fact]
    public void Hide_stops_a_running_timer_and_show_starts_it_again()
    {
        var beat = new SilentBeat();
        Assert.True(beat.Begin());

        Assert.True(beat.SetHidden(true));
        Assert.False(beat.SetHidden(true));
        Assert.True(beat.SetHidden(false));
        Assert.False(beat.SetHidden(false));
    }

    [Fact]
    public void Setting_the_same_cover_twice_does_nothing_the_second_time()
    {
        var beat = new SilentBeat();
        beat.Begin();

        Assert.True(beat.SetHidden(true));
        Assert.False(beat.SetHidden(true));
        Assert.True(beat.SetHidden(false));
        Assert.False(beat.SetHidden(false));
    }

    [Fact]
    public void Showing_does_not_release_a_pause_the_person_made()
    {
        var beat = new SilentBeat();
        beat.Begin();
        beat.Pause();
        beat.SetHidden(true);

        Assert.False(beat.SetHidden(false));
        Assert.True(beat.HeldByPerson);
        Assert.True(beat.Resume());
    }

    [Fact]
    public void Pause_with_no_beat_holds_nothing()
    {
        var beat = new SilentBeat();

        Assert.False(beat.Pause());
        Assert.False(beat.HeldByPerson);
        Assert.False(beat.Resume());
    }

    [Fact]
    public void End_clears_the_beat_and_the_hold_but_not_the_cover()
    {
        var beat = new SilentBeat();
        beat.Begin();
        beat.Pause();
        beat.SetHidden(true);

        beat.End();

        Assert.False(beat.Active);
        Assert.False(beat.HeldByPerson);
        Assert.True(beat.Hidden);
        Assert.False(beat.Begin());
    }

    [Fact]
    public void A_new_piece_after_a_held_one_starts_free()
    {
        var beat = new SilentBeat();
        beat.Begin();
        beat.Pause();
        beat.End();

        Assert.True(beat.Begin());
    }
}
