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
    [InlineData(1100, 600, WindowShape.TabletLandscape)]
    [InlineData(800, 560, WindowShape.PhoneLandscape)]
    [InlineData(1100, 760, WindowShape.TabletLandscape)]
    [InlineData(1920, 1080, WindowShape.TabletLandscape)]
    [InlineData(1100, 599, WindowShape.PhoneLandscape)] // wide but short: the phone landscape rules lay it out, so that is its shape
    public void The_window_rule_and_the_pages_rules_give_one_shape(double width, double height, WindowShape expected)
    {
        (double w, double h) = WindowShapeSeam.Effective(true, width, height);
        Assert.Equal(expected, WindowLayout.For(width, height));
        Assert.Equal(expected, WindowShapeSeam.DeviceShapeOf(w, h));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void On_windows_every_size_gets_the_same_shape_from_both(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        (double w, double h) = WindowShapeSeam.Effective(true, width, height);
        Assert.Equal(WindowLayout.For(width, height), WindowShapeSeam.DeviceShapeOf(w, h));
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

    [Theory]
    [InlineData(false, false, false, false, 400, 14, 5, 14, 0, 14)]   // a phone: equal 14 dp sides
    [InlineData(false, false, false, true, 1000, 40, 5, 40, 0, 40)]   // a tablet shelf, upright: its own 40 dp
    [InlineData(false, true, false, false, 800, 14, 3, 34, 0, 0)]     // held sideways: 34 on the left only
    [InlineData(true, false, false, false, 800, 40, 4, 40, 0, 40)]    // Windows, no rail, narrow: the shelf's own 40 dp
    [InlineData(true, false, false, false, 2400, 40, 6, 400, 0, 400)] // Windows, no rail, wide: centred (the VM's 399 px against 13 px)
    [InlineData(true, false, true, false, 1000, 40, 4, 16, 14, 26)]   // Windows with the rail: 16 / 14 / 26
    [InlineData(false, false, true, false, 1000, 14, 4, 16, 14, 26)]  // a phone-class window with the rail
    [InlineData(false, true, false, true, 1000, 40, 3, 34, 0, 0)]     // sideways and a tablet shelf: sideways wins
    [InlineData(true, true, true, false, 1000, 40, 4, 16, 14, 26)]    // rail and sideways: the rail wins
    public void The_shelf_list_geometry_is_one_rule(bool windows, bool sideways, bool rail, bool tabletShelf, double pageWidth, double sidePadding, int columns, double left, double top, double right)
    {
        WindowShapeSeam.ShelfGeometry expected = new(left, top, right, columns, 20, 28);
        Assert.Equal(expected, WindowShapeSeam.ShelfListGeometry(windows, sideways, rail, tabletShelf, pageWidth, sidePadding, columns, 20, 28)); // the gaps are carried as given
    }

    [Fact]
    public void The_reorder_layer_copies_the_shelf_geometry_moved_right_by_the_rail()
    {
        WindowShapeSeam.ShelfGeometry shelf = WindowShapeSeam.ShelfListGeometry(true, false, true, false, 1000, 40, 4, 12, 12);

        Assert.Equal(new WindowShapeSeam.ShelfGeometry(316, 14, 26, 4, 12, 12), shelf.BesideRail(300)); // the left grows by the rail; right, top and columns are the shelf's
    }

    [Theory]
    [InlineData(320)]
    [InlineData(360)]
    [InlineData(430)]
    public void The_reorder_layer_on_a_phone_is_frames_173_and_174(double pageWidth)
    {
        Assert.True(pageWidth > 0); // the phone's layer does not depend on the width

        WindowShapeSeam.ReorderLayerShape shape = WindowShapeSeam.ReorderLayerPhone();

        Assert.Equal(new WindowShapeSeam.ShelfGeometry(14, 0, 14, 3, 10, 27), shape.Grid); // 14 dp sides, 3 across, gap 10, row gap 27 (RH 180 less the mock tile 153), no top padding
        Assert.Equal(new WindowShapeSeam.ReorderHint(18, 12, 18, 12, 13.5), shape.Hint);   // `padding:12px 18px 12px`
    }

    [Theory]
    [InlineData(500, 3)]   // fewer than 3 never
    [InlineData(1024, 5)]  // the VM window (the layer covers the rail, so the full width counts)
    [InlineData(1100, 6)]  // the mock's window
    [InlineData(1600, 8)]
    [InlineData(2400, 13)]
    public void The_reorder_layer_on_the_windows_head_keeps_the_mock_tile_and_the_columns_follow_the_full_page_width(double pageWidth, int columns)
    {
        WindowShapeSeam.ReorderLayerShape shape = WindowShapeSeam.ReorderLayerDesktop(pageWidth);

        Assert.Equal(new WindowShapeSeam.ShelfGeometry(40, 0, 40, columns, 18, 18), shape.Grid); // 40 dp from both page edges (no rail offset), gap 18 both ways, no top padding
        Assert.Equal(new WindowShapeSeam.ReorderHint(40, 16, 40, 14, 14), shape.Hint); // `padding:16px 40px 14px`, 14 px
    }

    [Theory]
    [InlineData(1100)]
    [InlineData(1600)]
    [InlineData(2400)]
    public void The_windows_tile_stays_about_the_mock_155_dp(double pageWidth)
    {
        Assert.Equal(155, WindowShapeSeam.ReorderDesktopTileWidth);

        WindowShapeSeam.ReorderLayerShape shape = WindowShapeSeam.ReorderLayerDesktop(pageWidth);
        ShelfOrderGrid grid = ShelfOrderGrid.Fit(pageWidth, shape.Grid.Columns, 40, 40, 0, 18, shape.Grid.GapY, 18, WindowShapeSeam.ReorderTitleSpacing);

        Assert.InRange(grid.TileWidth, 155, 155 + 25); // never narrower than the mock's tile; the leftover of a column that did not fit is spread over the tiles
    }

    [Fact]
    public void Columns_for_a_windows_window_match_the_gallery_rule_in_the_reorder_check()
    {
        Assert.Equal(6, WindowShapeSeam.ReorderDesktopColumns(1100)); // floor((W - 80 + 18) / (155 + 18)), at least 3
        Assert.Equal(5, WindowShapeSeam.ReorderDesktopColumns(1099));
        Assert.Equal(6, WindowShapeSeam.ReorderDesktopColumns(1100.0000000001)); // float slack must not move the boundary
    }

    [Theory]
    [InlineData(true, false, 1100, 760, true)]   // the mock's Windows window
    [InlineData(true, false, 400, 800, false)]   // a narrow portrait Windows window has no frame: phone geometry
    [InlineData(true, true, 1200, 800, false)]   // a tablet keeps the shelf's
    [InlineData(false, false, 1100, 760, false)] // other heads never use the desktop frame
    public void The_desktop_frame_is_for_a_landscape_windows_window_only(bool isWindows, bool tablet, double width, double height, bool expected)
    {
        Assert.Equal(expected, WindowShapeSeam.ReorderUsesDesktopGeometry(isWindows, tablet, width, height));
    }

    [Theory]
    [InlineData(0, 12)]  // no top inset on the shelf: one gap down, as Slice 319 did
    [InlineData(14, 14)] // beside the rail: the shelf's own inset
    public void The_reorder_layer_on_a_tablet_keeps_the_shelf_geometry_and_the_hint_lines_up_with_the_grid(double shelfTop, double expectedTop)
    {
        WindowShapeSeam.ShelfGeometry shelf = new(316, shelfTop, 26, 4, 12, 12);

        WindowShapeSeam.ReorderLayerShape shape = WindowShapeSeam.ReorderLayerShelf(shelf);

        Assert.Equal(shelf with { Top = expectedTop }, shape.Grid); // no tablet frame in the mock
        Assert.Equal(new WindowShapeSeam.ReorderHint(316, 12, 26, 8, 13.5), shape.Hint); // the hint starts at the grid's left edge (beside the rail: after it)
    }

    [Theory]
    [InlineData(360, 780, false, false)]
    [InlineData(780, 360, false, true)]  // a phone held sideways has no frame
    [InlineData(800, 1200, true, true)]  // a tablet has none either
    public void The_reorder_layer_uses_the_shelf_geometry_for_a_tablet_or_a_phone_held_sideways(double width, double height, bool tablet, bool expected)
    {
        Assert.Equal(expected, WindowShapeSeam.ReorderUsesShelfGeometry(tablet, width, height));
    }

    [Theory]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, false)]
    public void The_tablet_test_follows_the_shelf_or_the_corner(bool grownUp, bool shelfTablet, bool cornerTablet, bool expected)
    {
        Assert.Equal(expected, WindowShapeSeam.IsTabletUpright(grownUp, shelfTablet, cornerTablet));
    }

    [Fact]
    public void The_rail_margins_are_named_constants()
    {
        Assert.Equal((16, 14, 26), (WindowShapeSeam.RailListInset, WindowShapeSeam.RailListTop, WindowShapeSeam.RailListRight));
    }

    [Fact]
    public void A_tablet_upright_window_keeps_its_own_shelf_rule()
    {
        Assert.Equal(LandscapeLayout.ShelfLayout(700, 800), WindowShapeSeam.ShelfLayout(true, 700, 800));
    }
}
