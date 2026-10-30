using System.Text.Json;
using DinoReader.Core;

namespace DinoReader.Tests;

public class StoreCatalogTests
{
    private const string Id = "0123456789abcdef0123456789abcdef";

    private static string Entry(string id = Id, int schema = 1, string title = "Bear", string download = "/api/stories/x/download?v=1") =>
        $$"""{"id":"{{id}}","title":"{{title}}","language":"en","schemaVersion":{{schema}},"version":1,"pageCount":12,"minutes":6,"coverUrl":null,"downloadUrl":"{{download}}"}""";

    [Fact]
    public void Parse_ReadsAWellFormedEntry()
    {
        StoreItem item = Assert.Single(StoreCatalog.Parse($$"""{"schemaVersion":1,"stories":[{{Entry()}}]}"""));
        Assert.Equal((Id, "Bear", 12, 6, null), (item.Id, item.Title, item.PageCount, item.Minutes, item.CoverUrl));
    }

    [Fact]
    public void Parse_ReadsTheCreditLine_WhenThereIsOne()
    {
        string json = $$"""{"stories":[{{Entry().Replace("\"minutes\"", "\"credits\":\" Beatrix Potter, 1902 \",\"minutes\"")}}]}""";
        Assert.Equal("Beatrix Potter, 1902", Assert.Single(StoreCatalog.Parse(json)).Credits);
    }

    [Fact]
    public void Parse_LeavesTheCreditNull_WhenAbsentBlankOrTooLong()
    {
        string blank = Entry().Replace("\"minutes\"", "\"credits\":\"  \",\"minutes\"");
        string tooLong = Entry().Replace("\"minutes\"", $"\"credits\":\"{new string('x', 201)}\",\"minutes\"");
        string json = $$"""{"stories":[{{Entry()}},{{blank}},{{tooLong}}]}""";
        Assert.All(StoreCatalog.Parse(json), item => Assert.Null(item.Credits));
    }

    [Fact]
    public void TheCreditLine_ShowsTheChosenLanguage_ElseTheEnglishCredits_ElseNull()
    {
        string map = "\"credits\":\"Retold from Aesop\",\"creditsByLanguage\":{\"fa\":\" بازگویی \",\"xx\":\"No\",\"de\":\"  \",\"ja\":7},\"minutes\"";
        StoreItem item = Assert.Single(StoreCatalog.Parse($$"""{"stories":[{{Entry().Replace("\"minutes\"", map)}}]}"""));
        StoreItem plain = Assert.Single(StoreCatalog.Parse($$"""{"stories":[{{Entry()}}]}"""));

        Assert.Equal("بازگویی", item.CreditsIn("fa"));
        Assert.Equal("بازگویی", item.CreditsIn("fa-IR"));
        Assert.Equal("Retold from Aesop", item.CreditsIn("de"));
        Assert.Equal("Retold from Aesop", item.CreditsIn("en"));
        Assert.Null(plain.CreditsIn("fa"));
    }

    [Fact]
    public void TheCreditsMap_IgnoresUnsupportedKeysBeforeTheCap()
    {
        string junk = string.Join(",", Enumerable.Range(0, 40).Select(i => $"\"zz{i}\":\"No\""));
        string map = $"\"creditsByLanguage\":{{{junk},\"fa\":\"بازگویی\"}},\"minutes\"";
        StoreItem item = Assert.Single(StoreCatalog.Parse($$"""{"stories":[{{Entry().Replace("\"minutes\"", map)}}]}"""));

        Assert.Equal("بازگویی", item.CreditsIn("fa"));
    }

    [Fact]
    public void Parse_ReadsTheTitlesMap_DroppingBadLanguagesAndBlankTitles()
    {
        string json = $$"""{"stories":[{{Entry().Replace("\"minutes\"", "\"titles\":{\"fa\":\" خرس \",\"xx\":\"No\",\"de\":\"  \",\"ja\":7},\"minutes\"")}}]}""";
        StoreItem item = Assert.Single(StoreCatalog.Parse(json));
        Assert.Equal("خرس", item.TitleIn("fa"));
        Assert.Null(item.TitleIn("de"));
        Assert.Null(item.TitleIn("ja"));
    }

    [Fact]
    public void TheStoreRow_ShowsTheChosenLanguagesTitle_FromTheListingThenTheTable_ElseTheTitle()
    {
        StoreItem listed = Assert.Single(StoreCatalog.Parse($$"""{"stories":[{{Entry(title: "The Lion and the Mouse").Replace("\"minutes\"", "\"titles\":{\"fa\":\"از لیست\"},\"minutes\"")}}]}"""));
        StoreItem old = Assert.Single(StoreCatalog.Parse($$"""{"stories":[{{Entry(title: "The Lion and the Mouse")}}]}"""));
        StoreItem unknown = Assert.Single(StoreCatalog.Parse($$"""{"stories":[{{Entry(title: "Some New Book")}}]}"""));

        Assert.Equal("از لیست", ShippedTitles.ShownInStore(listed, "fa"));
        Assert.Equal("شیر و موش", ShippedTitles.ShownInStore(old, "fa"));
        Assert.Equal("Some New Book", ShippedTitles.ShownInStore(unknown, "fa"));
        Assert.Equal("The Lion and the Mouse", ShippedTitles.ShownInStore(old, "en"));
    }

