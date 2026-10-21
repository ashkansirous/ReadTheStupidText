using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class ReadOwnershipTests
{
    [Fact]
    public void Original_owner_leaving_alone_is_the_last()
    {
        var owners = new ReadOwnership();

        Assert.True(owners.Leave());
    }

    [Fact]
    public void Joined_owner_keeps_the_read_alive_after_the_original_leaves()
    {
        var owners = new ReadOwnership();

        Assert.True(owners.TryJoin());
        Assert.False(owners.Leave());
        Assert.True(owners.Leave());
    }

    [Fact]
    public void Second_skip_can_join_after_the_first_skip_while_original_has_left()
    {
        var owners = new ReadOwnership();
        owners.TryJoin();
        owners.Leave();

        Assert.True(owners.TryJoin());
    }

    [Fact]
    public void Repeated_skips_hand_the_read_over_without_it_ever_ending()
    {
        var owners = new ReadOwnership();
        Assert.True(owners.TryJoin());
        Assert.False(owners.Leave());
        Assert.True(owners.TryJoin());
        Assert.False(owners.Leave());

        Assert.True(owners.Leave());
    }

    [Fact]
    public void Join_fails_once_the_last_owner_has_left()
    {
        var owners = new ReadOwnership();
        owners.Leave();

        Assert.False(owners.TryJoin());
    }
}
