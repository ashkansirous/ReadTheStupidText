using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 403 items 1 and 6: the fold rules, and the regression pins that every size the app uses today keeps the layout it had.
public class FoldLayoutTests
{
    private static readonly FoldScreen DuoOuter = new(FoldDevice.Duo, false);
    private static readonly FoldScreen DuoInner = new(FoldDevice.Duo, true);
    private static readonly FoldScreen FoldOuter = new(FoldDevice.Fold8, false);
    private static readonly FoldScreen FoldInner = new(FoldDevice.Fold8, true);
    private static readonly FoldScreen UltraOuter = new(FoldDevice.Fold8Ultra, false);
    private static readonly FoldScreen UltraInner = new(FoldDevice.Fold8Ultra, true);

    [Theory]
    [InlineData(433, FoldWidthClass.Phone)]
    [InlineData(411, FoldWidthClass.Phone)]
    [InlineData(428, FoldWidthClass.Phone)] // a half-width split
    [InlineData(599, FoldWidthClass.Phone)]
    [InlineData(600, FoldWidthClass.Wide)]
    [InlineData(800, FoldWidthClass.Wide)]
    [InlineData(857, FoldWidthClass.Wide)]
    [InlineData(867, FoldWidthClass.Wide)]
    public void The_class_splits_at_600_wide(double width, FoldWidthClass expected) =>
        Assert.Equal(expected, FoldLayout.ClassOf(width));

    [Fact]
    public void Two_pages_meet_the_gutter_and_run_full_height()
    {
        BookPages? pages = FoldLayout.PagesOf(867, 613, new FoldGutter(433.5, 0));

        Assert.NotNull(pages);
        Assert.Equal(new PageRect(0, 0, 433.5, 613), pages.Value.Left);
        Assert.Equal(new PageRect(433.5, 0, 433.5, 613), pages.Value.Right);
    }

    [Fact]
    public void A_gutter_with_width_leaves_a_gap_between_the_pages()
    {
        BookPages pages = FoldLayout.PagesOf(857, 643, new FoldGutter(428.5, 8))!.Value;

        Assert.Equal(424.5, pages.Left.Width);
        Assert.Equal(432.5, pages.Right.X);
        Assert.Equal(424.5, pages.Right.Width);
    }

    [Theory]
    [InlineData(867, 613)]
    [InlineData(800, 800)]
    [InlineData(433, 613)]
    public void With_no_fold_there_are_no_pages(double width, double height) =>
        Assert.Null(FoldLayout.PagesOf(width, height, null));

    [Theory]
    [InlineData(0, 4)]
    [InlineData(2, 4)] // the left page would be empty
    [InlineData(867, 4)]
    [InlineData(870, 4)]
    public void A_gutter_not_inside_the_window_gives_no_pages(double x, double gutterWidth) =>
        Assert.Null(FoldLayout.PagesOf(867, 613, new FoldGutter(x, gutterWidth)));

    [Theory]
    [InlineData(double.NaN, 613, 433, 4)]
    [InlineData(867, double.NaN, 433, 4)]
    [InlineData(double.PositiveInfinity, 613, 433, 4)]
    [InlineData(-867, 613, 433, 4)]
    [InlineData(867, -613, 433, 4)]
    [InlineData(867, 613, double.NaN, 4)]
    [InlineData(867, 613, 433, double.NaN)]
    [InlineData(867, 613, 433, double.PositiveInfinity)]
    public void Non_finite_or_negative_sizes_give_no_pages(double width, double height, double x, double gutterWidth) =>
        Assert.Null(FoldLayout.PagesOf(width, height, new FoldGutter(x, gutterWidth)));

    [Fact]
    public void A_negative_gutter_width_counts_as_no_gutter()
    {
        BookPages pages = FoldLayout.PagesOf(867, 613, new FoldGutter(433.5, -6))!.Value;

        Assert.Equal(433.5, pages.Left.Width);
        Assert.Equal(433.5, pages.Right.X);
    }

    [Fact]
    public void An_empty_window_gives_no_pages() =>
        Assert.Null(FoldLayout.PagesOf(0, 0, new FoldGutter(10, 0)));

    [Fact]
    public void The_pages_of_the_square_ultra_inner_screen_are_equal()
    {
        BookPages pages = FoldLayout.PagesOf(800, 800, new FoldGutter(400, 0))!.Value;

        Assert.Equal(pages.Left.Width, pages.Right.Width);
        Assert.Equal(800, pages.Left.Height);
    }

