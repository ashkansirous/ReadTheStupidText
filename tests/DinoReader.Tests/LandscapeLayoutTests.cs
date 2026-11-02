using DinoReader.Core;

namespace DinoReader.Tests;

public class LandscapeLayoutTests
{
    [Theory]
    [InlineData(760, 360, true)]
    [InlineData(360, 760, false)]
    [InlineData(500, 500, false)]
    public void IsLandscape_is_width_over_height(double w, double h, bool expected) =>
        Assert.Equal(expected, LandscapeLayout.IsLandscape(w, h));

    [Theory]
    [InlineData(760, 360, DeviceSize.Phone)]
    [InlineData(360, 760, DeviceSize.Phone)]
    [InlineData(1164, 804, DeviceSize.Tablet)]
    [InlineData(804, 1164, DeviceSize.Tablet)]
    [InlineData(1000, 599.9, DeviceSize.Phone)]
    [InlineData(1000, 600, DeviceSize.Tablet)]
    public void Phone_or_tablet_is_told_by_the_short_side(double w, double h, DeviceSize expected) =>
        Assert.Equal(expected, LandscapeLayout.SizeOf(w, h));

    [Fact]
    public void A_phone_held_sideways_gets_one_sideways_row_of_covers_as_tall_as_the_window_less_120()
    {
        ShelfShape shelf = LandscapeLayout.ShelfLayout(760, 360);
        Assert.True(shelf.Sideways);
        Assert.Equal(240, shelf.CoverHeight);
    }

    [Theory]
    [InlineData(360, 760, 2)]
    [InlineData(1164, 804, 6)]
    [InlineData(1000, 700, 5)]
    [InlineData(100, 700, 2)]
    [InlineData(3000, 2000, 6)]
    public void Everything_else_gets_a_grid_of_two_to_six_columns(double w, double h, int columns)
    {
        ShelfShape shelf = LandscapeLayout.ShelfLayout(w, h);
        Assert.False(shelf.Sideways);
        Assert.Equal(columns, shelf.Columns);
    }

    [Fact]
    public void A_tablet_held_sideways_keeps_a_grid()
    {
        Assert.False(LandscapeLayout.ShelfLayout(1164, 804).Sideways);
    }

    [Fact]
    public void A_tiny_window_never_has_a_negative_cover_height() =>
        Assert.Equal(0, LandscapeLayout.ShelfLayout(100, 80).CoverHeight);

    [Fact]
    public void The_picture_pane_is_the_picture_fitted_to_the_full_height()
    {
        Assert.Equal(300, LandscapeLayout.PicturePaneWidth(760, 400, 0.75), 6);
    }

    [Fact]
    public void A_wide_picture_is_capped_at_45_percent_of_the_width()
    {
        Assert.Equal(342, LandscapeLayout.PicturePaneWidth(760, 360, 2.0), 6);
    }

    [Fact]
    public void An_unknown_picture_shape_uses_three_by_four()
    {
        Assert.Equal(270, LandscapeLayout.PicturePaneWidth(760, 360, 0), 6);
    }

    [Theory]
    [InlineData(760, 360, 300)]
    [InlineData(1164, 804, 360)]
    public void List_panes_are_300_on_a_phone_and_360_on_a_tablet(double w, double h, double expected) =>
        Assert.Equal(expected, LandscapeLayout.ListPaneWidth(w, h));

    [Theory]
    [InlineData(360, ControlsPlacement.Rail)]
    [InlineData(479.9, ControlsPlacement.Rail)]
    [InlineData(480, ControlsPlacement.Row)]
    [InlineData(804, ControlsPlacement.Row)]
    public void Reading_controls_are_a_rail_under_480_dp_tall(double height, ControlsPlacement expected) =>
        Assert.Equal(expected, LandscapeLayout.ReadingControls(height));

    [Fact]
    public void Rail_buttons_are_68_and_52_and_row_buttons_84_and_64()
    {
        Assert.Equal(new ControlSizes(68, 52), LandscapeLayout.ControlSizesFor(ControlsPlacement.Rail));
        Assert.Equal(new ControlSizes(84, 64), LandscapeLayout.ControlSizesFor(ControlsPlacement.Row));
    }

