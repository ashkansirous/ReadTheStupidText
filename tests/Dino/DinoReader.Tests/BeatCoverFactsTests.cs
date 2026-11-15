using DinoReader.Core;
using Xunit;

namespace DinoReader.Tests;

/// <summary>
/// Slice 383: the page feeds its appear and disappear into <see cref="BeatCoverFacts"/> and states the result to <see cref="SilentBeat.SetHidden"/>. Every order of up to four events must leave the beat running
/// exactly when the page is shown and the beat is active; the window (lock, Home) is not an input at all.
/// </summary>
public class BeatCoverFactsTests
{
    public enum Event
    {
        Appear,
        Disappear,
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
        }
    }

    [Theory]
    [MemberData(nameof(Sequences))]
    public void The_beat_runs_exactly_when_the_page_is_shown_in_every_order(Event[] events)
    {
        var facts = new BeatCoverFacts();
        var beat = new SilentBeat();
        bool timerRunning = beat.Begin(); // the owner's timer: started when Begin says so, flipped only by the true results below
        bool pageShown = false;
        if (beat.SetHidden(facts.Hidden))
        {
            timerRunning = !facts.Hidden;
        }

        foreach (Event e in events)
        {
            Feed(facts, e);
            pageShown = e switch { Event.Appear => true, Event.Disappear => false, _ => pageShown };

            bool change = beat.SetHidden(facts.Hidden);
            if (change)
            {
                timerRunning = !facts.Hidden;
            }

            Assert.Equal(pageShown, !facts.Hidden); // Slice 383: the window never covers the page
            Assert.Equal(pageShown, beat.Running);
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
    public void A_gate_or_sheet_over_the_page_still_stops_the_beat_with_the_screen_on()
    {
        var facts = new BeatCoverFacts();
        var beat = new SilentBeat(BeatCoverFacts.StartsHidden);
        facts.Appeared();
        beat.SetHidden(facts.Hidden);
        beat.Begin();

        facts.Disappeared();

        Assert.True(beat.SetHidden(facts.Hidden));
        Assert.False(beat.Running);
        facts.Appeared();
        Assert.True(beat.SetHidden(facts.Hidden));
        Assert.True(beat.Running);
    }

    [Fact]
    public void The_initial_facts_are_hidden_because_the_page_has_not_appeared_and_appearing_shows_them()
    {
        var facts = new BeatCoverFacts();

        Assert.True(BeatCoverFacts.StartsHidden);
        Assert.True(facts.Hidden);
        facts.Appeared();
        Assert.False(facts.Hidden);
    }
}
