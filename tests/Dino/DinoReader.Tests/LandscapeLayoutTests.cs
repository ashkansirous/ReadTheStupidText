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

    // Slice 325 part A: the corner's rail and columns moved to CornerLayout (CornerLayoutTests); the retired LandscapeLayout.CornerLayout tests went with it.
    [Theory]
    [InlineData(804, 1164, 300)]
    [InlineData(600, 600, 300)]
    [InlineData(1164, 804, 360)]
    [InlineData(760, 360, 300)]
    [InlineData(360, 760, 300)]
    public void The_side_list_is_300_on_a_tablet_held_upright_and_keeps_the_list_pane_elsewhere(double w, double h, double expected) =>
        Assert.Equal(expected, LandscapeLayout.SideListWidth(w, h));

    [Theory]
    [InlineData(804, 1164, true)]
    [InlineData(800, 1280, true)]
    [InlineData(600, 600, true)]
    [InlineData(600, 1000, true)]
    [InlineData(599.9, 1000, false)]
    [InlineData(1164, 804, false)]
    [InlineData(360, 760, false)]
    [InlineData(0, 0, false)]
    public void A_tablet_portrait_is_at_least_as_tall_as_wide_and_600_dp_wide(double w, double h, bool expected) =>
        Assert.Equal(expected, LandscapeLayout.IsTabletPortrait(w, h));

    [Theory]
    [InlineData(804, 4)]
    [InlineData(800, 4)]
    [InlineData(600, 3)]
    [InlineData(680, 3)]
    [InlineData(750, 3)]
    [InlineData(760, 4)]
    [InlineData(1100, 6)]
    [InlineData(2000, 6)]
    public void Tablet_portrait_shelf_columns_are_the_width_less_80_over_170_from_3_to_6(double w, int columns)
    {
        ShelfShape shelf = LandscapeLayout.ShelfLayout(w, w * 1.4);
        Assert.False(shelf.Sideways);
        Assert.True(shelf.TabletUpright);
        Assert.Equal(columns, shelf.Columns);
        Assert.Equal(40, shelf.SidePadding);
    }

    [Fact]
    public void A_phone_upright_and_a_tablet_held_sideways_keep_their_old_shelf_rules()
    {
        ShelfShape phone = LandscapeLayout.ShelfLayout(360, 760);
        ShelfShape sideways = LandscapeLayout.ShelfLayout(1164, 804);
        Assert.False(phone.TabletUpright);
        Assert.Equal(2, phone.Columns);
        Assert.Equal(14, phone.SidePadding);
        Assert.False(sideways.TabletUpright);
        Assert.Equal(6, sideways.Columns);
    }

    [Fact]
    public void A_square_600_dp_window_counts_as_tablet_portrait_with_three_columns()
    {
        Assert.Equal(3, LandscapeLayout.ShelfLayout(600, 600).Columns);
        Assert.Equal(52, LandscapeLayout.TabletHeaderTarget);
    }
}
