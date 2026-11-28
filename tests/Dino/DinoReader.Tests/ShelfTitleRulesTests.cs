using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 325 part C: the kids' shelf of the 2026-11-08 mock (frames K1-K5, KL1, KT1, KT2, KW1): title give-way, columns and sizes.
public class ShelfTitleRulesTests
{
    [Theory]
    [InlineData(200, 120, 70, ShelfTitle.Full)]
    [InlineData(120, 120, 70, ShelfTitle.Full)]
    [InlineData(119, 120, 70, ShelfTitle.Short)]
    [InlineData(70, 120, 70, ShelfTitle.Short)]
    [InlineData(69, 120, 70, ShelfTitle.Hidden)]
    [InlineData(0, 120, 70, ShelfTitle.Hidden)]
    public void The_title_gives_way_full_then_short_then_hidden(double available, double full, double shortWidth, ShelfTitle expected) =>
        Assert.Equal(expected, ShelfTitleRules.Fit(available, full, shortWidth));

    [Fact]
    public void At_375_with_the_flag_English_Stories_fits_but_My_stories_does_not()
    {
        ShelfSizes k1 = ShelfTitleRules.SizesOf(CornerLayout.ChromeOf(375, 667, false));
        double pill = 130; // lock, "Grown-ups", padding
        double room = ShelfTitleRules.AvailableForTitle(375, k1.Header.Left + k1.Header.Right, k1.Icon, k1.FlagButton, pill, k1.HeaderGap);

        Assert.Equal(ShelfTitle.Short, ShelfTitleRules.Fit(room, 110, 70)); // "My stories" ~110, "Stories" ~70 in 22 pt
    }

    [Fact]
    public void At_375_with_the_flag_German_Fur_Erwachsene_hides_the_title()
    {
        ShelfSizes k1 = ShelfTitleRules.SizesOf(CornerLayout.ChromeOf(375, 667, false));
        double pill = 170; // lock, "Für Erwachsene", padding
        double room = ShelfTitleRules.AvailableForTitle(375, k1.Header.Left + k1.Header.Right, k1.Icon, k1.FlagButton, pill, k1.HeaderGap);

        Assert.Equal(ShelfTitle.Hidden, ShelfTitleRules.Fit(room, 160, 120)); // "Meine Geschichten", "Geschichten"
    }

    [Theory]
    [InlineData(393)]
    [InlineData(440)]
    [InlineData(412)]
    public void Wider_phones_leave_more_room_for_the_title(double width)
    {
        ShelfSizes narrow = ShelfTitleRules.SizesOf(CornerLayout.ChromeOf(375, 667, false));
        ShelfSizes wide = ShelfTitleRules.SizesOf(CornerLayout.ChromeOf(width, 900, false));
        double narrowRoom = ShelfTitleRules.AvailableForTitle(375, narrow.Header.Left + narrow.Header.Right, narrow.Icon, narrow.FlagButton, 130, narrow.HeaderGap);
        double wideRoom = ShelfTitleRules.AvailableForTitle(width, wide.Header.Left + wide.Header.Right, wide.Icon, wide.FlagButton, 130, wide.HeaderGap);

        Assert.True(wideRoom > narrowRoom);
    }

    [Theory]
    [InlineData(0)] // measured before the pill has a native view (Slice 387)
    [InlineData(45)] // just its padding and icon (11 + 14 + 20)
    public void An_unmeasured_pill_is_estimated_so_the_title_does_not_take_its_room(double measured)
    {
        double pill = ShelfTitleRules.PillWidth(0, measured, 9, 15, 11, 14, 20, 6); // "Grown-ups", not laid out yet
        double room = ShelfTitleRules.AvailableForTitle(393, 30, 44, 48, pill, 10);

        Assert.True(pill >= 11 + 14 + 20 + 6 + (9 * 15 * 0.6));
        Assert.Equal(ShelfTitle.Short, ShelfTitleRules.Fit(room, 150, 80));
    }

    [Fact]
    public void A_real_measure_is_kept_even_when_it_is_smaller_than_the_estimate()
    {
        Assert.Equal(69, ShelfTitleRules.PillWidth(0, 69, 9, 15, 11, 14, 20, 6)); // the estimate would be 132
        Assert.Equal(300, ShelfTitleRules.PillWidth(0, 300, 9, 15, 11, 14, 20, 6));
    }

    [Theory]
    [InlineData(80, 0)]
    [InlineData(80, 45)]
    [InlineData(80, 69)]
    public void The_laid_out_width_wins_over_a_smaller_or_unreal_measure(double laidOut, double measured)
    {
        Assert.Equal(laidOut, ShelfTitleRules.PillWidth(laidOut, measured, 9, 15, 11, 14, 20, 6));
    }

    [Fact]
    public void A_squeezed_layout_narrower_than_the_real_measure_gives_the_measure()
    {
        Assert.Equal(132, ShelfTitleRules.PillWidth(60, 132, 9, 15, 11, 14, 20, 6));
    }

    [Theory]
    [InlineData(-1)] // MAUI's width before the first layout
    [InlineData(0)]
    public void A_pill_not_laid_out_yet_falls_back_to_the_measure(double laidOut)
    {
        Assert.Equal(69, ShelfTitleRules.PillWidth(laidOut, 69, 9, 15, 11, 14, 20, 6));
    }

