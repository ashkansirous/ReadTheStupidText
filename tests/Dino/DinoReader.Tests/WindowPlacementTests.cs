using System.Globalization;
using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 235 part A: which rectangle the Windows window opens with.
public class WindowPlacementTests
{
    private static readonly (int Width, int Height) Default = (1100, 760);
    private static readonly (int Width, int Height) Minimum = (800, 560);
    private static readonly PixelRect Main = new(0, 0, 1920, 1040);
    private static readonly PixelRect Second = new(1920, 0, 1280, 1000);

    private static WindowOpening Resolve(SavedWindow? saved, params PixelRect[] areas) =>
        WindowPlacement.Resolve(saved, areas, Default, Minimum);

    [Fact]
    public void Nothing_saved_opens_at_the_default_size_centred_on_the_main_display()
    {
        WindowOpening opening = Resolve(null, Main, Second);

        Assert.Equal(new PixelRect(410, 140, 1100, 760), opening.Bounds);
        Assert.False(opening.Maximised);
    }

    [Fact]
    public void A_saved_window_on_a_display_opens_where_it_was()
    {
        var saved = new SavedWindow(new PixelRect(2000, 100, 900, 600), false);

        WindowOpening opening = Resolve(saved, Main, Second);

        Assert.Equal(saved.Bounds, opening.Bounds);
    }

    [Fact]
    public void The_maximised_state_is_kept_with_the_restored_rectangle()
    {
        var saved = new SavedWindow(new PixelRect(100, 100, 900, 600), true);

        WindowOpening opening = Resolve(saved, Main);

        Assert.True(opening.Maximised);
        Assert.Equal(saved.Bounds, opening.Bounds);
    }

    [Fact]
    public void A_saved_place_off_every_display_opens_centred_on_the_main_one_at_the_saved_size()
    {
        var saved = new SavedWindow(new PixelRect(5000, 4000, 900, 600), false);

        WindowOpening opening = Resolve(saved, Main, Second);

        Assert.Equal(new PixelRect(510, 220, 900, 600), opening.Bounds);
    }

    [Fact]
    public void A_display_that_was_unplugged_sends_the_window_to_the_main_one()
    {
        var saved = new SavedWindow(new PixelRect(2000, 100, 900, 600), false);

        WindowOpening opening = Resolve(saved, Main);

        Assert.Equal(new PixelRect(510, 220, 900, 600), opening.Bounds);
    }

    [Fact]
    public void An_off_screen_size_is_clamped_to_the_minimum_and_the_main_display()
    {
        var tooBig = new SavedWindow(new PixelRect(9000, 9000, 4000, 3000), false);
        var tooSmall = new SavedWindow(new PixelRect(9000, 9000, 100, 100), false);

        Assert.Equal(new PixelRect(0, 0, 1920, 1040), Resolve(tooBig, Main).Bounds);
        Assert.Equal(new PixelRect(560, 240, 800, 560), Resolve(tooSmall, Main).Bounds);
    }

    [Fact]
    public void A_saved_size_below_the_minimum_is_raised()
    {
        var saved = new SavedWindow(new PixelRect(100, 100, 300, 200), false);

        Assert.Equal(new PixelRect(100, 100, 800, 560), Resolve(saved, Main).Bounds);
    }

    [Fact]
    public void A_window_with_only_a_sliver_on_a_display_counts_as_off_it()
    {
        var saved = new SavedWindow(new PixelRect(1900, 100, 900, 600), false); // 20 px inside the main display, the rest in the gap beyond it

        WindowOpening opening = Resolve(saved, Main);

        Assert.Equal(new PixelRect(510, 220, 900, 600), opening.Bounds);
    }

    [Fact]
    public void A_window_with_its_title_bar_above_the_screen_opens_centred()
    {
        var saved = new SavedWindow(new PixelRect(100, -600, 900, 700), false); // the body is on screen, the title bar is not

        Assert.Equal(new PixelRect(510, 170, 900, 700), Resolve(saved, Main).Bounds);
    }

    [Fact]
    public void A_saved_size_larger_than_its_display_is_clamped_to_it()
    {
        var saved = new SavedWindow(new PixelRect(100, 100, 3000, 2000), false);

        Assert.Equal(new PixelRect(0, 0, 1920, 1040), Resolve(saved, Main).Bounds);
    }

