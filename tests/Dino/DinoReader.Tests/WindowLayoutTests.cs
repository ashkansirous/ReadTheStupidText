using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 235 part A: the width rule of the Windows window (Batch 46 mock), on the content area (window minus the 32 px title bar).
public class WindowLayoutTests
{
    [Theory]
    [InlineData(1100, 728, WindowShape.TabletLandscape)] // the opening size
    [InlineData(800, 528, WindowShape.PhoneLandscape)] // the minimum
    [InlineData(1920, 1048, WindowShape.TabletLandscape)] // maximised
    [InlineData(700, 800, WindowShape.TabletUpright)]
    [InlineData(500, 900, WindowShape.PhonePortrait)]
    public void The_mocks_cases(double width, double height, WindowShape expected)
    {
        Assert.Equal(expected, WindowLayout.For(width, height));
    }

    [Theory]
    [InlineData(959, 600, WindowShape.PhoneLandscape)]
    [InlineData(960, 600, WindowShape.TabletLandscape)]
    [InlineData(600, 601, WindowShape.TabletUpright)]
    [InlineData(599, 601, WindowShape.PhonePortrait)]
    [InlineData(700, 700, WindowShape.PhoneLandscape)] // square: not taller than wide
    [InlineData(960, 961, WindowShape.TabletUpright)]
    [InlineData(1000, 1200, WindowShape.TabletUpright)] // wide enough for landscape, but taller than wide
    public void Boundaries(double width, double height, WindowShape expected)
    {
        Assert.Equal(expected, WindowLayout.For(width, height));
    }

    [Theory]
    [InlineData(800, 800)]
    [InlineData(1600, 1600)]
    [InlineData(1920, 1600)]
    public void Content_is_capped_at_1600(double window, double expected)
    {
        Assert.Equal(expected, WindowLayout.ContentWidth(window));
    }

    [Fact]
    public void Content_height_leaves_out_the_title_bar()
    {
        Assert.Equal(728, WindowLayout.ContentHeight(WindowLayout.DefaultWindowHeight));
        Assert.Equal(0, WindowLayout.ContentHeight(10));
    }
}
