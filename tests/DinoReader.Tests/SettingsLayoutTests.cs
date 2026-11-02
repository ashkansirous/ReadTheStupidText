using DinoReader.Core;

namespace DinoReader.Tests;

public class SettingsLayoutTests
{
    [Theory]
    [InlineData(360, 760, 1)]
    [InlineData(804, 1164, 1)]
    [InlineData(760, 360, 2)]
    [InlineData(1164, 804, 3)]
    [InlineData(1000, 599, 2)]
    [InlineData(1000, 600, 3)]
    public void Store_columns_are_one_upright_two_on_a_phone_sideways_three_on_a_tablet_sideways(double w, double h, int expected) =>
        Assert.Equal(expected, SettingsLayout.StoreColumns(w, h));

    [Fact]
    public void Slim_store_cards_have_a_52_dp_cover_and_upright_ones_keep_66_by_88()
    {
        Assert.Equal(52, SettingsLayout.StoreCover(2).Width);
        Assert.Equal(52, SettingsLayout.StoreCover(3).Width);
        Assert.Equal(new CoverSize(66, 88), SettingsLayout.StoreCover(1));
        Assert.True(SettingsLayout.SlimStoreCards(2));
        Assert.False(SettingsLayout.SlimStoreCards(1));
    }

    [Theory]
    [InlineData(760, 360, true)]
    [InlineData(1164, 804, true)]
    [InlineData(360, 760, false)]
    [InlineData(599, 900, false)]
    [InlineData(743.9, 1164, false)]
    [InlineData(744, 1164, true)]
    [InlineData(804, 1164, true)]
    public void Settings_has_two_panes_sideways_or_on_a_tablet_744_dp_wide_upright(double w, double h, bool expected) =>
        Assert.Equal(expected, SettingsLayout.TwoPanes(w, h));

    [Fact]
    public void The_right_pane_is_capped_at_640_on_a_tablet_and_takes_the_rest_on_a_phone()
    {
        Assert.Equal(640, SettingsLayout.PaneMaxWidth(1164, 804));
        Assert.True(double.IsPositiveInfinity(SettingsLayout.PaneMaxWidth(760, 360)));
        Assert.Equal(640, SettingsLayout.PaneWidth(1164, 804)); // 1164 - 360 = 804, capped
        Assert.Equal(460, SettingsLayout.PaneWidth(760, 360)); // 760 - 300
        Assert.Equal(0, SettingsLayout.PaneWidth(360, 760));
    }

    [Fact]
    public void A_tablet_upright_at_744_dp_has_a_pane_that_fits_beside_the_list()
    {
        Assert.Equal(384, SettingsLayout.PaneWidth(744, 1133)); // 744 - 360
    }

    [Theory]
    [InlineData(760, 360, true)]
    [InlineData(1164, 804, true)]
    [InlineData(360, 760, false)]
    [InlineData(804, 1164, false)]
    public void Backup_cards_sit_side_by_side_only_when_sideways(double w, double h, bool expected) =>
        Assert.Equal(expected, SettingsLayout.BackupSideBySide(w, h));
}
