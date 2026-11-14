using System.Text.Json;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Store tags on the listing (Slice 260): the closed set, the badges a card shows, tagged-first order and the package size.</summary>
public class StoreTagTests
{
    private const string Id = "0123456789abcdef0123456789abcdef";

    private static string Entry(string id, string extra = "") =>
        $$"""{"id":"{{id}}","title":"Book {{id[..2]}}","language":"en","schemaVersion":1,"version":1,"downloadUrl":"/d"{{extra}}}""";

    private static string Id2(int n) => n.ToString("x2") + Id[2..];

    [Theory]
    [InlineData("Hot", StoreTag.Hot)]
    [InlineData("Popular", StoreTag.Popular)]
    [InlineData("Recommended", StoreTag.Recommended)]
    [InlineData("New", StoreTag.New)]
    public void Parse_AcceptsTheFourNamesExactly(string name, StoreTag expected) => Assert.Equal(expected, StoreTags.Parse(name));

    [Theory]
    [InlineData("hot")]
    [InlineData("NEW")]
    [InlineData("Sale")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_RejectsAnythingElse(string? name) => Assert.Null(StoreTags.Parse(name));

    [Fact]
    public void Listing_ReadsTagsInListOrder_DropsUnknownAndRepeated()
    {
        string json = $$"""{"stories":[{{Entry(Id, ",\"tags\":[\"New\",\"Hot\",\"Hot\",\"hot\",\"Sale\",7]")}}]}""";
        Assert.Equal([StoreTag.Hot, StoreTag.New], Assert.Single(StoreCatalog.Parse(json)).TagList);
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"tags\":null")]
    [InlineData(",\"tags\":\"Hot\"")]
    [InlineData(",\"tags\":[]")]
    public void Listing_WithNoUsableTags_HasNoBadges(string extra)
    {
        StoreItem item = Assert.Single(StoreCatalog.Parse($$"""{"stories":[{{Entry(Id, extra)}}]}"""));
        Assert.Empty(item.TagList);
        Assert.Empty(item.Badges);
    }

    [Fact]
    public void ACard_ShowsAtMostTwoBadges_InListOrder()
    {
        Assert.Equal([StoreTag.Hot, StoreTag.Popular], StoreTags.Badges([StoreTag.New, StoreTag.Popular, StoreTag.Hot, StoreTag.Recommended]));
        Assert.Equal([StoreTag.Recommended], StoreTags.Badges([StoreTag.Recommended, StoreTag.Recommended]));
    }

    [Fact]
    public void TaggedBooksListFirst_ThenTheListOrderInsideEachGroup()
    {
        string json = $$"""{"stories":[{{Entry(Id2(1))}},{{Entry(Id2(2), ",\"tags\":[\"New\"]")}},{{Entry(Id2(3))}},{{Entry(Id2(4), ",\"tags\":[\"Hot\"]")}}]}""";
        Assert.Equal([Id2(2), Id2(4), Id2(1), Id2(3)], StoreCatalog.Parse(json).Select(i => i.Id));
    }

    [Fact]
    public void Bytes_AreKept_WhenAPositiveWholeNumber_AndUnknownOtherwise()
    {
        Assert.Equal(9_437_184L, Bytes(",\"bytes\":9437184"));
        Assert.Null(Bytes(""));
        Assert.Null(Bytes(",\"bytes\":0"));
        Assert.Null(Bytes(",\"bytes\":-4"));
        Assert.Null(Bytes(",\"bytes\":1.5"));
        Assert.Null(Bytes(",\"bytes\":\"9\""));
    }

    private static long? Bytes(string extra) => Assert.Single(StoreCatalog.Parse($$"""{"stories":[{{Entry(Id, extra)}}]}""")).Bytes;

    [Fact]
    public void ABadTagsOrBytesField_DoesNotSpoilTheEntry()
    {
        using JsonDocument document = JsonDocument.Parse($$"""{"stories":[{{Entry(Id, ",\"tags\":{\"a\":1},\"bytes\":[1]")}}]}""");
        StoreItem item = Assert.Single(StoreCatalog.Parse(document.RootElement.GetRawText()));
        Assert.Empty(item.TagList);
        Assert.Null(item.Bytes);
    }
}
