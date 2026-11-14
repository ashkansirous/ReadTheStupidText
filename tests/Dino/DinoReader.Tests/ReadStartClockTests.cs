using DinoReader.Core;

namespace DinoReader.Tests;

public class ReadStartClockTests
{
    [Fact]
    public void Answers_for_its_own_read()
    {
        var clock = new ReadStartClock(7, () => 42);

        Assert.Equal(42, clock.ElapsedFor(7));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(0)]
    public void Gives_no_time_to_another_read(int otherRead)
    {
        var clock = new ReadStartClock(7, () => 42);

        Assert.Null(clock.ElapsedFor(otherRead));
    }

    [Fact]
    public void A_clock_made_for_a_new_read_ignores_the_previous_reads_event()
    {
        var previous = new ReadStartClock(1, () => 10);
        var next = new ReadStartClock(2, () => 5);

        Assert.Null(next.ElapsedFor(1));
        Assert.Equal(10, previous.ElapsedFor(1));
    }

    [Fact]
    public void The_stopwatch_form_counts_from_creation()
    {
        var clock = new ReadStartClock(3);

        Assert.True(clock.ElapsedFor(3) >= 0);
    }
}