    [Theory]
    [MemberData(nameof(ShelfCases))]
    public void The_shelf_columns_follow_the_mock(FoldScreen screen, double w, double h, int columns) =>
        Assert.Equal(columns, LandscapeLayout.ShelfLayout(w, h, screen).Columns);

    public static TheoryData<FoldScreen, double, double, int> ShelfCases() => new()
    {
        { DuoOuter, 433, 613, 3 }, { FoldOuter, 411, 658, 3 }, { UltraOuter, 411, 960, 2 },
        { DuoInner, 867, 613, 4 }, { FoldInner, 857, 643, 4 }, { UltraInner, 800, 800, 3 },
    };

    [Fact]
    public void The_fold_shelf_changes_only_the_column_count()
    {
        ShelfShape today = LandscapeLayout.ShelfLayout(800, 800);
        ShelfShape ultraInner = LandscapeLayout.ShelfLayout(800, 800, UltraInner);

        Assert.Equal(today with { Columns = 3 }, ultraInner);
        Assert.True(ultraInner.TabletUpright);
        Assert.Equal(LandscapeLayout.TabletPortraitShelfSidePadding, ultraInner.SidePadding);
    }

    // Decision 3: a half-width split on the inner screen is under 600 and shows the outer (phone) layouts, whatever the caller passes as Inner.
    [Fact]
    public void A_428_wide_half_of_an_inner_screen_gets_the_outer_result_from_every_fold_rule()
    {
        ChromeShape shape = CornerLayout.ChromeOf(428, 613, false, DuoInner);

        Assert.Equal(CornerLayout.ChromeOf(428, 613, false), shape);
        Assert.Equal(ChromeKind.Bar, shape.Kind);
        Assert.Equal(CornerLayout.SizesOf(shape), CornerLayout.SizesOf(shape, 428, DuoInner));
        Assert.Equal(4, CornerLayout.BookColumns(shape, 428, DuoInner));
        Assert.Equal(3, CornerLayout.BookColumns(shape, 428, UltraInner));
        Assert.Equal(3, LandscapeLayout.ShelfLayout(428, 613, DuoInner).Columns);
        Assert.Equal(2, LandscapeLayout.ShelfLayout(428, 613, UltraInner).Columns);
        Assert.Equal(1, SettingsLayout.SettingsColumns(428, FoldInner));
        Assert.Equal(2, SettingsLayout.StoreColumns(850, 428, FoldInner));
        Assert.Null(FoldLayout.ShelfRailWidth(428, DuoInner));
    }

    [Theory]
    [InlineData(433, 613)]
    [InlineData(411, 658)]
    [InlineData(411, 960)]
    public void The_corner_books_columns_on_the_outer_screens(double w, double h)
    {
        ChromeShape shape = CornerLayout.ChromeOf(w, h, false, DuoOuter);

        Assert.Equal(ChromeKind.Bar, shape.Kind);
        Assert.Equal(4, CornerLayout.BookColumns(shape, w, DuoOuter));
        Assert.Equal(4, CornerLayout.BookColumns(shape, w, FoldOuter));
        Assert.Equal(3, CornerLayout.BookColumns(shape, w, UltraOuter));
    }

    [Theory]
    [MemberData(nameof(InnerCornerCases))]
    public void The_inner_corner_is_a_230_rail_with_44_tabs_and_the_mock_columns(FoldScreen screen, double w, double h, int books, double gap)
    {
        ChromeShape shape = CornerLayout.ChromeOf(w, h, false, screen);
        CornerSizes sizes = CornerLayout.SizesOf(shape, w, screen);

        Assert.True(shape.IsRail);
        Assert.Equal(230, shape.RailWidth);
        Assert.Equal(230, sizes.RailWidth);
        Assert.Equal(44, sizes.TabHeight);
        Assert.Equal(books, CornerLayout.BookColumns(shape, w, screen));
        Assert.Equal(gap, sizes.GapX);
    }

    public static TheoryData<FoldScreen, double, double, int, double> InnerCornerCases() => new()
    {
        { DuoInner, 867, 613, 5, 14 }, { FoldInner, 857, 643, 5, 12 }, { UltraInner, 800, 800, 4, 12 },
    };

    [Fact]
    public void The_inner_shelf_rail_is_220_and_only_on_an_inner_screen()
    {
        Assert.Equal(220, FoldLayout.ShelfRailWidth(867, DuoInner));
        Assert.Equal(220, FoldLayout.ShelfRailWidth(800, UltraInner));
        Assert.Null(FoldLayout.ShelfRailWidth(433, DuoOuter));
        Assert.Null(FoldLayout.ShelfRailWidth(867, FoldScreen.None));
    }

