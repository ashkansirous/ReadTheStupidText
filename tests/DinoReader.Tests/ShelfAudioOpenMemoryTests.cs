using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 285: what opening a book remembers (a recent listing, runs that failed).
public sealed class ShelfAudioOpenMemoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_listing_is_fresh_for_a_few_minutes_and_never_when_not_fetched()
    {
        Assert.False(ShelfAudioOpenMemory.ListingIsFresh(null, Start));
        Assert.True(ShelfAudioOpenMemory.ListingIsFresh(Start, Start.AddMinutes(4)));
        Assert.False(ShelfAudioOpenMemory.ListingIsFresh(Start, Start.AddMinutes(5)));
        Assert.False(ShelfAudioOpenMemory.ListingIsFresh(Start, Start.AddMinutes(-1)));
    }

    [Fact]
    public void A_failed_book_is_remembered_for_its_language_only_until_cleared()
    {
        var memory = new ShelfAudioOpenMemory();
        memory.MarkFailed("fa", "b1");

        Assert.True(memory.HasFailed("fa", "b1"));
        Assert.False(memory.HasFailed("fa", "b2"));
        Assert.False(memory.HasFailed("sv", "b1"));

        memory.Clear("fa", "b1");
        Assert.False(memory.HasFailed("fa", "b1"));
    }

    [Fact]
    public void A_failed_language_covers_every_book()
    {
        var memory = new ShelfAudioOpenMemory();
        memory.MarkFailed("fa", null);

        Assert.True(memory.HasFailed("fa", "any"));
        Assert.False(memory.HasFailed("sv", "any"));
    }
}