    [Fact]
    public void The_title_slack_decides_a_title_exactly_as_wide_as_its_room()
    {
        Assert.Equal(ShelfTitle.Full, ShelfTitleRules.Fit(100, 100, 60));
        Assert.Equal(ShelfTitle.Short, ShelfTitleRules.Fit(100, 100 + ShelfTitleRules.TitleSlack, 60));
    }

    [Fact]
    public void Without_a_flag_the_title_has_the_flags_room()
    {
        double with = ShelfTitleRules.AvailableForTitle(375, 28, 40, 44, 130, 10);
        double without = ShelfTitleRules.AvailableForTitle(375, 28, 40, 0, 130, 10);

        Assert.Equal(44 + 10, without - with);
    }

    [Theory]
    [InlineData(375, 667, 2)] // K1
    [InlineData(393, 852, 2)] // K2
    [InlineData(440, 956, 2)] // K3
    [InlineData(412, 915, 2)] // K4
    [InlineData(360, 800, 2)] // the S21 (Slice 465: the subscription flow expected 3 columns on a phone; phone portrait is always 2)
    [InlineData(852, 393, 4)] // KL1
    [InlineData(640, 360, 2)]
    [InlineData(820, 1180, 3)] // KT1
    [InlineData(1180, 820, 4)] // KT2
    public void The_shelf_has_the_columns_each_frame_draws(double w, double h, int columns)
    {
        ChromeShape shape = CornerLayout.ChromeOf(w, h, windows: false);

        Assert.Equal(columns, ShelfTitleRules.Columns(shape, w));
    }

    [Theory]
    [InlineData(1100, 4)] // KW1
    [InlineData(800, 4)]
    [InlineData(1920, 6)]
    public void The_Windows_shelf_is_four_columns_and_more_on_a_wide_window(double w, int columns)
    {
        ChromeShape shape = CornerLayout.ChromeOf(w, 760, windows: true);

        Assert.Equal(columns, ShelfTitleRules.Columns(shape, w));
    }

    [Fact]
    public void The_header_sizes_follow_frames_K1_to_K4()
    {
        ShelfSizes k1 = ShelfTitleRules.SizesOf(CornerLayout.ChromeOf(375, 667, false));
        ShelfSizes k2 = ShelfTitleRules.SizesOf(CornerLayout.ChromeOf(393, 852, false));
        ShelfSizes k3 = ShelfTitleRules.SizesOf(CornerLayout.ChromeOf(440, 956, false));
        ShelfSizes k4 = ShelfTitleRules.SizesOf(CornerLayout.ChromeOf(412, 915, false));

        Assert.Equal([40d, 44d, 48d, 44d], [k1.Icon, k2.Icon, k3.Icon, k4.Icon]);
        Assert.Equal([22d, 22d, 24d, 22d], [k1.Title, k2.Title, k3.Title, k4.Title]);
        Assert.Equal([44d, 44d, 48d, 48d], [k1.FlagButton, k2.FlagButton, k3.FlagButton, k4.FlagButton]);
        Assert.Equal([26d, 26d, 30d, 30d], [k1.Flag, k2.Flag, k3.Flag, k4.Flag]);
        Assert.Equal([44d, 44d, 48d, 48d], [k1.PillHeight, k2.PillHeight, k3.PillHeight, k4.PillHeight]);
    }

    [Fact]
    public void The_pill_is_as_tall_in_the_shelf_as_the_coral_pill_is_in_the_corner_so_going_in_and_coming_out_is_the_same_tap()
    {
        foreach ((double w, double h, bool windows) in new[] { (375d, 667d, false), (393d, 852d, false), (440d, 956d, false), (412d, 915d, false), (852d, 393d, false), (820d, 1180d, false), (1180d, 820d, false), (1100d, 760d, true) })
        {
            ChromeShape shape = CornerLayout.ChromeOf(w, h, windows);

            Assert.Equal(CornerLayout.SizesOf(shape).PillHeight, ShelfTitleRules.SizesOf(shape).PillHeight);
            Assert.Equal(CornerLayout.SizesOf(shape).RailWidth, ShelfTitleRules.SizesOf(shape).RailWidth);
        }
    }

    [Fact]
    public void Covers_are_round_16_and_18_on_a_tablet_held_upright()
    {
        Assert.Equal(16, ShelfTitleRules.SizesOf(CornerLayout.ChromeOf(375, 667, false)).CoverRadius);
        Assert.Equal(18, ShelfTitleRules.SizesOf(CornerLayout.ChromeOf(820, 1180, false)).CoverRadius);
        Assert.Equal(5, ShelfTitleRules.SizesOf(CornerLayout.ChromeOf(820, 1180, false)).ShadowOffset);
    }

    [Fact]
    public void The_shelf_geometry_beside_a_rail_has_the_content_padding()
    {
        ChromeShape kt1 = CornerLayout.ChromeOf(820, 1180, false);

        WindowShapeSeam.ShelfGeometry geometry = ShelfTitleRules.Geometry(kt1, 3);

        Assert.Equal((28d, 40d, 32d, 3), (geometry.Left, geometry.Top, geometry.Right, geometry.Columns));
        Assert.Equal((18d, 28d), (geometry.GapX, geometry.GapY));
    }
}
