using DinoReader;
using Xunit;

namespace DinoReader.Tests;

public class DownloadCancelSlotTests
{
    [Fact]
    public void A_new_download_cancels_a_run_still_going_without_disposing_its_source()
    {
        var slot = new DownloadCancelSlot();
        CancellationToken first = slot.Begin();

        CancellationToken second = slot.Begin();

        Assert.True(first.IsCancellationRequested);
        Assert.False(second.IsCancellationRequested);
        Assert.True(first.CanBeCanceled); // reading the cancelled run's token after the new Begin must not throw (its source is still alive)
    }

    [Fact]
    public void The_end_of_the_first_download_does_not_dispose_the_second_ones_source()
    {
        var slot = new DownloadCancelSlot();
        CancellationToken first = slot.Begin();
        CancellationToken second = slot.Begin();

        slot.End(first);
        slot.Cancel();

        Assert.True(second.IsCancellationRequested); // the second run's cancel still works
    }

    [Fact]
    public void Cancel_trips_the_running_download_and_the_source_lives_until_its_own_end()
    {
        var slot = new DownloadCancelSlot();
        CancellationToken own = slot.Begin();

        slot.Cancel();
        Assert.True(own.IsCancellationRequested);
        Assert.True(own.CanBeCanceled); // not disposed by the cancel

        slot.End(own);
        slot.Cancel(); // nothing left: nothing thrown
    }
}
