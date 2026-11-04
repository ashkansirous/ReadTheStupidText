using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 246 C2: the listing's optional per-language audio packs (Worker route /api/stories, Slice 246 B2).
public class StoreCatalogAudioTests
{
    private const string Id = "0123456789abcdef0123456789abcdef";

    private static string Item(string audio) =>
        $$"""{"stories":[{"id":"{{Id}}","title":"Bear","language":"en","schemaVersion":1,"version":1,"downloadUrl":"/api/stories/x/download?v=1"{{audio}}}]}""";

    [Fact]
    public void Parse_ReadsAPackPerLanguage()
    {
        StoreItem item = Assert.Single(StoreCatalog.Parse(Item(""","audio":{"fa":{"version":2,"bytes":1234,"url":"/api/stories/x/audio/fa?v=2"},"nb":{"version":1,"bytes":null,"url":"/a/nb?v=1"}}""")));

        Assert.Equal(new AudioPackInfo(2, 1234, "/api/stories/x/audio/fa?v=2"), item.AudioIn("fa"));
        Assert.Equal(new AudioPackInfo(1, null, "/a/nb?v=1"), item.AudioIn("nb"));
        Assert.Null(item.AudioIn("de"));
    }

    [Fact]
    public void AudioIn_TakesTheLanguageCaseAndRegionLoosely()
    {
        StoreItem item = Assert.Single(StoreCatalog.Parse(Item(""","audio":{"fa":{"version":1,"bytes":5,"url":"/u"}}""")));

        Assert.NotNull(item.AudioIn("FA"));
        Assert.NotNull(item.AudioIn("fa-IR"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"audio\":null")]
    [InlineData(",\"audio\":[]")]
    [InlineData(",\"audio\":{}")]
    [InlineData(",\"audio\":{\"fa\":5}")]
    [InlineData(",\"audio\":{\"fa\":{\"version\":0,\"url\":\"/u\"}}")]
    [InlineData(",\"audio\":{\"fa\":{\"version\":1.5,\"url\":\"/u\"}}")]
    [InlineData(",\"audio\":{\"fa\":{\"version\":1,\"url\":\"  \"}}")]
    [InlineData(",\"audio\":{\"fa\":{\"version\":1}}")]
    [InlineData(",\"audio\":{\"xx\":{\"version\":1,\"url\":\"/u\"}}")]
    public void Parse_ABadOrMissingAudioField_MeansNoPack_AndKeepsTheBook(string audio)
    {
        StoreItem item = Assert.Single(StoreCatalog.Parse(Item(audio)));

        Assert.Null(item.Audio);
        Assert.Null(item.AudioIn("fa"));
    }

    [Fact]
    public void Parse_OneBadPackDoesNotSpoilTheOthers_AndANonPositiveSizeIsUnknown()
    {
        StoreItem item = Assert.Single(StoreCatalog.Parse(Item(""","audio":{"fa":{"version":0,"url":"/u"},"nb":{"version":3,"bytes":-4,"url":"/u"}}""")));

        Assert.Null(item.AudioIn("fa"));
        Assert.Equal(new AudioPackInfo(3, null, "/u"), item.AudioIn("nb"));
    }
}
