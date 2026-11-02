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
}
