using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 325 part A: the corner's chrome, rail widths, columns and sizes at every frame of the 2026-11-08 grown-up mock.
public class CornerLayoutTests
{
    [Theory]
    [InlineData(375, 667, ChromeKind.Bar, ChromeFamily.Phone, PhoneWidth.Compact, 0)] // P1 iPhone 8
    [InlineData(393, 852, ChromeKind.Bar, ChromeFamily.Phone, PhoneWidth.Regular, 0)] // P2 iPhone 15 Pro
    [InlineData(440, 956, ChromeKind.Bar, ChromeFamily.Phone, PhoneWidth.Large, 0)] // P3 iPhone 17 Pro Max
    [InlineData(412, 915, ChromeKind.Bar, ChromeFamily.Phone, PhoneWidth.Medium, 0)] // P4 Pixel 10 Pro
    [InlineData(412, 891, ChromeKind.Bar, ChromeFamily.Phone, PhoneWidth.Medium, 0)] // P5 Samsung S26 Ultra (assumed size)
    [InlineData(852, 393, ChromeKind.Rail, ChromeFamily.PhoneLandscape, PhoneWidth.Large, 250)] // L1
    [InlineData(820, 1180, ChromeKind.Rail, ChromeFamily.TabletPortrait, PhoneWidth.Large, 260)] // T1
    [InlineData(1180, 820, ChromeKind.Rail, ChromeFamily.TabletLandscape, PhoneWidth.Large, 290)] // T2
    [InlineData(568, 320, ChromeKind.Rail, ChromeFamily.PhoneLandscape, PhoneWidth.Large, 250)] // a small phone held sideways has no room for a bottom bar
    [InlineData(599, 900, ChromeKind.Bar, ChromeFamily.Phone, PhoneWidth.Large, 0)] // 600 is the line
    [InlineData(600, 900, ChromeKind.Rail, ChromeFamily.TabletPortrait, PhoneWidth.Large, 260)]
    public void The_chrome_is_a_bar_under_600_wide_and_a_rail_from_600_or_held_sideways(double w, double h, ChromeKind kind, ChromeFamily family, PhoneWidth width, double rail)
    {
        ChromeShape shape = CornerLayout.ChromeOf(w, h, windows: false);

        Assert.Equal(kind, shape.Kind);
        Assert.Equal(family, shape.Family);
        Assert.Equal(rail, shape.RailWidth);
        if (kind == ChromeKind.Bar)
        {
            Assert.Equal(width, shape.Width);
        }
    }

    [Theory]
    [InlineData(1100, 760)] // W1
    [InlineData(800, 560)]
    [InlineData(1600, 900)]
    public void Windows_is_always_a_280_rail(double w, double h)
    {
        ChromeShape shape = CornerLayout.ChromeOf(w, h, windows: true);

        Assert.True(shape.IsRail);
        Assert.Equal(ChromeFamily.Windows, shape.Family);
        Assert.Equal(280, shape.RailWidth);
    }

    [Theory]
    [InlineData(375, 667, 3)] // P1
    [InlineData(393, 852, 3)] // P2
    [InlineData(440, 956, 3)] // P3
    [InlineData(852, 393, 5)] // L1
    [InlineData(640, 360, 3)]
    [InlineData(568, 320, 2)]
    [InlineData(820, 1180, 3)] // T1
    [InlineData(600, 900, 2)]
    [InlineData(1180, 820, 4)] // T2
    public void The_books_grid_has_the_columns_each_frame_draws(double w, double h, int columns)
    {
        ChromeShape shape = CornerLayout.ChromeOf(w, h, windows: false);

        Assert.Equal(columns, CornerLayout.BookColumns(shape, w));
    }

    [Theory]
    [InlineData(1100, 4)] // W1
    [InlineData(800, 4)]
    [InlineData(1920, 6)]
    public void Windows_books_are_four_columns_and_more_on_a_wide_window(double w, int columns)
    {
        ChromeShape shape = CornerLayout.ChromeOf(w, 760, windows: true);

        Assert.Equal(columns, CornerLayout.BookColumns(shape, w));
    }

    [Fact]
    public void The_phone_header_grows_with_the_width_as_the_mock_draws_it()
    {
        CornerSizes p1 = CornerLayout.SizesOf(CornerLayout.ChromeOf(375, 667, false));
        CornerSizes p2 = CornerLayout.SizesOf(CornerLayout.ChromeOf(393, 852, false));
        CornerSizes p3 = CornerLayout.SizesOf(CornerLayout.ChromeOf(440, 956, false));
        CornerSizes p4 = CornerLayout.SizesOf(CornerLayout.ChromeOf(412, 915, false));

        Assert.Equal([22d, 22d, 24d, 22d], [p1.Title, p2.Title, p3.Title, p4.Title]);
        Assert.Equal([44d, 44d, 48d, 48d], [p1.PillHeight, p2.PillHeight, p3.PillHeight, p4.PillHeight]);
        Assert.Equal([52d, 52d, 56d, 56d], [p1.TabRow, p2.TabRow, p3.TabRow, p4.TabRow]);
        Assert.Equal([28d, 28d, 30d, 30d], [p1.TabPillHeight, p2.TabPillHeight, p3.TabPillHeight, p4.TabPillHeight]);
        Assert.Equal([30d, 30d, 32d, 30d], [p1.DotSize, p2.DotSize, p3.DotSize, p4.DotSize]);
    }

