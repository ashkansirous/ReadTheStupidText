using System.Text.Json;
using DinoReader.BookBuilder;
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

    // Slice 394: BandsWithBooks / EffectiveBand and the one-band-per-book ranges.
    [Fact]
    public void BandsWithBooks_NoBooks_IsEmpty() => Assert.Empty(StoreFilter.BandsWithBooks([]));

    [Fact]
    public void BandsWithBooks_OneBand_ListsOnlyThatBand() =>
        Assert.Equal([StoreAgeBand.UpTo5], StoreFilter.BandsWithBooks([Item("A", 3, 5), Item("B", 4, 5)]));

    [Fact]
    public void BandsWithBooks_TwoBands_NeverListsAllAgesOrAnEmptyBand() =>
        Assert.Equal([StoreAgeBand.UpTo5, StoreAgeBand.From6To8], StoreFilter.BandsWithBooks([Item("A", 3, 5), Item("B", 6, 8)]));

    [Fact]
    public void BandsWithBooks_ABookWithNoRange_DoesNotMakeAnEmptyBandLookFull() =>
        Assert.Equal([StoreAgeBand.UpTo5], StoreFilter.BandsWithBooks([Item("A", 3, 5), Item("Plain", null, null)]));

    [Fact]
    public void BandsWithBooks_OnlyUnrangedBooks_IsEmpty() => Assert.Empty(StoreFilter.BandsWithBooks([Item("Plain", null, null)]));

    [Fact]
    public void EffectiveBand_FallsBackToAll_WhenTheBandLostItsLastBook()
    {
        Assert.Equal(StoreAgeBand.All, StoreFilter.EffectiveBand(StoreAgeBand.From9To12, [StoreAgeBand.UpTo5, StoreAgeBand.From6To8]));
        Assert.Equal(StoreAgeBand.UpTo5, StoreFilter.EffectiveBand(StoreAgeBand.UpTo5, [StoreAgeBand.UpTo5, StoreAgeBand.From6To8]));
        Assert.Equal(StoreAgeBand.All, StoreFilter.EffectiveBand(StoreAgeBand.All, []));
    }

    [Theory]
    [InlineData(3, 5, StoreAgeBand.UpTo5)]
    [InlineData(6, 8, StoreAgeBand.From6To8)]
    public void NarrowedRanges_PutABookUnderExactlyOneBand(int min, int max, StoreAgeBand expected)
    {
        StoreItem item = Item("Book", min, max);
        StoreAgeBand[] bands = Enum.GetValues<StoreAgeBand>().Where(b => b != StoreAgeBand.All && StoreFilter.InBand(item, b)).ToArray();
        Assert.Equal([expected], bands);
    }

    [Fact]
    public void EveryBookJsonRange_FitsExactlyOneBand()
    {
        string[] files = Directory.GetFiles(Path.Combine(StarterBuilder.RepoRoot(), "content", "dino"), "book.json", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
            Assert.True(doc.RootElement.TryGetProperty("ageMin", out JsonElement ageMin) && ageMin.ValueKind == JsonValueKind.Number, $"{file}: ageMin must be a number");
            Assert.True(doc.RootElement.TryGetProperty("ageMax", out JsonElement ageMax) && ageMax.ValueKind == JsonValueKind.Number, $"{file}: ageMax must be a number");
            StoreItem item = Item(file, ageMin.GetInt32(), ageMax.GetInt32());
            int bands = Enum.GetValues<StoreAgeBand>().Count(b => b != StoreAgeBand.All && StoreFilter.InBand(item, b));
            Assert.True(bands == 1, $"{file}: ageMin/ageMax {item.AgeMin}-{item.AgeMax} fits {bands} bands, not exactly one (Slice 394 Decision 1)");
        }
    }

    [Fact]
    public void AnEmptyList_HasNoBandsAndNoAuthors_AndEveryFilterOnItIsEmpty()
    {
        StoreItem[] none = [];
        Assert.Empty(StoreFilter.BandsWithBooks(none));
        Assert.Empty(StoreFilter.Authors(none));
        Assert.Empty(StoreFilter.Apply(none, StoreAgeBand.UpTo5, "Aesop"));
        Assert.Equal(StoreAgeBand.All, StoreFilter.EffectiveBand(StoreAgeBand.UpTo5, StoreFilter.BandsWithBooks(none)));
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

    // Slice 394: Resolve is the one decision behind chips, author row and list.
    [Fact]
    public void Resolve_OnLoad_KeepsAValidBandAndAuthor()
    {
        StoreFilterState s = StoreFilter.Resolve(Items, StoreAgeBand.UpTo5, "Aesop");
        Assert.Equal(StoreAgeBand.UpTo5, s.Band);
        Assert.Equal("Aesop", s.Author);
        Assert.True(s.ChipsShown);
        Assert.Equal([StoreAgeBand.UpTo5, StoreAgeBand.From6To8, StoreAgeBand.From9To12], s.VisibleBands);
        Assert.True(s.AuthorRowShown);
    }

    [Fact]
    public void Resolve_AfterAChipTap_DropsAnAuthorTheNewBandDoesNotOffer()
    {
        StoreFilterState s = StoreFilter.Resolve(Items, StoreAgeBand.From9To12, "Potter");
        Assert.Equal(StoreAgeBand.From9To12, s.Band);
        Assert.Null(s.Author);
        Assert.Equal(["Aesop"], s.AuthorsInBand);
        Assert.False(s.AuthorRowShown);
    }

    [Fact]
    public void Resolve_AfterAnAuthorTap_KeepsTheBand()
    {
        StoreFilterState s = StoreFilter.Resolve(Items, StoreAgeBand.From6To8, "Andersen");
        Assert.Equal((StoreAgeBand.From6To8, "Andersen"), (s.Band, s.Author));
        Assert.Equal(["Duckling"], StoreFilter.Apply(Items, s.Band, s.Author).Select(i => i.Title));
    }

    [Fact]
    public void Resolve_OnAppearing_WithTheSameList_GivesTheSameState() =>
        Assert.Equal(StoreFilter.Resolve(Items, StoreAgeBand.UpTo5, "Aesop").Band, StoreFilter.Resolve(Items, StoreAgeBand.UpTo5, "Aesop").Band);

    [Fact]
    public void Resolve_ABandThatEmpties_FallsBackToAll_AndTheListIsNotFiltered()
    {
        StoreItem[] items = [Item("A", 3, 5, "X"), Item("B", 3, 5, "Y")];
        StoreFilterState s = StoreFilter.Resolve(items, StoreAgeBand.From9To12, null);
        Assert.Equal(StoreAgeBand.All, s.Band);
        Assert.False(s.ChipsShown);
        Assert.Equal(2, StoreFilter.Apply(items, s.Band, s.Author).Count);
    }

    [Fact]
    public void Resolve_AnAuthorNoLongerOffered_IsNull_SoTheListIsNotFilteredByIt()
    {
        StoreItem[] items = [Item("A", 3, 5, "X"), Item("B", 3, 5, null)];
        StoreFilterState s = StoreFilter.Resolve(items, StoreAgeBand.All, "X");
        Assert.Null(s.Author);
        Assert.False(s.AuthorRowShown);
        Assert.Equal(2, StoreFilter.Apply(items, s.Band, s.Author).Count);
    }

    // Slice 394, coordinator decision: the requested band is kept and comes back; an author the shown list did not offer is dropped (the page stores the resolved author on a chip tap) and does not come back.
    [Fact]
    public void Resolve_BandEmptiesAndReturns_BandComesBack_AuthorDoesNot()
    {
        StoreItem[] young = [Item("A", 3, 5, "X"), Item("B", 3, 5, "Y")];
        StoreItem[] withTeens = [.. young, Item("C", 10, 12, "Z"), Item("D", 10, 12, "W")];
        Assert.Equal("Z", StoreFilter.Resolve(withTeens, StoreAgeBand.From9To12, "Z").Author);

        StoreFilterState emptied = StoreFilter.Resolve(young, StoreAgeBand.From9To12, "Z");
        Assert.Equal((StoreAgeBand.All, null), (emptied.Band, emptied.Author));

        StoreFilterState returned = StoreFilter.Resolve(withTeens, StoreAgeBand.From9To12, emptied.Author);
        Assert.Equal((StoreAgeBand.From9To12, null), (returned.Band, returned.Author));
        Assert.Equal(2, StoreFilter.Apply(withTeens, returned.Band, returned.Author).Count);
    }

    // Slice 394: the chip tap compares with the requested band, so "All ages" clears a request whose band is empty.
    [Fact]
    public void Resolve_AfterClearingAnEmptyRequestedBand_StaysOnAll_WhenTheBandReturns()
    {
        StoreItem[] withTeens = [Item("A", 3, 5, "X"), Item("B", 3, 5, "Y"), Item("C", 10, 12, "Z")];
        Assert.Equal(StoreAgeBand.All, StoreFilter.Resolve(withTeens, StoreAgeBand.All, null).Band);
        Assert.Equal(StoreAgeBand.From9To12, StoreFilter.Resolve(withTeens, StoreAgeBand.From9To12, null).Band);
    }

    [Fact]
    public void Resolve_MatchesTheAuthorIgnoringCase_AndReturnsTheListedSpelling()
    {
        StoreFilterState s = StoreFilter.Resolve(Items, StoreAgeBand.All, "aesop");
        Assert.Equal("Aesop", s.Author);
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
