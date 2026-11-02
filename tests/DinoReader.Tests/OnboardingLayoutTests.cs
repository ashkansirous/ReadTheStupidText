using DinoReader.Core;

namespace DinoReader.Tests;

public class OnboardingLayoutTests
{
    [Theory]
    [InlineData(760, 360, true, 300)]
    [InlineData(1164, 804, true, 360)]
    [InlineData(360, 760, false, 0)]
    [InlineData(804, 1164, false, 0)]
    public void Intro_splits_only_when_sideways_with_a_300_or_360_dp_picture_panel(double w, double h, bool split, double panel)
    {
        IntroShape shape = OnboardingLayout.Intro(w, h);
        Assert.Equal(split, shape.Split);
        Assert.Equal(panel, shape.PicturePanel);
        Assert.Equal(split, shape.ButtonsInRow);
    }

    [Fact]
    public void Intro_copy_is_capped_at_460_on_a_tablet_in_either_orientation_and_free_on_a_phone()
    {
        Assert.Equal(460, OnboardingLayout.Intro(1164, 804).TextMaxWidth);
        Assert.Equal(460, OnboardingLayout.Intro(804, 1164).TextMaxWidth);
        Assert.True(double.IsPositiveInfinity(OnboardingLayout.Intro(760, 360).TextMaxWidth));
        Assert.True(double.IsPositiveInfinity(OnboardingLayout.Intro(360, 760).TextMaxWidth));
    }

    [Fact]
    public void Intro_on_a_tablet_held_upright_stacks_the_picture_above_the_copy()
    {
        IntroShape shape = OnboardingLayout.Intro(804, 1164);
        Assert.False(shape.Split);
        Assert.False(shape.ButtonsInRow);
    }

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
    [InlineData(760, 360, true, 3)]
    [InlineData(1164, 804, true, 3)]
    [InlineData(360, 760, false, 2)]
    [InlineData(804, 1164, false, 2)]
    public void First_run_has_three_language_columns_sideways_and_two_upright(double w, double h, bool split, int columns)
    {
        FirstRunShape shape = OnboardingLayout.FirstRun(w, h);
        Assert.Equal(split, shape.Split);
        Assert.Equal(columns, shape.LanguageColumns);
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
