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
}
