using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 323: a check-only trigger rebuilds the shelf tiles only when a language they were built in changed.
public class ShelfRefreshRuleTests
{
    [Fact]
    public void Nothing_built_yet_does_not_rebuild_before_the_page_appeared()
    {
        Assert.False(ShelfRefreshRule.ShouldRebuild(null, null, "en", "fa", false));
    }

    [Fact]
    public void Nothing_built_yet_rebuilds_once_the_page_appeared()
    {
        Assert.True(ShelfRefreshRule.ShouldRebuild(null, null, "en", "fa", true));
    }

    [Fact]
    public void The_same_languages_do_not_rebuild()
    {
        Assert.False(ShelfRefreshRule.ShouldRebuild("en", "fa", "en", "fa", true));
    }

    [Theory]
    [InlineData("en", "fa", "de", "fa")]
    [InlineData("en", "fa", "en", "en")]
    public void A_changed_language_rebuilds(string builtApp, string builtTitle, string app, string title)
    {
        Assert.True(ShelfRefreshRule.ShouldRebuild(builtApp, builtTitle, app, title, true));
    }
}
