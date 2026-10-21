using ReadTheStupidText.Infrastructure.Reading;

namespace ReadTheStupidText.Tests;

public class ReadOwnershipTests
{
    [Fact]
    public void The_original_owner_leaving_alone_ends_the_read()
    {
        var read = new ReadOwnership();

        Assert.True(read.Leave());
    }

    [Fact]
    public void A_skip_that_joined_keeps_the_read_alive_after_the_original_owner_leaves()
    {
        var read = new ReadOwnership();
        Assert.True(read.TryJoin());

        Assert.False(read.Leave()); // the original SpeakAsync returns
        Assert.True(read.Leave());  // the skip's playback ends: the last owner ends the read
    }

    [Fact]
    public void Repeated_skips_hand_the_read_over_without_it_ever_ending()
    {
        var read = new ReadOwnership();
        Assert.True(read.TryJoin());   // first skip joins, then supersedes the original
        Assert.False(read.Leave());    // original returns
        Assert.True(read.TryJoin());   // second skip joins before it supersedes the first
        Assert.False(read.Leave());    // first skip's playback is released

        Assert.True(read.Leave());     // second skip finishes: only now is the read over
    }

    [Fact]
    public void A_finished_read_cannot_be_joined()
    {
        var read = new ReadOwnership();
        read.Leave();

        Assert.False(read.TryJoin());
    }
}
