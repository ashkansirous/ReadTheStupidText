using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 269: Slice 260's crash passed two reviews and an emulator run because no check fed the shelf a book carrying every new field. This fixture sets every optional field of a store
// listing, and the source test below fails on the pre-#805 StoreTagBadge (AutomationId set in Refresh(), which ran again whenever Tag changed).
public sealed class StoreEveryFieldFixtureTests
{
    private const string Id = "0123456789abcdef0123456789abcdef";

    private const string EveryField = $$$"""
        {"stories":[{"id":"{{{Id}}}","title":"Bear","language":"en","schemaVersion":1,"version":3,"pageCount":12,"minutes":5,"coverUrl":"/api/stories/x/cover?v=3",
        "downloadUrl":"/api/stories/x/download?v=3","credits":"Told by A","creditsByLanguage":{"fa":"B"},"ageMin":3,"ageMax":6,"author":"Anon",
        "titles":{"fa":"Khers"},"audio":{"fa":{"version":2,"bytes":1234,"url":"/a/fa?v=2"}},"tags":["Hot","Popular","Recommended","New"],"bytes":4096}]}
        """;

    // The old badge: Tag was a bindable property whose change handler ran Refresh(), and Refresh() assigned the AutomationId.
    private const string PreFix805Badge = """
        public sealed class StoreTagBadge : Border
        {
        	public StoreTagBadge()
        	{
        		Refresh();
        	}

        	private void Refresh()
        	{
        		BackgroundColor = Colors.Red;
        		AutomationId = $"store-tag-{Tag}";
        	}
        }
        """;

    [Fact]
    public void A_listing_with_every_optional_field_set_is_read_whole()
    {
        StoreItem item = Assert.Single(StoreCatalog.Parse(EveryField));

        Assert.Equal(12, item.PageCount);
        Assert.Equal(5, item.Minutes);
        Assert.Equal("Told by A", item.Credits);
        Assert.Equal("B", item.CreditsIn("fa"));
        Assert.Equal(3, item.AgeMin);
        Assert.Equal(6, item.AgeMax);
        Assert.Equal("Anon", item.Author);
        Assert.Equal("Khers", item.TitleIn("fa"));
        Assert.Equal(new AudioPackInfo(2, 1234, "/a/fa?v=2"), item.AudioIn("fa"));
        Assert.Equal(4096, item.Bytes);
        Assert.Equal([StoreTag.Hot, StoreTag.Popular, StoreTag.Recommended, StoreTag.New], item.TagList);
    }

    [Fact]
    public void The_card_shows_at_most_two_of_the_four_tags_in_list_order()
    {
        StoreItem item = Assert.Single(StoreCatalog.Parse(EveryField));

        Assert.Equal([StoreTag.Hot, StoreTag.Popular], item.Badges);
    }

    [Fact]
    public void The_badge_source_sets_its_AutomationId_only_in_the_constructor()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "DinoReader.App", "StoreTagBadge.cs"));

        Assert.False(SetsAutomationIdOutsideConstructor(source));
    }

    [Fact]
    public void The_same_check_fails_on_the_pre_805_badge()
    {
        Assert.True(SetsAutomationIdOutsideConstructor(PreFix805Badge));
    }

    // True when an AutomationId assignment appears after the "Refresh" method begins, i.e. in code that can run more than once.
    private static bool SetsAutomationIdOutsideConstructor(string source)
    {
        int refresh = source.IndexOf("void Refresh()", StringComparison.Ordinal);
        int assign = source.LastIndexOf("AutomationId =", StringComparison.Ordinal);
        return refresh >= 0 && assign > refresh;
    }

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src", "DinoReader.App")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new DirectoryNotFoundException("Repo root not found.");
    }
}
