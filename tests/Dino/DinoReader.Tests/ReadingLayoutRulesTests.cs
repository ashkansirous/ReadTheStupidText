using DinoReader.Core;
using Xunit;

namespace DinoReader.Tests;

/// <summary>The reading screen's sideways margins and the tablet title (Slice 242 item 6, mock Batch 44 frames 113, 114, 127).</summary>
public class ReadingLayoutRulesTests
{
    [Fact]
    public void Phone_with_a_rail_keeps_34_left_and_lets_the_rail_carry_the_right_inset()
    {
        ReadingLayoutRules.SurfaceInsets inset = ReadingLayoutRules.SidewaysSurface(800, 360, ControlsPlacement.Rail);

        Assert.Equal(new ReadingLayoutRules.SurfaceInsets(34, 6, 0, 8), inset);
    }

    [Fact]
    public void Phone_with_a_row_keeps_34_left_and_26_right()
    {
        ReadingLayoutRules.SurfaceInsets inset = ReadingLayoutRules.SidewaysSurface(800, 360, ControlsPlacement.Row);

        Assert.Equal(new ReadingLayoutRules.SurfaceInsets(34, 6, 26, 8), inset);
    }

    [Fact]
    public void Tablet_keeps_14_each_side()
    {
        ReadingLayoutRules.SurfaceInsets inset = ReadingLayoutRules.SidewaysSurface(1180, 820, ControlsPlacement.Row);

        Assert.Equal(new ReadingLayoutRules.SurfaceInsets(14, 6, 14, 8), inset);
    }

    [Theory]
    [InlineData(ReadingLayout.PictureOverlay, ControlsPlacement.Row, true, true, true, true)]
    [InlineData(ReadingLayout.PictureOverlay, ControlsPlacement.Rail, true, true, true, false)]
    [InlineData(ReadingLayout.PictureOverlay, ControlsPlacement.Row, false, true, true, false)]
    [InlineData(ReadingLayout.PictureOverlay, ControlsPlacement.Row, true, false, true, false)]
    [InlineData(ReadingLayout.PictureOverlay, ControlsPlacement.Row, true, true, false, false)]
    [InlineData(ReadingLayout.PictureSide, ControlsPlacement.Row, true, true, true, false)]
    [InlineData(ReadingLayout.TextRow, ControlsPlacement.Row, true, true, true, false)]
    [InlineData(ReadingLayout.Upright, ControlsPlacement.Row, true, true, true, false)]
    public void Tablet_title_shows_only_over_a_drawn_card_beside_a_picture_with_a_row_and_a_title(ReadingLayout layout, ControlsPlacement controls, bool pictureBesideCard, bool drawsCard, bool hasTitle, bool expected)
    {
        Assert.Equal(expected, ReadingLayoutRules.ShowsTabletTitle(layout, controls, pictureBesideCard, drawsCard, hasTitle));
    }

    [Fact]
    public void Card_is_lowered_by_the_title_line_and_gap_only_when_the_title_is_shown()
    {
        Assert.Equal(56, ReadingLayoutRules.CardTop(true, ReadingLayoutRules.TabletTitleLine));
        Assert.Equal(0, ReadingLayoutRules.CardTop(false, ReadingLayoutRules.TabletTitleLine));
    }

    [Fact]
    public void Card_follows_a_title_line_grown_by_the_font_scale()
    {
        double line = ReadingLayoutRules.TitleLine(48);

        Assert.Equal(48, line);
        Assert.Equal(68, ReadingLayoutRules.CardTop(true, line));
        Assert.Equal(ReadingLayoutRules.TabletTitleLine, ReadingLayoutRules.TitleLine(0));
    }

    [Theory]
    [InlineData(804, 1164, ReadingMode.Text, false, ReadingLayout.Stacked)]
    [InlineData(804, 1164, ReadingMode.Text, true, ReadingLayout.Stacked)]
    [InlineData(804, 1164, ReadingMode.PictureAndText, true, ReadingLayout.Stacked)]
    [InlineData(804, 1164, ReadingMode.PictureOnly, true, ReadingLayout.Stacked)]
    [InlineData(800, 1280, ReadingMode.PictureAndText, true, ReadingLayout.Stacked)]
    [InlineData(804, 1164, ReadingMode.PictureOnly, false, ReadingLayout.Upright)]
    [InlineData(804, 1164, ReadingMode.PictureAndText, false, ReadingLayout.Upright)]
    [InlineData(360, 760, ReadingMode.Text, true, ReadingLayout.Upright)]
    [InlineData(599, 1000, ReadingMode.PictureAndText, true, ReadingLayout.Upright)]
    [InlineData(1164, 804, ReadingMode.PictureAndText, true, ReadingLayout.PictureOverlay)]
    [InlineData(1164, 804, ReadingMode.Text, true, ReadingLayout.TextRow)]
    [InlineData(760, 360, ReadingMode.PictureOnly, true, ReadingLayout.PictureSide)]
    public void A_tablet_held_upright_stacks_text_and_picture_text_books_only(double w, double h, ReadingMode mode, bool pictureText, ReadingLayout expected) =>
        Assert.Equal(expected, ReadingLayoutRules.For(w, h, mode, pictureText));

