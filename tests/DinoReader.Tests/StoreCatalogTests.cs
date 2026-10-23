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