    [Theory]
    [InlineData(375, 667, 4, 52, 4)]
    [InlineData(393, 852, 6, 52, 0)] // P2: bar 58; the 34 home-bar inset is the bottom padding, added by the safe area
    [InlineData(412, 915, 6, 56, 0)] // P4: bar 62; the 24 gesture inset is the bottom padding
    [InlineData(440, 956, 6, 56, 0)] // P3: bar 62
    public void At_the_default_font_scale_a_bar_tab_is_the_mock_row_between_the_bars_padding(double w, double h, double padTop, double mockRow, double padBottom)
    {
        CornerSizes s = CornerLayout.SizesOf(CornerLayout.ChromeOf(w, h, false));
        const double gap = 2;

        double min = CornerLayout.TabBarMinHeight(s, gap);

        Assert.Equal(padTop + mockRow + padBottom, min); // the mock's P1-P4 rows (52 or 56) and paddings
        Assert.True(s.TabPillHeight + gap + Math.Ceiling(s.TabLabel * CornerLayout.TabLabelLineHeight) <= mockRow); // pill, gap and a descender-safe label line fit inside the row
    }

    [Fact]
    public void A_content_taller_than_the_row_grows_the_tab()
    {
        var s = new CornerSizes { TabRow = 40, TabBar = new Edges(0, 2, 0, 2), TabPillHeight = 30, TabLabel = 20 };

        Assert.Equal(2 + 2 + 30 + 2 + 28, CornerLayout.TabBarMinHeight(s, 2));
    }

    [Fact]
    public void The_least_bar_tab_height_never_drops_below_the_mock_row()
    {
        var s = new CornerSizes { TabRow = 80, TabBar = new Edges(0, 1, 0, 1), TabPillHeight = 10, TabLabel = 10 };

        Assert.Equal(82, CornerLayout.TabBarMinHeight(s, 2)); // the row of 80 plus the bar's own 1 + 1
    }

    [Fact]
    public void The_rail_sizes_follow_frames_L1_T1_and_W1()
    {
        CornerSizes l1 = CornerLayout.SizesOf(CornerLayout.ChromeOf(852, 393, false));
        CornerSizes t1 = CornerLayout.SizesOf(CornerLayout.ChromeOf(820, 1180, false));
        CornerSizes w1 = CornerLayout.SizesOf(CornerLayout.ChromeOf(1100, 760, true));

        Assert.Equal([46d, 56d, 48d], [l1.TabHeight, t1.TabHeight, w1.TabHeight]);
        Assert.Equal([44d, 56d, 52d], [l1.PillHeight, t1.PillHeight, w1.PillHeight]);
        Assert.Equal([30d, 36d, 36d], [l1.DotSize, t1.DotSize, w1.DotSize]);
        Assert.False(l1.NoteInRail); // L1 puts the short line beside the title
        Assert.True(t1.NoteInRail);
        Assert.True(w1.TabHints);
        Assert.False(t1.TabHints);
    }

    [Fact]
    public void Only_a_phone_held_sideways_has_the_short_coral_button()
    {
        Assert.False(CornerLayout.ChromeOf(852, 393, false).FullWords);
        Assert.True(CornerLayout.ChromeOf(820, 1180, false).FullWords);
        Assert.True(CornerLayout.ChromeOf(1100, 760, true).FullWords);
        Assert.False(CornerLayout.ChromeOf(375, 667, false).FullWords);
    }

    [Theory]
    [InlineData(347, 150, 130, true)]
    [InlineData(347, 200, 140, false)]
    [InlineData(347, 200, 137, true)]
    public void The_coral_button_drops_its_label_before_the_title_gives_way(double headerWidth, double titleWidth, double pillWithLabel, bool keeps) =>
        Assert.Equal(keeps, CornerLayout.PillKeepsLabel(headerWidth, titleWidth, pillWithLabel, 10));

    [Fact]
    public void The_books_grid_geometry_has_the_content_sides_and_no_top()
    {
        ChromeShape p1 = CornerLayout.ChromeOf(375, 667, false);
        ChromeShape t1 = CornerLayout.ChromeOf(820, 1180, false);

        WindowShapeSeam.ShelfGeometry bar = CornerLayout.BooksGeometry(p1, 3);
        WindowShapeSeam.ShelfGeometry rail = CornerLayout.BooksGeometry(t1, 3);

        Assert.Equal((12d, 0d, 12d, 3), (bar.Left, bar.Top, bar.Right, bar.Columns));
        Assert.Equal((28d, 0d, 32d, 3), (rail.Left, rail.Top, rail.Right, rail.Columns));
        Assert.Equal((16d, 22d), (rail.GapX, rail.GapY));
    }

    [Theory]
    [InlineData(852, 393, 250)]
    [InlineData(820, 1180, 260)]
    [InlineData(1180, 820, 290)]
    public void The_reorder_layer_sits_beside_the_new_rail_width(double w, double h, double rail)
    {
        ChromeShape shape = CornerLayout.ChromeOf(w, h, false);
        WindowShapeSeam.ShelfGeometry books = CornerLayout.BooksGeometry(shape, 3);

        Assert.Equal(rail, shape.RailWidth);
        Assert.Equal(books.Left + rail, books.BesideRail(shape.RailWidth).Left);
    }
}
