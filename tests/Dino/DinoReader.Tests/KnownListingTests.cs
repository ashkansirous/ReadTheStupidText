using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 360, "Expectation rule after review": which listing is the known one, and reading the saved copy back.
public sealed class KnownListingTests
{
    private static StoreItem Item(string id) => new(id, "T", "en", 1, 1, 5, 3, null, "https://x/" + id);

    [Fact]
    public void When_this_launch_read_a_live_listing_that_leaves_the_book_out_the_saved_entry_is_not_used()
    {
        Assert.Null(KnownListing.Pick(liveListingRead: true, live: null, saved: Item("pigs")));
    }

    [Fact]
    public void When_no_live_listing_was_read_the_saved_entry_is_used()
    {
        StoreItem saved = Item("pigs");
        Assert.Same(saved, KnownListing.Pick(liveListingRead: false, live: null, saved: saved));
    }

    [Fact]
    public void A_live_entry_always_wins()
    {
        StoreItem live = Item("pigs");
        Assert.Same(live, KnownListing.Pick(true, live, Item("pigs")));
        Assert.Same(live, KnownListing.Pick(false, live, Item("pigs")));
    }

    [Fact]
    public void A_saved_listing_that_parses_gives_its_readable_entries_by_id()
    {
        string json = """{"stories":[{"id":"0123456789abcdef0123456789abcdef","title":"Pigs","language":"en","schemaVersion":1,"version":1,"downloadUrl":"https://x/pigs"}]}""";
        IReadOnlyDictionary<string, StoreItem>? saved = KnownListing.ParseSaved(json);
        Assert.NotNull(saved);
        Assert.True(saved!.ContainsKey("0123456789ABCDEF0123456789ABCDEF"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{\"stories\":[{\"id\":\"pi")]
    [InlineData("[1,2,3]")]
    [InlineData("not json")]
    public void A_saved_file_that_does_not_parse_is_no_saved_listing(string? json)
    {
        Assert.Null(KnownListing.ParseSaved(json));
    }

    [Theory]
    [InlineData("""{"stories":[]}""", false)]
    [InlineData("""{"stories":[{"id":"bad"}]}""", false)]
    [InlineData("not json", false)]
    [InlineData("""{"stories":[{"id":"0123456789abcdef0123456789abcdef","title":"Pigs","language":"en","schemaVersion":1,"version":1,"downloadUrl":"https://x/pigs"}]}""", true)]
    public void Only_a_listing_with_at_least_one_readable_book_may_replace_the_saved_one(string json, bool savable)
    {
        Assert.Equal(savable, KnownListing.IsSavable(json));
    }

    [Fact]
    public void A_saved_file_over_the_size_cap_is_no_saved_listing()
    {
        string huge = "{\"stories\":[]," + new string(' ', (int)KnownListing.MaxSavedBytes) + "}";
        Assert.Null(KnownListing.ParseSaved(huge));
    }
}
