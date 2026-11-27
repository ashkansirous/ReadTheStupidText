using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 418 decision 10: a locked shelf's covers have no "..." menu.
public class TileMenuRuleTests
{
    [Theory]
    [InlineData(false, false, false)] // locked shelf: no menu
    [InlineData(false, true, false)]
    [InlineData(true, false, true)] // unlocked, readable book: menu
    [InlineData(true, true, false)] // unlocked, lapsed book: no menu (Slice 398)
    public void ShowMore_FollowsTheLockAndTheLapse(bool unlocked, bool lapsed, bool expected) =>
        Assert.Equal(expected, TileMenuRule.ShowMore(unlocked, lapsed));
}
