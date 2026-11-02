using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 235 part A: a window below its minimum is resized back up.
public class WindowMinimumTests
{
    private static readonly (int Width, int Height) Minimum = (800, 560);

    [Fact]
    public void A_size_at_or_above_the_minimum_needs_no_resize()
    {
        Assert.Null(WindowMinimum.Enforce((800, 560), Minimum));
        Assert.Null(WindowMinimum.Enforce((1100, 760), Minimum));
    }

    [Fact]
    public void A_size_below_the_minimum_in_both_directions_goes_up_to_it()
    {
        Assert.Equal((800, 560), WindowMinimum.Enforce((400, 300), Minimum));
    }

    [Fact]
    public void Only_the_short_side_is_raised()
    {
        Assert.Equal((800, 700), WindowMinimum.Enforce((600, 700), Minimum));
        Assert.Equal((1000, 560), WindowMinimum.Enforce((1000, 300), Minimum));
    }
}
