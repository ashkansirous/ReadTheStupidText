using DinoReader.Core;

namespace DinoReader.Tests;

public class SettingsLayoutTests
{
    // Decision 4 (Slice 325 part B): one column of store cards below 800 dp of content, two from 800.
    [Theory]
    [InlineData(0, 1)]
    [InlineData(375, 1)]
    [InlineData(560, 1)]  // tablet portrait 820 less the 260 rail
    [InlineData(602, 1)]  // phone landscape 852 less the 250 rail
    [InlineData(799.9, 1)]
    [InlineData(800, 2)]
    [InlineData(820, 2)]  // Windows 1100 less the 280 rail
    [InlineData(890, 2)]  // tablet landscape 1180 less the 290 rail (frame T2)
    public void The_store_has_one_column_below_800_dp_of_content_and_two_from_800(double content, int expected) =>
        Assert.Equal(expected, SettingsLayout.StoreColumns(content));

    [Theory]
    [InlineData(1180, 290, 890)]
    [InlineData(1100, 280, 820)]
    [InlineData(393, 0, 393)]
    [InlineData(200, 280, 0)]
    public void The_content_column_is_the_window_less_the_rail(double window, double rail, double expected) =>
        Assert.Equal(expected, SettingsLayout.ContentWidth(window, rail));

    [Fact]
    public void A_store_cover_is_76_dp_wide_and_3_to_4()
    {
        Assert.Equal(76, SettingsLayout.StoreCover.Width);
        Assert.Equal(76 * 4.0 / 3.0, SettingsLayout.StoreCover.Height, 6);
    }

    [Fact]
    public void The_settings_list_is_one_column_of_at_most_620_dp_and_the_store_cards_are_14_dp_apart()
    {
        Assert.Equal(620, SettingsLayout.MaxContentWidth);
        Assert.Equal(14, SettingsLayout.StoreCardGap);
    }

    [Theory]
    [InlineData(760, 360, true)]
    [InlineData(1164, 804, true)]
    [InlineData(360, 760, false)]
    [InlineData(804, 1164, false)]
    public void Backup_cards_sit_side_by_side_only_when_sideways(double w, double h, bool expected) =>
        Assert.Equal(expected, SettingsLayout.BackupSideBySide(w, h));

    [Theory]
    [InlineData(559.9, false)]
    [InlineData(560, true)]
    [InlineData(0, false)]
    public void Backup_cards_need_a_content_column_of_560_dp(double contentWidth, bool expected) =>
        Assert.Equal(expected, SettingsLayout.BackupCardsFit(contentWidth));

    [Fact]
    public void A_phone_held_sideways_at_914_by_411_keeps_the_backup_cards_side_by_side()
    {
        ChromeShape chrome = CornerLayout.ChromeOf(914, 411, false);
        Assert.True(SettingsLayout.BackupCardsFit(CornerLayout.ContentWidth(chrome, 914)));
    }

    [Fact]
    public void A_phone_held_sideways_at_800_by_360_keeps_the_backup_cards_stacked()
    {
        ChromeShape chrome = CornerLayout.ChromeOf(800, 360, false);
        Assert.False(SettingsLayout.BackupCardsFit(CornerLayout.ContentWidth(chrome, 800)));
    }
}
