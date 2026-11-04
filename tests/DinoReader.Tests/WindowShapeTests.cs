using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 235 part C: the seam between the Windows window and the pages' existing layout rules. Phones and tablets must see no change at all.
public class WindowShapeTests
{
    private static readonly WindowShape[] AllShapes =
        [WindowShape.PhonePortrait, WindowShape.PhoneLandscape, WindowShape.TabletUpright, WindowShape.TabletLandscape];

    public static TheoryData<double, double> Sizes()
    {
        var sizes = new TheoryData<double, double>();
        foreach (double width in new double[] { 0, 320, 411, 599, 600, 700, 800, 959, 960, 1100, 1600, 1920, 2400 })
        {
            foreach (double height in new double[] { 0, 320, 480, 528, 599, 600, 700, 728, 800, 1048, 1200 })
            {
                sizes.Add(width, height);
            }
        }

        return sizes;
    }

    [Fact]
    public void Off_windows_the_device_shape_is_returned_for_every_input()
    {
        foreach (WindowShape window in AllShapes)
        {
            foreach (WindowShape device in AllShapes)
            {
                Assert.Equal(device, WindowShapeSeam.For(false, window, device));
            }
        }
    }

    [Fact]
    public void On_windows_the_window_rule_decides()
    {
        foreach (WindowShape window in AllShapes)
        {
            foreach (WindowShape device in AllShapes)
            {
                Assert.Equal(window, WindowShapeSeam.For(true, window, device));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Off_windows_the_size_and_the_shelf_are_unchanged(double width, double height)
    {
        Assert.Equal((width, height), WindowShapeSeam.Effective(false, width, height));
        Assert.Equal(LandscapeLayout.ShelfLayout(width, height), WindowShapeSeam.ShelfLayout(false, width, height));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void On_windows_the_size_is_never_made_larger(double width, double height)
    {
        (double w, double h) = WindowShapeSeam.Effective(true, width, height);
        Assert.Equal(width, w);
        Assert.True(h <= height);
    }

    [Theory]
    [InlineData(1100, 728, WindowShape.TabletLandscape)] // opening size
    [InlineData(800, 528, WindowShape.PhoneLandscape)] // minimum
    [InlineData(1920, 1048, WindowShape.TabletLandscape)] // maximised
    [InlineData(900, 700, WindowShape.PhoneLandscape)] // 600+ tall but under 960 wide: the mock says phone landscape, the phone rules would say tablet
    [InlineData(700, 700, WindowShape.PhoneLandscape)] // square
    [InlineData(1000, 1000, WindowShape.TabletLandscape)] // square, wide
    [InlineData(700, 800, WindowShape.TabletUpright)]
    [InlineData(500, 900, WindowShape.PhonePortrait)]
    public void On_windows_the_existing_rules_see_the_windows_shape(double width, double height, WindowShape expected)
    {
        (double w, double h) = WindowShapeSeam.Effective(true, width, height);
        Assert.Equal(expected, WindowShapeSeam.DeviceShapeOf(w, h));
        Assert.Equal(expected, WindowLayout.For(width, height));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Asking_twice_gives_the_same_size(double width, double height)
    {
        (double w, double h) = WindowShapeSeam.Effective(true, width, height);
        Assert.Equal((w, h), WindowShapeSeam.Effective(true, w, h));
    }

    [Fact]
    public void A_desktop_shelf_keeps_40_dp_at_the_sides()
    {
        Assert.Equal(40, WindowShapeSeam.ShelfLayout(true, 1100, 728).SidePadding);
        Assert.Equal(LandscapeLayout.PhoneShelfSidePadding, WindowShapeSeam.ShelfLayout(false, 1100, 728).SidePadding);
    }

    [Fact]
    public void A_short_wide_window_stays_what_the_rules_call_it_rather_than_being_stretched()
    {
        (double w, double h) = WindowShapeSeam.Effective(true, 1000, 528);
        Assert.Equal((1000d, 528d), (w, h));
    }

    [Theory]
    [InlineData(1100, 728, WindowShape.TabletLandscape)]
    [InlineData(360, 800, WindowShape.PhonePortrait)]
    [InlineData(800, 360, WindowShape.PhoneLandscape)]
    [InlineData(1280, 800, WindowShape.TabletLandscape)]
    [InlineData(800, 1280, WindowShape.TabletUpright)]
    [InlineData(900, 700, WindowShape.TabletLandscape)] // a tablet this size is a tablet; only the Windows window calls it phone landscape
    public void The_device_shape_is_what_the_existing_rules_say(double width, double height, WindowShape expected)
    {
        Assert.Equal(expected, WindowShapeSeam.DeviceShapeOf(width, height));
    }

    [Theory]
    [InlineData(800, 4)] // minimum window: four columns
    [InlineData(1100, 5)] // opening size: five
    [InlineData(1600, 6)]
    [InlineData(1920, 6)] // maximised: capped at 1600 and six, the rule's maximum
    [InlineData(0, 2)]
    public void The_shelf_columns_follow_the_content_width(double width, int expected)
    {
        Assert.Equal(expected, WindowShapeSeam.ShelfColumns(width));
    }

    [Fact]
    public void A_phone_landscape_window_gets_a_grid_not_the_sideways_row_on_windows()
    {
        ShelfShape windows = WindowShapeSeam.ShelfLayout(true, 800, 528);
        Assert.False(windows.Sideways);
        Assert.Equal(4, windows.Columns);
        Assert.True(LandscapeLayout.ShelfLayout(800, 528).Sideways); // the phone keeps its row
    }

    [Theory]
    [InlineData(1100, 728)]
    [InlineData(1920, 1048)]
    public void A_wide_window_keeps_the_tablet_grid(double width, double height)
    {
        ShelfShape windows = WindowShapeSeam.ShelfLayout(true, width, height);
        Assert.False(windows.Sideways);
        Assert.Equal(WindowShapeSeam.ShelfColumns(width), windows.Columns);
    }

    [Theory]
    [InlineData(1100, 728)]
    [InlineData(1920, 1048)]
    [InlineData(2400, 1200)]
    public void A_tablet_landscape_window_keeps_the_rules_cover_height_and_counts_the_capped_width(double width, double height)
    {
        ShelfShape rule = LandscapeLayout.ShelfLayout(width, height);
        ShelfShape windows = WindowShapeSeam.ShelfLayout(true, width, height);
        Assert.Equal(rule.CoverHeight, windows.CoverHeight);
        Assert.Equal(WindowShapeSeam.ShelfColumns(Math.Min(width, WindowLayout.MaximumContentWidth)), windows.Columns);
    }

    [Theory]
    [InlineData(800, 40, 40)]
    [InlineData(1600, 40, 40)]
    [InlineData(1680, 40, 40)]
    [InlineData(1700, 40, 50)]
    [InlineData(2400, 40, 400)]
    public void The_shelf_side_margin_depends_only_on_the_page_width(double pageWidth, double sidePadding, double expected)
    {
        Assert.Equal(expected, WindowShapeSeam.ShelfSideMargin(pageWidth, sidePadding));
    }

    [Fact]
    public void A_tablet_upright_window_keeps_its_own_shelf_rule()
    {
        Assert.Equal(LandscapeLayout.ShelfLayout(700, 800), WindowShapeSeam.ShelfLayout(true, 700, 800));
    }
}
