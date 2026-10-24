using DinoReader.Core;

namespace DinoReader.Tests;

public class StoreFilterTests
{
    private static StoreItem Item(string title, int? min, int? max, string? author = null) =>
        new("0123456789abcdef0123456789abcdef", title, "en", 1, 1, 10, 5, null, "/d", null, min, max, author);

    private static readonly StoreItem[] Items =
    [
        Item("Peter", 3, 6, "Potter"),
        Item("Duckling", 5, 8, "Andersen"),
        Item("Lion", 4, 7, "Aesop"),
        Item("Teen", 10, 14, "Aesop"),
        Item("Plain", null, null),
    ];

    private static string[] Titles(StoreAgeBand band, string? author = null) =>
        StoreFilter.Apply(Items, band, author).Select(i => i.Title).ToArray();

    [Fact]
    public void All_ShowsEveryStory() => Assert.Equal(5, Titles(StoreAgeBand.All).Length);

    [Fact]
    public void UpTo5_MatchesRangesThatReachBelowSix() =>
        Assert.Equal(["Peter", "Duckling", "Lion", "Plain"], Titles(StoreAgeBand.UpTo5));

    [Fact]
    public void From6To8_MatchesOverlap_IncludingARangeThatStartsEarlier() =>
        Assert.Equal(["Peter", "Duckling", "Lion", "Plain"], Titles(StoreAgeBand.From6To8));

    [Fact]
    public void From9To12_MatchesOnlyRangesReachingNine() =>
        Assert.Equal(["Teen", "Plain"], Titles(StoreAgeBand.From9To12));

    [Fact]
    public void From9To12_IncludesStoriesAgedThirteenAndOver()
    {
        StoreItem[] items = [Item("Older", 13, 16), Item("Young", 3, 6)];
        Assert.Equal(["Older"], StoreFilter.Apply(items, StoreAgeBand.From9To12, null).Select(i => i.Title).ToArray());
    }

    [Fact]
    public void AStoryWithNoRange_ShowsUnderEveryBand() =>
        Assert.All(Enum.GetValues<StoreAgeBand>(), band => Assert.Contains("Plain", Titles(band)));

    [Fact]
    public void Author_NarrowsTheList_AndCombinesWithTheBand()
    {
        Assert.Equal(["Lion", "Teen"], Titles(StoreAgeBand.All, "Aesop"));
        Assert.Equal(["Lion"], Titles(StoreAgeBand.UpTo5, "Aesop"));
    }

    [Fact]
    public void Authors_AreDistinctAndSorted() => Assert.Equal(["Aesop", "Andersen", "Potter"], StoreFilter.Authors(Items));

    [Fact]
    public void Authors_DifferingOnlyInCase_AreOneEntry_AndMatchEachOther()
    {
        StoreItem[] items = [Item("A", 4, 7, "Aesop"), Item("B", 4, 7, "aesop")];
        Assert.Single(StoreFilter.Authors(items));
        Assert.Equal(2, StoreFilter.Apply(items, StoreAgeBand.All, "Aesop").Count);
    }
}
