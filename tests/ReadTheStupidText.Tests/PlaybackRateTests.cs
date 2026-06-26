using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Tests;

public class PlaybackRateTests
{
    [Fact]
    public void Default_is_one()
    {
        Assert.Equal(1.0, PlaybackRate.Default.Value);
    }

    [Theory]
    [InlineData(0.5, 0.5)]   // minimum, on-step
    [InlineData(2.0, 2.0)]   // maximum, on-step
    [InlineData(1.0, 1.0)]
    [InlineData(1.25, 1.25)]
    public void On_step_values_are_preserved(double input, double expected)
    {
        Assert.Equal(expected, new PlaybackRate(input).Value);
    }

    [Theory]
    [InlineData(-1.0, 0.5)]   // below range clamps to minimum
    [InlineData(0.0, 0.5)]
    [InlineData(0.49, 0.5)]
    [InlineData(5.0, 2.0)]    // above range clamps to maximum
    [InlineData(2.01, 2.0)]
    public void Out_of_range_values_clamp(double input, double expected)
    {
        Assert.Equal(expected, new PlaybackRate(input).Value);
    }

    [Theory]
    [InlineData(0.97, 0.95)]   // snaps to nearest 0.05 step
    [InlineData(0.98, 1.0)]
    [InlineData(1.23, 1.25)]
    [InlineData(1.74, 1.75)]
    public void Off_step_values_snap_to_nearest_step(double input, double expected)
    {
        Assert.Equal(expected, new PlaybackRate(input).Value);
    }

    [Fact]
    public void Snapped_value_has_no_floating_point_drift()
    {
        // 1.15 must be exactly 1.15, not 1.1500000000000001.
        Assert.Equal(1.15, new PlaybackRate(1.151).Value);
    }

    [Fact]
    public void ToDisplayLabel_formats_with_x_suffix()
    {
        Assert.Equal("1.25x", new PlaybackRate(1.25).ToDisplayLabel());
        Assert.Equal("1x", new PlaybackRate(1.0).ToDisplayLabel());
    }
}