    [Theory]
    [MemberData(nameof(SettingsCases))]
    public void The_settings_columns_follow_the_mock(FoldScreen screen, double w, int columns) =>
        Assert.Equal(columns, SettingsLayout.SettingsColumns(w, screen));

    public static TheoryData<FoldScreen, double, int> SettingsCases() => new()
    {
        { DuoInner, 867, 2 }, { FoldInner, 857, 2 }, { UltraInner, 800, 1 }, { DuoOuter, 433, 1 }, { UltraOuter, 411, 1 }, { FoldScreen.None, 867, 1 },
    };

    [Fact]
    public void The_inner_store_lists_books_in_one_column_even_when_wide()
    {
        Assert.Equal(1, SettingsLayout.StoreColumns(900, 867, DuoInner));
        Assert.Equal(2, SettingsLayout.StoreColumns(900, 1100, FoldScreen.None));
        Assert.Equal(1, SettingsLayout.StoreColumns(500, 800, FoldScreen.None));
    }

    // Regression pins: the sizes the app has today, with no fold, give the answers origin/main gave.
    [Theory]
    [InlineData(433, 613, false, 2, false)]
    [InlineData(867, 613, false, 4, false)]
    [InlineData(411, 658, false, 2, false)]
    [InlineData(857, 643, false, 4, false)]
    [InlineData(411, 960, false, 2, false)]
    [InlineData(800, 800, false, 4, true)]
    [InlineData(428, 926, false, 2, false)]
    [InlineData(1180, 820, false, 6, false)]
    [InlineData(820, 1180, false, 4, true)]
    [InlineData(852, 393, true, 0, false)]
    public void Today_s_shelf_is_unchanged_without_a_fold(double w, double h, bool sideways, int columns, bool tabletUpright)
    {
        ShelfShape shape = LandscapeLayout.ShelfLayout(w, h, FoldScreen.None);

        Assert.Equal(LandscapeLayout.ShelfLayout(w, h), shape);
        Assert.Equal(sideways, shape.Sideways);
        Assert.Equal(columns, shape.Columns);
        Assert.Equal(tabletUpright, shape.TabletUpright);
    }

    [Theory]
    [InlineData(433, 613, ChromeKind.Bar, 0, 3)]
    [InlineData(867, 613, ChromeKind.Rail, 290, 4)]
    [InlineData(411, 658, ChromeKind.Bar, 0, 3)]
    [InlineData(857, 643, ChromeKind.Rail, 290, 4)]
    [InlineData(411, 960, ChromeKind.Bar, 0, 3)]
    [InlineData(800, 800, ChromeKind.Rail, 260, 3)]
    [InlineData(852, 393, ChromeKind.Rail, 250, 5)]
    [InlineData(1180, 820, ChromeKind.Rail, 290, 4)]
    [InlineData(820, 1180, ChromeKind.Rail, 260, 3)]
    public void Today_s_corner_is_unchanged_without_a_fold(double w, double h, ChromeKind kind, double rail, int books)
    {
        ChromeShape shape = CornerLayout.ChromeOf(w, h, false, FoldScreen.None);

        Assert.Equal(CornerLayout.ChromeOf(w, h, false), shape);
        Assert.Equal(kind, shape.Kind);
        Assert.Equal(rail, shape.RailWidth);
        Assert.Equal(books, CornerLayout.BookColumns(shape, w, FoldScreen.None));
        Assert.Equal(CornerLayout.SizesOf(shape), CornerLayout.SizesOf(shape, w, FoldScreen.None));
    }

    [Fact]
    public void An_outer_screen_keeps_the_phone_bar_sizes()
    {
        ChromeShape shape = CornerLayout.ChromeOf(433, 613, false, DuoOuter);

        Assert.Equal(CornerLayout.SizesOf(shape), CornerLayout.SizesOf(shape, 433, DuoOuter));
        Assert.Equal(CornerLayout.ChromeOf(433, 613, false), shape);
    }

    [Fact]
    public void A_half_width_split_on_the_inner_screen_is_passed_as_no_fold_and_keeps_the_phone_layout()
    {
        ChromeShape shape = CornerLayout.ChromeOf(428, 613, false, FoldScreen.None);

        Assert.Equal(ChromeKind.Bar, shape.Kind);
        Assert.Equal(2, LandscapeLayout.ShelfLayout(428, 613, FoldScreen.None).Columns);
    }
}
