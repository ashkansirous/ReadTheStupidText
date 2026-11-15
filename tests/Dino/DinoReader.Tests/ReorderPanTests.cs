using DinoReader.Core;

namespace DinoReader.Tests;

public sealed class ReorderPanTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(3, 4, false)] // 5 dp: a tap's wobble
    [InlineData(7.9, 0, false)]
    [InlineData(8, 0, true)] // the threshold itself starts the drag
    [InlineData(0, -8, true)] // up counts as much as down
    [InlineData(6, 6, true)] // diagonal: 8.49 dp
    public void A_touch_becomes_a_drag_only_past_the_threshold(double dx, double dy, bool expected) =>
        Assert.Equal(expected, ReorderPan.StartsDrag(dx, dy));

    [Theory]
    [InlineData(0, 8)] // no platform slop known: the 8 dp floor
    [InlineData(4, 8)]
    [InlineData(8, 8)]
    [InlineData(9.5, 9.5)] // a platform slop above the floor wins
    public void The_threshold_is_never_below_the_platform_touch_slop(double slopDp, double expected) =>
        Assert.Equal(expected, ReorderPan.ThresholdFor(slopDp));

    [Fact]
    public void A_wider_threshold_needs_a_longer_move()
    {
        Assert.False(ReorderPan.StartsDrag(8.5, 0, 9.5));
        Assert.True(ReorderPan.StartsDrag(9.5, 0, 9.5));
    }

    [Fact]
    public void The_finger_on_the_surface_is_the_cover_spot_plus_the_touch_offset_plus_the_move()
    {
        (double x, double y) = ReorderPan.FingerOnSurface(100, 200, 20, 30, 15, -5, 0);
        Assert.Equal((135.0, 225.0), (x, y));
    }

    [Fact]
    public void A_scroll_under_a_still_finger_moves_it_down_the_surface()
    {
        (double x, double y) = ReorderPan.FingerOnSurface(100, 200, 20, 30, 0, 0, 72);
        Assert.Equal((120.0, 302.0), (x, y));
    }
}
