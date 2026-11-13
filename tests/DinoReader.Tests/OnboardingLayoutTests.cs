using DinoReader.Core;

namespace DinoReader.Tests;

public class OnboardingLayoutTests
{
    // Slice 363 (mock Missing States G1-G3): one centred card from 600 wide, the bottom sheet under it.
    [Theory]
    [InlineData(1100, 760)] // the Windows window of G1 and G2
    [InlineData(804, 1164)] // a tablet held upright
    [InlineData(1164, 804)] // a tablet held sideways
    [InlineData(600, 900)]
    [InlineData(800, 1280)]
    public void Gate_from_600_wide_and_tall_enough_is_the_440_dp_card(double w, double h)
    {
        GateShape shape = OnboardingLayout.Gate(w, h);
        Assert.Equal(GateMode.Card, shape.Mode);
        Assert.True(shape.Card);
        Assert.False(shape.Short);
        Assert.Equal(440, shape.CardWidth);
    }

    [Theory]
    [InlineData(852, 393)] // G3: a phone held sideways
    [InlineData(760, 360)]
    [InlineData(640, 320)]
    [InlineData(1100, 500)] // a short Windows window
    [InlineData(864, 405)]
    public void Gate_from_600_wide_but_short_is_the_470_dp_short_card(double w, double h)
    {
        GateShape shape = OnboardingLayout.Gate(w, h);
        Assert.Equal(GateMode.ShortCard, shape.Mode);
        Assert.True(shape.Short);
        Assert.Equal(470, shape.CardWidth);
    }

    [Theory]
    [InlineData(360, 760)]
    [InlineData(412, 915)]
    [InlineData(393, 852)]
    [InlineData(599, 1200)]
    public void Gate_under_600_wide_and_540_or_taller_keeps_the_bottom_sheet(double w, double h)
    {
        GateShape shape = OnboardingLayout.Gate(w, h);
        Assert.Equal(GateMode.Sheet, shape.Mode);
        Assert.False(shape.Card);
        Assert.Equal(0, shape.CardWidth);
    }

    [Theory]
    [InlineData(600, 900, 440)] // 600 - 48 = 552, so the drawn 440
    [InlineData(852, 393, 470)]
    [InlineData(500, 450, 452)] // narrow and short: the short card clamped to the window, not the sheet
    public void Gate_card_never_exceeds_the_window_less_48_dp(double w, double h, double expected)
    {
        double width = OnboardingLayout.Gate(w, h).CardWidth;
        Assert.Equal(expected, width);
        Assert.True(width <= w - 48);
    }

    [Fact]
    public void Gate_card_choice_changes_at_the_short_height_only()
    {
        Assert.Equal(GateMode.ShortCard, OnboardingLayout.Gate(1000, OnboardingLayout.GateShortBelowHeight - 1).Mode);
        Assert.Equal(GateMode.Card, OnboardingLayout.Gate(1000, OnboardingLayout.GateShortBelowHeight).Mode);
    }

    [Theory]
    [InlineData(804, TabletCardKind.Gate, 520)]
    [InlineData(804, TabletCardKind.BookSheet, 560)]
    [InlineData(600, TabletCardKind.Gate, 520)]
    [InlineData(600, TabletCardKind.BookSheet, 520)]
    [InlineData(100, TabletCardKind.Gate, 20)]
    [InlineData(50, TabletCardKind.Gate, 0)]
    public void A_card_is_the_window_less_80_dp_never_over_its_drawn_width(double w, TabletCardKind kind, double expected) =>
        Assert.Equal(expected, TabletCardLayout.CardWidth(w, kind));

    [Fact]
    public void Gate_sheet_keeps_52_dp_keys()
    {
        Assert.Equal(52, OnboardingLayout.UprightKeyHeight);
    }

    [Fact]
    public void Gate_sideways_key_bounds_are_40_to_64_dp()
    {
        Assert.Equal(40, OnboardingLayout.MinSidewaysKeyHeight);
        Assert.Equal(64, OnboardingLayout.SidewaysKeyHeight);
    }

    [Theory]
    [InlineData(0, 2, 0, 0)]
    [InlineData(1, 2, 0, 1)]
    [InlineData(2, 2, 1, 0)]
    [InlineData(2, 3, 0, 2)]
    [InlineData(3, 3, 1, 0)]
    [InlineData(13, 3, 4, 1)]
    [InlineData(5, 0, 5, 0)]
    public void Cell_of_fills_row_by_row(int index, int columns, int row, int column) =>
        Assert.Equal(new GridCell(row, column), OnboardingLayout.CellOf(index, columns));

    [Theory]
    [InlineData(14, 2, 7)]
    [InlineData(14, 3, 5)]
    [InlineData(0, 3, 0)]
    [InlineData(6, 3, 2)]
    public void Rows_for_rounds_up(int count, int columns, int rows) =>
        Assert.Equal(rows, OnboardingLayout.RowsFor(count, columns));
}
