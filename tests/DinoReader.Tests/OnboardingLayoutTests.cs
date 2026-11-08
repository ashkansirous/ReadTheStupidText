using DinoReader.Core;

namespace DinoReader.Tests;

public class OnboardingLayoutTests
{
    [Fact]
    public void Gate_on_a_tablet_held_upright_is_a_520_dp_card_with_80_dp_keys_and_a_160_dp_ring()
    {
        GateShape shape = OnboardingLayout.Gate(804, 1164);
        Assert.True(shape.Card);
        Assert.False(shape.Split);
        Assert.Equal(520, shape.CardWidth);
        Assert.Equal(80, shape.KeyHeight);
        Assert.Equal(160, shape.RingSize);
    }

    [Theory]
    [InlineData(360, 760)]
    [InlineData(760, 360)]
    [InlineData(1164, 804)]
    public void Gate_keeps_the_phone_sizes_everywhere_else(double w, double h)
    {
        GateShape shape = OnboardingLayout.Gate(w, h);
        Assert.False(shape.Card);
        Assert.Equal(52, shape.KeyHeight);
        Assert.Equal(120, shape.RingSize);
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
    public void Gate_upright_is_the_sheet_with_52_dp_keys()
    {
        Assert.False(OnboardingLayout.Gate(360, 760).Split);
        Assert.Equal(52, OnboardingLayout.UprightKeyHeight);
    }

    [Theory]
    [InlineData(760, 360)]
    [InlineData(1164, 804)]
    [InlineData(640, 320)]
    public void Gate_sideways_splits(double w, double h) => Assert.True(OnboardingLayout.Gate(w, h).Split);

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
