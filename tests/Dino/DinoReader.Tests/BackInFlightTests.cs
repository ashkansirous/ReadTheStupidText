using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 235 part B: a second Esc while a back is under way does nothing.
public class BackInFlightTests
{
    [Fact]
    public void A_second_back_is_refused_until_the_first_ends()
    {
        var gate = new BackInFlight();

        Assert.True(gate.TryBegin());
        Assert.False(gate.TryBegin());
        Assert.True(gate.IsActive);

        gate.End();

        Assert.False(gate.IsActive);
        Assert.True(gate.TryBegin());
    }
}
