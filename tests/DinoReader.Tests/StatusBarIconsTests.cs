using DinoReader.Core;

namespace DinoReader.Tests;

public class StatusBarIconsTests
{
    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void Ink_under_the_bar_always_gets_light_icons(bool ink, bool lightBackground, bool expectedDark) =>
        Assert.Equal(expectedDark, StatusBarIcons.UseDarkIcons(ink, lightBackground));

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)] // a rail has no header on top
    [InlineData(false, false, false)] // the kids' shelf
    [InlineData(false, true, false)]
    public void Ink_runs_under_the_bar_only_in_the_corner_with_a_header(bool grownUp, bool rail, bool expected) =>
        Assert.Equal(expected, StatusBarIcons.InkUnderBar(grownUp, rail));

    [Fact]
    public void The_corner_page_is_ink_dark_so_its_icons_are_light_whatever_the_flag()
    {
        const double inkLuminance = 0.02; // Dino ink is far below the 0.179 crossover
        bool ink = StatusBarIcons.InkUnderBar(grownUp: true, rail: false);

        Assert.False(StatusBarIcons.UseDarkIcons(ink, lightBackground: inkLuminance > 0.179));
        Assert.False(StatusBarIcons.UseDarkIcons(inkUnderBar: false, lightBackground: inkLuminance > 0.179));
    }
}
