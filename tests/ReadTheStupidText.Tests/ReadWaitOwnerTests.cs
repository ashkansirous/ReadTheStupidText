using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class ReadWaitOwnerTests
{
    [Fact]
    public void Loading_then_playing_ends_the_loading_wait()
    {
        var owner = new ReadWaitOwner();

        Assert.Equal(ReadWaitAction.Begin, owner.Next(loading: true, stalled: false, foreignWaitActive: false));
        Assert.Equal(ReadWaitAction.End, owner.Next(loading: false, stalled: false, foreignWaitActive: true));
        Assert.False(owner.Owns);
    }

    [Fact]
    public void Loading_then_cancel_ends_the_loading_wait()
    {
        var owner = new ReadWaitOwner();
        owner.Next(loading: true, stalled: false, foreignWaitActive: false);

        Assert.Equal(ReadWaitAction.End, owner.Cancelled());
        Assert.Equal(ReadWaitAction.None, owner.Next(loading: false, stalled: false, foreignWaitActive: false));
    }

    [Fact]
    public void Playing_stalled_playing_begins_then_ends_the_stalled_wait()
    {
        var owner = new ReadWaitOwner();

        Assert.Equal(ReadWaitAction.None, owner.Next(loading: false, stalled: false, foreignWaitActive: false));
        Assert.Equal(ReadWaitAction.Begin, owner.Next(loading: false, stalled: true, foreignWaitActive: false));
        Assert.Equal(ReadWaitAction.End, owner.Next(loading: false, stalled: false, foreignWaitActive: true));
    }

    [Fact]
    public void Stalled_then_cancel_ends_the_stalled_wait()
    {
        var owner = new ReadWaitOwner();
        owner.Next(loading: false, stalled: true, foreignWaitActive: false);

        Assert.Equal(ReadWaitAction.End, owner.Cancelled());
        Assert.False(owner.Owns);
    }

    [Fact]
    public void A_wait_someone_else_began_is_neither_taken_nor_ended()
    {
        var owner = new ReadWaitOwner();

        Assert.Equal(ReadWaitAction.None, owner.Next(loading: false, stalled: true, foreignWaitActive: true));
        Assert.Equal(ReadWaitAction.None, owner.Next(loading: false, stalled: false, foreignWaitActive: true));
        Assert.Equal(ReadWaitAction.None, owner.Cancelled());
    }

    [Fact]
    public void Loading_that_stalls_into_a_stall_keeps_one_owned_wait_until_it_clears()
    {
        var owner = new ReadWaitOwner();
        owner.Next(loading: true, stalled: false, foreignWaitActive: false);

        Assert.Equal(ReadWaitAction.Begin, owner.Next(loading: false, stalled: true, foreignWaitActive: true));
        Assert.Equal(ReadWaitAction.End, owner.Next(loading: false, stalled: false, foreignWaitActive: true));
    }
}
