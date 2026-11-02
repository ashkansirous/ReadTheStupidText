using DinoReader.Core;

namespace DinoReader.Tests;

public class RecordLayoutTests
{
    [Fact]
    public void Held_upright_the_screen_is_the_stack_it_always_was()
    {
        RecordShape shape = RecordLayout.For(360, 760, 0.75);

        Assert.Equal(new RecordShape(RecordColumns.Stacked, 0, 0), shape);
    }

    [Fact]
    public void A_phone_held_sideways_has_three_columns_with_the_picture_fitted_to_the_full_height()
    {
        RecordShape shape = RecordLayout.For(760, 360, 0.75);

        Assert.Equal(RecordColumns.Three, shape.Columns);
        Assert.Equal(270, shape.PicturePane, 3);
        Assert.Equal(RecordLayout.PhoneRailWidth, shape.RailWidth);
    }

    [Fact]
    public void A_tablet_held_sideways_has_two_columns_and_no_rail()
    {
        RecordShape shape = RecordLayout.For(1164, 804, 0.75);

        Assert.Equal(RecordColumns.Two, shape.Columns);
        Assert.Equal(1164 * LandscapeLayout.MaxPictureShare, shape.PicturePane, 3);
        Assert.Equal(0, shape.RailWidth);
    }

    [Fact]
    public void A_wide_picture_is_capped_at_45_percent_of_the_window()
    {
        RecordShape shape = RecordLayout.For(900, 360, 2.0);

        Assert.Equal(900 * LandscapeLayout.MaxPictureShare, shape.PicturePane, 3);
    }

    [Fact]
    public void A_page_without_a_picture_has_no_picture_column()
    {
        Assert.Equal(0, RecordLayout.For(760, 360, null).PicturePane);
        Assert.Equal(0, RecordLayout.For(1164, 804, null).PicturePane);
    }

    [Fact]
    public void A_picture_of_unknown_shape_is_fitted_as_three_by_four()
    {
        Assert.Equal(270, RecordLayout.For(760, 360, 0).PicturePane, 3);
    }

    [Fact]
    public void A_narrow_phone_gives_the_middle_column_its_minimum_by_shrinking_the_picture()
    {
        RecordShape shape = RecordLayout.For(640, 360, 16.0 / 9);
        double middle = 640 - RecordLayout.PhoneStartPadding - RecordLayout.PhoneEndPadding - RecordLayout.PhoneRailWidth - 2 * RecordLayout.ColumnGap - shape.PicturePane;

        Assert.Equal(RecordLayout.MinMiddleWidth, middle, 3);
        Assert.True(shape.PicturePane < 288);
    }

    [Theory]
    [InlineData(520, 360)]
    [InlineData(640, 360)]
    [InlineData(760, 360)]
    public void On_a_phone_the_middle_column_keeps_its_minimum_after_paddings_and_gaps(double w, double h)
    {
        RecordShape shape = RecordLayout.For(w, h, 0.75);
        double gaps = RecordLayout.ColumnGap + (shape.PicturePane > 0 ? RecordLayout.ColumnGap : 0);
        double middle = w - RecordLayout.PhoneStartPadding - RecordLayout.PhoneEndPadding - RecordLayout.PhoneRailWidth - gaps - shape.PicturePane;

        Assert.True(middle >= RecordLayout.MinMiddleWidth - 0.001, $"middle {middle}");
    }

    [Fact]
    public void With_no_room_for_a_picture_its_column_is_zero_wide_not_a_gap()
    {
        Assert.Equal(0, RecordLayout.For(400, 360, 0.75).PicturePane);
    }

    [Fact]
    public void A_page_without_a_picture_is_not_capped_on_a_narrow_phone()
    {
        Assert.Equal(0, RecordLayout.For(480, 320, null).PicturePane);
    }

    [Theory]
    [InlineData(RecordColumns.Three, 19)]
    [InlineData(RecordColumns.Two, 28)]
    [InlineData(RecordColumns.Stacked, 20)]
    public void The_words_size_follows_the_shape(RecordColumns columns, double expected) =>
        Assert.Equal(expected, RecordLayout.WordsSize(columns));

    [Theory]
    [InlineData(760, 360, 2)]
    [InlineData(1164, 804, 2)]
    [InlineData(360, 760, 1)]
    [InlineData(804, 1164, 1)]
    public void Cast_rows_are_two_columns_only_held_sideways(double w, double h, int expected) =>
        Assert.Equal(expected, LandscapeLayout.CastColumns(w, h));
}
