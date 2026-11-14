using DinoReader.Core;

namespace DinoReader.Tests;

public class EditorLayoutTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_page_without_a_picture_has_none_in_any_orientation_focused_or_not(bool focused)
    {
        Assert.Equal(EditorPicture.None, EditorLayout.For(360, 760, false, focused).Picture);
        Assert.Equal(EditorPicture.None, EditorLayout.For(760, 360, false, focused).Picture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Held_upright_the_picture_stays_above_the_field(bool focused)
    {
        Assert.Equal(new EditorShape(EditorPicture.Above, 0), EditorLayout.For(360, 760, true, focused));
    }

    [Fact]
    public void Held_sideways_on_a_phone_without_focus_the_picture_is_a_left_column_fitted_to_the_height()
    {
        EditorShape shape = EditorLayout.For(760, 360, true, false);

        Assert.Equal(EditorPicture.Left, shape.Picture);
        Assert.Equal(270, shape.PictureWidth, 3);
    }

    [Fact]
    public void Held_sideways_on_a_phone_with_the_editor_focused_the_picture_is_a_64_dp_thumbnail()
    {
        Assert.Equal(new EditorShape(EditorPicture.Thumbnail, 64), EditorLayout.For(760, 360, true, true));
    }

    [Fact]
    public void The_thumbnail_does_not_depend_on_the_window_height()
    {
        Assert.Equal(EditorPicture.Thumbnail, EditorLayout.For(760, 160, true, true).Picture);
        Assert.Equal(EditorPicture.Thumbnail, EditorLayout.For(760, 599, true, true).Picture);
        Assert.Equal(EditorPicture.Left, EditorLayout.For(760, 160, true, false).Picture);
    }

    [Fact]
    public void A_tablet_held_sideways_keeps_the_left_column_even_with_the_editor_focused()
    {
        Assert.Equal(EditorPicture.Left, EditorLayout.For(1164, 804, true, true).Picture);
        Assert.Equal(EditorPicture.Left, EditorLayout.For(900, 600, true, true).Picture);
    }
}
