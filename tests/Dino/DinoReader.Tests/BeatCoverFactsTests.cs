using DinoReader.Core;
using Xunit;

namespace DinoReader.Tests;

/// <summary>
/// Slice 322, second design: the page feeds every event into <see cref="BeatCoverFacts"/> and states the result to <see cref="SilentBeat.SetHidden"/>. Every order of up to four events (appear, disappear, stop, resume,
/// swap to a window that is in front, swap to a window that is already stopped) must leave the beat running exactly when the page is shown, its window is in front, and the beat is active.
/// </summary>
public class BeatCoverFactsTests
{
    public enum Event
    {
        Appear,
        Disappear,
        Stop,
        Resume,
        SwapForeground,
        SwapBackground,
        SwapUnknown,
    }

    private static readonly Event[] AllEvents = Enum.GetValues<Event>();

    public static IEnumerable<object[]> Sequences()
    {
        yield return [Array.Empty<Event>()];
        foreach (Event a in AllEvents)
        {
            yield return [new[] { a }];
            foreach (Event b in AllEvents)
            {
                yield return [new[] { a, b }];
                foreach (Event c in AllEvents)
                {
                    yield return [new[] { a, b, c }];
                    foreach (Event d in AllEvents)
                    {
                        yield return [new[] { a, b, c, d }];
                    }
                }
            }
        }
    }

    private static void Feed(BeatCoverFacts facts, Event e)
    {
        switch (e)
        {
            case Event.Appear: facts.Appeared(); break;
            case Event.Disappear: facts.Disappeared(); break;
            case Event.Stop: facts.WindowStopped(); break;
            case Event.Resume: facts.WindowResumed(); break;
            case Event.SwapForeground: facts.WindowWatched(true); break;
            case Event.SwapBackground: facts.WindowWatched(false); break;
            case Event.SwapUnknown: facts.WindowWatched(null); break;
        }
    }

    [Theory]
    [MemberData(nameof(Sequences))]
    public void The_beat_runs_exactly_when_the_page_is_shown_and_its_window_in_front_in_every_order(Event[] events)
    {
        var facts = new BeatCoverFacts();
        var beat = new SilentBeat();
        bool timerRunning = beat.Begin(); // the owner's timer: started when Begin says so, flipped only by the true results below
        bool pageShown = false;
        bool windowFront = true;
        if (beat.SetHidden(facts.Hidden))
        {
            timerRunning = !facts.Hidden;
        }

        foreach (Event e in events)
        {
            Feed(facts, e);
            pageShown = e switch { Event.Appear => true, Event.Disappear => false, _ => pageShown };
            windowFront = e switch { Event.Stop or Event.SwapBackground => false, Event.Resume or Event.SwapForeground => true, _ => windowFront };

            bool change = beat.SetHidden(facts.Hidden);
            if (change)
            {
                timerRunning = !facts.Hidden;
            }

            Assert.Equal(pageShown && windowFront, !facts.Hidden);
            Assert.Equal(pageShown && windowFront, beat.Running);
            Assert.Equal(beat.Running, timerRunning);
            Assert.False(beat.SetHidden(facts.Hidden)); // idempotent: stating the same fact again changes nothing
        }
    }

    [Fact]
    public void The_beat_and_the_cover_facts_start_in_the_same_state()
    {
        var facts = new BeatCoverFacts();
        var beat = new SilentBeat(BeatCoverFacts.StartsHidden);

        Assert.Equal(facts.Hidden, beat.Hidden);
        Assert.False(beat.Begin()); // a beat begun before the first Appearing does not run
        Assert.False(beat.Running);
        Assert.True(beat.SetHidden(facts.Hidden) == false); // stating the facts' own state changes nothing
    }

    [Fact]
    public void A_resume_after_a_stop_shows_the_beat_without_any_platform_read()
    {
        var facts = new BeatCoverFacts();
        var beat = new SilentBeat(BeatCoverFacts.StartsHidden);
        facts.Appeared();
        beat.SetHidden(facts.Hidden);
        beat.Begin();
        facts.WindowStopped();
        Assert.True(beat.SetHidden(facts.Hidden)); // timer stops

        facts.WindowResumed();

        Assert.True(beat.SetHidden(facts.Hidden)); // whole beat starts
        Assert.True(beat.Running);
    }

    [Fact]
    public void The_initial_facts_are_hidden_because_the_page_has_not_appeared_and_appearing_shows_them()
    {
        var facts = new BeatCoverFacts();

        Assert.True(BeatCoverFacts.StartsHidden);
        Assert.True(facts.Hidden);
        Assert.Equal(BeatCoverCause.Page, facts.Cause);
        facts.Appeared();
        Assert.False(facts.Hidden);
    }

    [Fact]
    public void An_unknown_window_state_leaves_the_fact_unchanged()
    {
        var facts = new BeatCoverFacts();
        facts.Appeared();
        facts.WindowStopped();

        facts.WindowWatched(null);

        Assert.False(facts.WindowInForeground);
        Assert.Equal(BeatCoverCause.Window, facts.Cause);
        facts.WindowWatched(true);
        Assert.Equal(BeatCoverCause.None, facts.Cause);
    }

    [Fact]
    public void A_hidden_page_names_the_page_as_the_cause_before_the_window()
    {
        var facts = new BeatCoverFacts();
        facts.WindowStopped();

        Assert.Equal(BeatCoverCause.Page, facts.Cause);
    }

    [Fact]
    public void A_window_already_stopped_when_first_watched_keeps_the_beat_still()
    {
        var facts = new BeatCoverFacts();
        var beat = new SilentBeat();
        beat.Begin();
        facts.Appeared();

        facts.WindowWatched(false);

        Assert.False(beat.SetHidden(facts.Hidden) && !facts.Hidden);
        Assert.False(beat.Running);
    }

    [Fact]
    public void Activated_shows_the_beat_after_stop_and_an_unknown_swap_with_no_resume()
    {
        var facts = new BeatCoverFacts();
        var beat = new SilentBeat(BeatCoverFacts.StartsHidden);
        beat.Begin();
        facts.Appeared();
        beat.SetHidden(facts.Hidden);
        facts.WindowStopped();
        Assert.True(beat.SetHidden(facts.Hidden));
        facts.WindowWatched(null); // a recreated activity: the platform could not tell
        Assert.False(beat.SetHidden(facts.Hidden));
        Assert.False(beat.Running);

        facts.WindowActivated();

        Assert.True(beat.SetHidden(facts.Hidden));
        Assert.True(beat.Running);
    }

    [Fact]
    public void Leaving_while_the_window_is_stopped_leaves_the_beat_still_until_the_page_is_back_and_in_front()
    {
        var facts = new BeatCoverFacts();
        var beat = new SilentBeat();
        beat.Begin();
        facts.Appeared();
        facts.WindowStopped();
        beat.SetHidden(facts.Hidden);

        facts.Disappeared();
        beat.SetHidden(facts.Hidden);
        facts.Appeared();
        facts.WindowWatched(true); // resubscribed: the window is in front
        Assert.True(beat.SetHidden(facts.Hidden));
        Assert.True(beat.Running);
    }
}