    [Theory]
    [InlineData(760, 360, PinnedPlacement.SideRailBottom)]
    [InlineData(360, 760, PinnedPlacement.BottomBar)]
    public void A_pinned_button_moves_to_the_side_rail_only_in_landscape(double w, double h, PinnedPlacement expected) =>
        Assert.Equal(expected, LandscapeLayout.PinnedButton(w, h));

    [Fact]
    public void The_text_card_starts_after_the_picture_pane_and_the_gap() =>
        Assert.Equal(300 + LandscapeLayout.SidewaysGap, LandscapeLayout.TextCardLeft(760, 400, 0.75, false), 6);

    [Fact]
    public void An_unknown_picture_shape_still_leaves_the_text_card_after_a_three_by_four_pane() =>
        Assert.Equal(270 + LandscapeLayout.SidewaysGap, LandscapeLayout.TextCardLeft(760, 360, 0, false), 6);

    [Fact]
    public void A_page_with_no_picture_has_its_text_card_at_the_left_edge() =>
        Assert.Equal(0, LandscapeLayout.TextCardLeft(760, 400, null, false));

    [Fact]
    public void A_picture_only_page_has_no_text_card_offset() =>
        Assert.Equal(0, LandscapeLayout.TextCardLeft(760, 400, 0.75, true));

    [Fact]
    public void A_cover_is_three_wide_for_four_tall()
    {
        Assert.Equal(180, LandscapeLayout.CoverWidth(240), 6);
        Assert.Equal(0, LandscapeLayout.CoverWidth(-5));
    }

    [Theory]
    [InlineData(360, 760, false, 0, 3, false)]
    [InlineData(700, 500, true, 300, 3, false)]
    [InlineData(760, 360, true, 300, 3, false)]
    [InlineData(640, 360, true, 300, 2, false)]
    [InlineData(568, 320, true, 300, 2, false)]
    [InlineData(1164, 804, true, 360, 5, true)]
    [InlineData(744, 1164, true, 360, 3, true)]
    [InlineData(700, 1100, false, 0, 3, false)]
    public void The_corner_gets_an_ink_rail_held_sideways_or_on_a_wide_tablet(double w, double h, bool rail, double railWidth, int columns, bool fullRows)
    {
        CornerShape corner = LandscapeLayout.CornerLayout(w, h);
        Assert.Equal(rail, corner.Rail);
        Assert.Equal(railWidth, corner.RailWidth);
        Assert.Equal(columns, corner.Columns);
        Assert.Equal(fullRows, corner.FullRowButtons);
    }

    [Theory]
    [InlineData(300, 2)]
    [InlineData(0, 2)]
    [InlineData(400, 3)]
    [InlineData(804, 5)]
    public void The_corner_fits_as_many_book_columns_as_the_width_left_allows(double widthLeft, int columns) =>
        Assert.Equal(columns, LandscapeLayout.CornerColumnsFor(widthLeft));

    [Theory]
    [InlineData(640, 360)]
    [InlineData(568, 320)]
    [InlineData(760, 360)]
    [InlineData(1164, 804)]
    public void Every_corner_column_keeps_at_least_its_minimum_width_unless_it_is_the_two_column_floor(double w, double h)
    {
        CornerShape corner = LandscapeLayout.CornerLayout(w, h);
        double each = (w - corner.RailWidth - LandscapeLayout.CornerBooksMargin - (corner.Columns - 1) * LandscapeLayout.CornerColumnGap) / corner.Columns;
        Assert.True(each >= LandscapeLayout.CornerColumnWidth || corner.Columns == LandscapeLayout.MinCornerColumns);
    }

    [Fact]
    public void A_phone_held_upright_never_gets_the_rail_however_wide_the_screen_is() =>
        Assert.False(LandscapeLayout.CornerLayout(590, 1000).Rail);
}