    [Fact]
    public void Parse_ReadsAgeRangeAndAuthor()
    {
        string json = $$"""{"stories":[{{Entry().Replace("\"minutes\"", "\"ageMin\":3,\"ageMax\":6,\"author\":\" Beatrix Potter \",\"minutes\"")}}]}""";
        StoreItem item = Assert.Single(StoreCatalog.Parse(json));
        Assert.Equal((3, 6, "Beatrix Potter"), (item.AgeMin, item.AgeMax, item.Author));
    }

    [Theory]
    [InlineData("\"ageMin\":9,\"ageMax\":4")]
    [InlineData("\"ageMin\":3")]
    [InlineData("\"ageMin\":-1,\"ageMax\":4")]
    [InlineData("\"ageMin\":3,\"ageMax\":19")]
    public void Parse_DropsAnInvalidAgeRange(string fields)
    {
        StoreItem item = Assert.Single(StoreCatalog.Parse($$"""{"stories":[{{Entry().Replace("\"minutes\"", fields + ",\"minutes\"")}}]}"""));
        Assert.Null(item.AgeMin);
        Assert.Null(item.AgeMax);
    }

    [Theory]
    [InlineData("\"ageMin\":3.5,\"ageMax\":6")]
    [InlineData("\"ageMin\":\"3\",\"ageMax\":6")]
    public void Parse_DropsOnlyTheRange_WhenAnAgeIsWrongTyped(string fields)
    {
        StoreItem item = Assert.Single(StoreCatalog.Parse($$"""{"stories":[{{Entry().Replace("\"minutes\"", fields + ",\"minutes\"")}}]}"""));
        Assert.Equal(Id, item.Id);
        Assert.Null(item.AgeMin);
        Assert.Null(item.AgeMax);
    }

    [Theory]
    [InlineData("\"ageMin\":3.0,\"ageMax\":6e0")]
    public void Parse_AcceptsWholeNumbersWrittenAsDecimals(string fields)
    {
        StoreItem item = Assert.Single(StoreCatalog.Parse($$"""{"stories":[{{Entry().Replace("\"minutes\"", fields + ",\"minutes\"")}}]}"""));
        Assert.Equal((3, 6), (item.AgeMin, item.AgeMax));
    }

    [Fact]
    public void Parse_KeepsTheStory_WhenTheAuthorIsWrongTyped()
    {
        StoreItem item = Assert.Single(StoreCatalog.Parse($$"""{"stories":[{{Entry().Replace("\"minutes\"", "\"author\":7,\"minutes\"")}}]}"""));
        Assert.Null(item.Author);
    }

    [Fact]
    public void Parse_SkipsMalformedEntries()
    {
        string json = $$"""{"stories":[{{Entry("not-an-id")}},{{Entry(title: " ")}},{{Entry(download: "")}},{{Entry(schema: 0)}},{{Entry()}}]}""";
        Assert.Single(StoreCatalog.Parse(json));
    }

    [Fact]
    public void Parse_SkipsAnEntryWithAWrongTypedField_AndKeepsTheRest()
    {
        string bad = Entry().Replace("\"pageCount\":12", "\"pageCount\":\"twelve\"");
        Assert.Single(StoreCatalog.Parse($$"""{"stories":[{{bad}},{{Entry()}},7]}"""));
        Assert.Throws<JsonException>(() => StoreCatalog.Parse("[]"));
    }

    [Fact]
    public void Parse_ThrowsOnABodyThatIsNoList()
    {
        Assert.ThrowsAny<JsonException>(() => StoreCatalog.Parse("not json"));
        Assert.Throws<JsonException>(() => StoreCatalog.Parse("null"));
        Assert.Empty(StoreCatalog.Parse("{}"));
    }

    [Fact]
    public void Readable_HidesAStoryPackedInANewerFormat()
    {
        var items = StoreCatalog.Parse($$"""{"stories":[{{Entry(schema: StoryPackageManifest.CurrentSchemaVersion + 1)}},{{Entry(schema: StoryPackageManifest.CurrentSchemaVersion)}}]}""");
        Assert.Equal(2, items.Count);
        Assert.Single(StoreCatalog.Readable(items));
    }
}

public class ShippedTitlesForBookTests
{
    private static Story Book(string title, string? sourceId) =>
        new(StoryIds.New(), title, "en", DateTimeOffset.UnixEpoch, [], [], CoverStyle.Picture, 0, "", SourceId: sourceId);

    [Fact]
    public void ForBook_TranslatesAShippedBook_ButNotAnOwnBookWithTheSameTitle()
    {
        Assert.Null(ShippedTitles.ForBook(Book("The Three Little Pigs", null), "es"));
        Assert.Equal("Los tres cerditos", ShippedTitles.ForBook(Book("The Three Little Pigs", "pigs"), "es"));
    }
}