    [Fact]
    public void Rotating_a_tablet_flips_the_shape_both_ways()
    {
        Assert.Equal(ReadingLayout.Stacked, ReadingLayoutRules.For(804, 1164, ReadingMode.PictureAndText, true));
        Assert.Equal(ReadingLayout.PictureOverlay, ReadingLayoutRules.For(1164, 804, ReadingMode.PictureAndText, true));
        Assert.Equal(ReadingLayout.Stacked, ReadingLayoutRules.For(804, 1164, ReadingMode.PictureAndText, true));
    }

    [Theory]
    [InlineData(1016, 537.6)]
    [InlineData(1196, 624)]
    public void The_stacked_picture_panel_is_48_percent_of_the_surface_plus_the_controls_band(double surface, double expected) =>
        Assert.Equal(expected, ReadingLayoutRules.StackedPictureHeight(surface), 1);

    [Fact]
    public void A_short_surface_leaves_the_text_card_120_dp_at_least()
    {
        double surface = 300;

        Assert.Equal(surface - ReadingLayoutRules.StackedGap - ReadingLayoutRules.StackedMinCard, ReadingLayoutRules.StackedPictureHeight(surface), 6);
        Assert.Equal(0, ReadingLayoutRules.StackedPictureHeight(0));
    }

    [Theory]
    [InlineData(600, 600, true)]
    [InlineData(599, 599, false)]
    [InlineData(650, 650, true)]
    [InlineData(600, 599, false)]
    public void The_tablet_upright_boundary_is_height_at_least_width_and_width_at_least_600(double w, double h, bool tabletUpright)
    {
        Assert.Equal(tabletUpright, ReadingLayoutRules.For(w, h, ReadingMode.Text, true) == ReadingLayout.Stacked);
    }

    [Fact]
    public void Stacked_fits_only_when_the_surface_holds_the_gap_and_the_card_with_a_picture_above()
    {
        double edge = ReadingLayoutRules.StackedTop + ReadingLayoutRules.StackedControlsBand + ReadingLayoutRules.StackedGap + ReadingLayoutRules.StackedMinCard;

        Assert.False(ReadingLayoutRules.StackedFits(edge));
        Assert.True(ReadingLayoutRules.StackedFits(edge + 1));
        Assert.True(ReadingLayoutRules.StackedFits(600));
        Assert.True(ReadingLayoutRules.StackedPictureHeight(600 - ReadingLayoutRules.StackedTop - ReadingLayoutRules.StackedControlsBand) > 0);
    }

    [Fact]
    public void Stacked_text_is_36_sp_alone_and_32_sp_beside_a_picture()
    {
        Assert.Equal(36, ReadingLayoutRules.StackedTextSizeFor(ReadingMode.Text));
        Assert.Equal(32, ReadingLayoutRules.StackedTextSizeFor(ReadingMode.PictureAndText));
        Assert.Equal(32, ReadingLayoutRules.StackedTextSizeFor(ReadingMode.PictureOnly));
    }

    [Fact]
    public void The_stacked_surface_keeps_40_dp_at_the_sides_and_the_chip_sits_12_dp_in_from_the_corner()
    {
        Assert.Equal(new ReadingLayoutRules.SurfaceInsets(40, 6, 40, 0), ReadingLayoutRules.StackedSurface);
        Assert.Equal(new ReadingLayoutRules.SurfaceInsets(52, 18, 0, 0), ReadingLayoutRules.StackedChip);
    }

    [Fact]
    public void The_tablet_title_is_never_shown_when_stacked() =>
        Assert.False(ReadingLayoutRules.ShowsTabletTitle(ReadingLayout.Stacked, ControlsPlacement.Row, true, true, true));
}
