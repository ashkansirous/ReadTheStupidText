using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>The first-run and intro skeleton at the sizes of the onboarding mock's frames (Slice 325 part D).</summary>
public class OnboardingShapeTests
{
    private static OnboardingShape Phone(double w, double h, bool intro = false) => OnboardingLayout.Shape(w, h, windows: false, intro);

    [Theory]
    [InlineData(375, 667, true)]
    [InlineData(393, 852, false)]
    [InlineData(599, 900, false)]
    public void Under_600_wide_the_page_is_one_column_and_under_700_tall_it_is_compact(double w, double h, bool compact)
    {
        OnboardingShape shape = Phone(w, h);
        Assert.Equal(OnboardingMode.Stack, shape.Mode);
        Assert.Equal(OnboardingFamily.Phone, shape.Family);
        Assert.Equal(0, shape.LeftPane);
        Assert.Equal(compact, shape.Compact);
    }

    [Theory]
    [InlineData(852, 393, OnboardingFamily.PhoneLandscape, 300, 300)]
    [InlineData(820, 1180, OnboardingFamily.TabletPortrait, 320, 320)]
    [InlineData(1180, 820, OnboardingFamily.TabletLandscape, 460, 560)]
    public void From_600_wide_the_page_has_two_panes_with_the_left_pane_of_its_family(double w, double h, OnboardingFamily family, double pane, double introPane)
    {
        OnboardingShape shape = Phone(w, h);
        Assert.Equal(OnboardingMode.Split, shape.Mode);
        Assert.Equal(family, shape.Family);
        Assert.Equal(pane, shape.LeftPane);
        Assert.False(shape.Compact);
        Assert.Equal(introPane, Phone(w, h, intro: true).LeftPane);
    }

    [Theory]
    [InlineData(1100, 760, 380, 500)]
    [InlineData(800, 560, 380, 500)]
    public void The_windows_head_has_a_380_pane_and_a_500_intro_pane(double w, double h, double pane, double introPane)
    {
        OnboardingShape shape = OnboardingLayout.Shape(w, h, windows: true, intro: false);
        Assert.Equal(OnboardingFamily.Windows, shape.Family);
        Assert.Equal(pane, shape.LeftPane);
        Assert.Equal(introPane, OnboardingLayout.Shape(w, h, windows: true, intro: true).LeftPane);
    }

    [Theory]
    [InlineData(393, 852, 2, 52, 28)]
    [InlineData(375, 667, 2, 48, 28)]
    [InlineData(852, 393, 3, 46, 24)]
    [InlineData(1180, 820, 2, 64, 34)]
    [InlineData(820, 1180, 2, 64, 34)]
    public void Language_buttons_follow_the_frames(double w, double h, int columns, double height, double flag)
    {
        OnboardingShape shape = Phone(w, h);
        Assert.Equal(columns, OnboardingLayout.LanguageColumns(shape));
        Assert.Equal(height, OnboardingLayout.LanguageButtonHeight(shape));
        Assert.Equal(flag, OnboardingLayout.FlagSize(shape));
    }

    [Fact]
    public void The_windows_language_page_is_two_wide_buttons()
    {
        OnboardingShape shape = OnboardingLayout.Shape(1100, 760, windows: true, intro: false);
        Assert.Equal(2, OnboardingLayout.LanguageColumns(shape));
        Assert.Equal(64, OnboardingLayout.LanguageButtonHeight(shape));
    }

    [Theory]
    [InlineData(393, 852, false, 2, true, false)]
    [InlineData(375, 667, false, 2, true, true)]
    [InlineData(852, 393, false, 4, false, true)]
    [InlineData(820, 1180, false, 2, true, true)]
    [InlineData(1180, 820, false, 3, true, true)]
    [InlineData(1100, 760, true, 3, true, true)]
    public void The_book_picker_follows_the_frames(double w, double h, bool windows, int columns, bool tags, bool countBeside)
    {
        OnboardingShape shape = OnboardingLayout.Shape(w, h, windows, intro: false);
        Assert.Equal(columns, OnboardingLayout.BookColumns(shape));
        Assert.Equal(tags, OnboardingLayout.TagsShown(shape));
        Assert.Equal(countBeside, OnboardingLayout.CountBesideContinue(shape));
    }

    [Fact]
    public void Only_the_iphone_8_page_uses_the_shorter_books_line()
    {
        Assert.True(OnboardingLayout.ShortBooksLine(Phone(375, 667)));
        Assert.False(OnboardingLayout.ShortBooksLine(Phone(393, 852)));
        Assert.False(OnboardingLayout.ShortBooksLine(Phone(852, 393)));
    }

    [Theory]
    [InlineData(393, 852, false, double.NaN, 56)]
    [InlineData(852, 393, false, 220, 50)]
    [InlineData(1180, 820, false, 220, 50)]
    [InlineData(1100, 760, true, 260, 52)]
    public void The_main_button_is_56_high_stacked_and_a_fixed_size_beside_the_panes(double w, double h, bool windows, double width, double height)
    {
        (double actualWidth, double actualHeight) = OnboardingLayout.ContinueSize(OnboardingLayout.Shape(w, h, windows, intro: false));
        Assert.Equal(width, actualWidth);
        Assert.Equal(height, actualHeight);
    }

    [Theory]
    [InlineData(393, 852, false, 1.0)]
    [InlineData(375, 667, false, 0.8)]
    [InlineData(852, 393, false, 0.8)]
    [InlineData(820, 1180, false, 1.15)]
    [InlineData(1180, 820, false, 1.6)]
    [InlineData(1100, 760, true, 1.4)]
    [InlineData(380, 700, true, 1.0)]
    [InlineData(380, 500, true, 0.8)]
    public void The_intro_picture_scales_as_drawn(double w, double h, bool windows, double scale) =>
        Assert.Equal(scale, OnboardingLayout.ArtScale(OnboardingLayout.Shape(w, h, windows, intro: true)));

    [Theory]
    [InlineData(1000, 20, 440)]
    [InlineData(393, 20, 353)]
    [InlineData(30, 20, 0)]
    public void The_centred_column_is_440_at_most(double width, double pad, double expected) =>
        Assert.Equal(expected, OnboardingLayout.CentredWidth(width, pad));
}
