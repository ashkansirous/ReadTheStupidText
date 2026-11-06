using DinoReader.Core;

namespace DinoReader.Tests;

public class TabletPageLayoutTests
{
    [Theory]
    [InlineData(804, 1164, true)]
    [InlineData(800, 800, true)]
    [InlineData(600, 1000, true)]
    [InlineData(599, 1000, false)]
    [InlineData(1164, 804, false)]
    [InlineData(360, 760, false)]
    public void The_rules_apply_exactly_on_a_tablet_held_upright(double w, double h, bool expected) =>
        Assert.Equal(expected, TabletPageLayout.Applies(w, h));

    [Fact]
    public void Store_has_two_columns_90_dp_covers_and_the_action_under_the_card_on_a_tablet_upright()
    {
        StoreCardShape shape = TabletPageLayout.StoreCard(804, 1164);

        Assert.Equal(2, shape.Columns);
        Assert.Equal(new CoverSize(90, 120), shape.Cover);
        Assert.False(shape.Slim);
        Assert.True(shape.ActionBelow);
    }

    [Theory]
    [InlineData(360, 760)]
    [InlineData(760, 360)]
    [InlineData(1164, 804)]
    [InlineData(599, 1000)]
    public void Store_keeps_settings_shapes_on_every_other_window(double w, double h)
    {
        int columns = SettingsLayout.StoreColumns(w, h);

        Assert.Equal(new StoreCardShape(columns, SettingsLayout.StoreCover(columns), SettingsLayout.SlimStoreCards(columns), false), TabletPageLayout.StoreCard(w, h));
        Assert.Equal(columns, TabletPageLayout.StoreColumns(w, h));
    }

    [Theory]
    [InlineData(804, 1164, 2)]
    [InlineData(360, 760, 1)]
    [InlineData(760, 360, 2)]
    [InlineData(1164, 804, 2)]
    public void Cast_is_two_columns_on_a_tablet_upright_and_unchanged_elsewhere(double w, double h, int expected) =>
        Assert.Equal(expected, TabletPageLayout.CastColumns(w, h));

    [Theory]
    [InlineData(804, 1164, 560)]
    [InlineData(600, 1000, 520)]
    [InlineData(620, 1000, 540)]
    [InlineData(360, 760, 0)]
    [InlineData(1164, 804, 0)]
    public void The_cast_picker_is_a_card_of_width_min_window_less_80_and_560_on_a_tablet_upright(double w, double h, double expected) =>
        Assert.Equal(expected, TabletPageLayout.PickerWidth(w, h));

    [Theory]
    [InlineData(804, 1164, true)]
    [InlineData(360, 760, false)]
    [InlineData(760, 360, true)]
    [InlineData(1164, 804, true)]
    public void Backup_cards_sit_side_by_side_sideways_or_on_a_tablet_upright(double w, double h, bool expected) =>
        Assert.Equal(expected, TabletPageLayout.BackupSideBySide(w, h));

    [Fact]
    public void The_editor_picture_is_440_dp_and_a_120_dp_strip_while_the_field_has_focus_on_a_tablet_upright()
    {
        Assert.Equal(new EditorPictureBand(440, false), TabletPageLayout.EditorPicture(804, 1164, true, false));
        Assert.Equal(new EditorPictureBand(120, true), TabletPageLayout.EditorPicture(804, 1164, true, true));
    }

    [Theory]
    [InlineData(804, 1164, false, false)]
    [InlineData(360, 760, true, false)]
    [InlineData(760, 360, true, true)]
    public void The_editor_picture_band_is_absent_without_a_picture_and_off_a_tablet_upright(double w, double h, bool hasPicture, bool focused) =>
        Assert.Null(TabletPageLayout.EditorPicture(w, h, hasPicture, focused));

    [Theory]
    [InlineData(600, 700, 214, 120)]
    [InlineData(600, 800, 264, 120)]
    [InlineData(804, 1164, 440, 120)]
    public void The_editor_picture_is_capped_to_half_the_body_and_the_strip_is_120_dp(double w, double h, double band, double strip)
    {
        Assert.Equal(band, TabletPageLayout.EditorPicture(w, h, true, false)!.Value.Height);
        Assert.Equal(strip, TabletPageLayout.EditorPicture(w, h, true, true)!.Value.Height);
    }

    [Theory]
    [InlineData(400, false, 200)]
    [InlineData(1000, false, 440)]
    [InlineData(1000, true, 120)]
    [InlineData(100, true, 120)]
    public void One_rule_gives_the_picture_height_from_the_body_height_and_the_field_focus(double body, bool focused, double expected) =>
        Assert.Equal(expected, TabletPageLayout.PictureHeight(440, body, focused));

    [Fact]
    public void The_editor_art_is_fitted_inside_the_band_less_padding_and_the_helper_line()
    {
        Assert.Equal(440 - 16 - 40, TabletPageLayout.EditorArtHeight(new EditorPictureBand(440, false)));
        Assert.Equal(104, TabletPageLayout.EditorArtHeight(new EditorPictureBand(120, true)));
        Assert.Equal(0, TabletPageLayout.EditorArtHeight(new EditorPictureBand(10, true)));
    }

    [Fact]
    public void Pages_keep_40_dp_sides_on_a_tablet_upright() =>
        Assert.Equal(40, TabletPageLayout.PageSideMargin);
}