    // Slice 316 item 7: a window saved maximised reopens maximised, and the normal bounds behind it are the clamped ones, so un-maximising never gives a window bigger than the screen.
    [Fact]
    public void A_maximised_window_saved_larger_than_a_small_screen_keeps_maximised_with_clamped_restored_bounds()
    {
        var small = new PixelRect(0, 0, 1366, 728);
        var saved = new SavedWindow(new PixelRect(100, 100, 1800, 1000), true);

        WindowOpening opening = Resolve(saved, small);

        Assert.True(opening.Maximised);
        Assert.Equal(new PixelRect(0, 0, 1366, 728), opening.Bounds);
    }

    [Fact]
    public void A_maximised_window_off_every_display_keeps_maximised_with_normal_bounds_inside_the_main_one()
    {
        var saved = new SavedWindow(new PixelRect(9000, 9000, 2500, 1500), true);

        WindowOpening opening = Resolve(saved, Main);

        Assert.True(opening.Maximised);
        Assert.Equal(new PixelRect(0, 0, 1920, 1040), opening.Bounds);
    }

    [Fact]
    public void A_saved_position_near_the_right_or_bottom_edge_is_pulled_back_inside_the_work_area()
    {
        var saved = new SavedWindow(new PixelRect(1800, 900, 900, 700), false);

        Assert.Equal(new PixelRect(1020, 340, 900, 700), Resolve(saved, Main).Bounds);
    }

    [Fact]
    public void Saved_text_with_negative_coordinates_round_trips_under_a_culture_with_a_unicode_minus()
    {
        var saved = new SavedWindow(new PixelRect(-1500, -40, 900, 700), true);
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
            string text = saved.ToText();

            Assert.Equal("-1500,-40,900,700,1", text);
            Assert.Equal(saved, SavedWindow.TryParse(text));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void A_window_whose_title_bar_strip_is_on_a_display_counts_as_on_it()
    {
        Assert.True(WindowPlacement.IsOnAnyDisplay(new PixelRect(1800, 900, 900, 600), new[] { Main }));
        Assert.False(WindowPlacement.IsOnAnyDisplay(new PixelRect(100, 1030, 900, 600), new[] { Main }));
    }

    [Theory]
    [InlineData("2147483000,0,1000,600,0")]
    [InlineData("0,0,2147483000,600,0")]
    [InlineData("0,-2147483000,900,600,0")]
    public void Absurd_saved_numbers_read_as_nothing_saved(string text)
    {
        Assert.Null(SavedWindow.TryParse(text));
    }

    [Fact]
    public void A_window_straddling_two_displays_is_pulled_onto_the_one_holding_most_of_its_title_bar()
    {
        var saved = new SavedWindow(new PixelRect(1500, 100, 900, 600), false);

        Assert.Equal(new PixelRect(1920, 100, 900, 600), Resolve(saved, Main, Second).Bounds);
    }

    [Fact]
    public void A_display_smaller_than_the_minimum_gets_the_minimum()
    {
        WindowOpening opening = Resolve(null, new PixelRect(0, 0, 640, 480));

        Assert.Equal(800, opening.Bounds.Width);
        Assert.Equal(560, opening.Bounds.Height);
    }

    [Fact]
    public void No_displays_known_keeps_the_saved_rectangle_or_the_default()
    {
        Assert.Equal(new PixelRect(0, 0, 1100, 760), Resolve(null).Bounds);
        var saved = new SavedWindow(new PixelRect(5, 6, 900, 600), true);
        Assert.Equal(new WindowOpening(saved.Bounds, true), Resolve(saved));
    }

    [Fact]
    public void The_saved_text_round_trips()
    {
        var saved = new SavedWindow(new PixelRect(-1200, 30, 900, 600), true);

        Assert.Equal(saved, SavedWindow.TryParse(saved.ToText()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1,2,3")]
    [InlineData("a,b,c,d,e")]
    [InlineData("0,0,0,600,0")]
    [InlineData("0,0,900,-1,0")]
    public void Damaged_saved_text_reads_as_nothing_saved(string? text)
    {
        Assert.Null(SavedWindow.TryParse(text));
    }
}
