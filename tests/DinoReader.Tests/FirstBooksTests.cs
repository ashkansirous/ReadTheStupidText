using DinoReader.Core;

namespace DinoReader.Tests;

public class FirstBooksTests
{
    private static StoreItem Item(string id, long? bytes = null, params StoreTag[] tags) =>
        new(id, id, "en", 1, 1, null, null, null, "/api/stories/" + id, Tags: tags, Bytes: bytes);

    private static HashSet<string> Ids(params string[] ids) => new(ids, StringComparer.OrdinalIgnoreCase);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(4, true)]
    public void The_first_run_book_step_is_done_from_two_books_on_the_shelf(int onShelf, bool expected)
    {
        Assert.Equal(expected, FirstBooks.IsSatisfied(onShelf));
        Assert.Equal(expected, FirstBooks.CanContinue(onShelf));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(5, 0)]
    [InlineData(-3, 2)]
    public void The_count_line_says_how_many_more_ticks_are_needed(int ticked, int expected)
    {
        Assert.Equal(expected, FirstBooks.StillNeeded(ticked));
    }

    [Fact]
    public void Tagged_books_come_first_and_each_group_keeps_the_listing_order()
    {
        StoreItem[] listing = [Item("a"), Item("b", null, StoreTag.Popular), Item("c"), Item("d", null, StoreTag.Hot)];

        IReadOnlyList<StoreItem> ordered = FirstBooks.Order(listing);

        Assert.Equal(["b", "d", "a", "c"], ordered.Select(i => i.Id));
    }

    [Fact]
    public void Books_already_on_the_shelf_come_back_ticked_after_a_kill_mid_download()
    {
        StoreItem[] listing = [Item("a"), Item("b"), Item("c")];

        Assert.Equal(["b"], FirstBooks.AlreadyOnShelf(listing, Ids("b", "not-listed")));
    }

    [Fact]
    public void Only_ticked_books_not_yet_on_the_shelf_are_fetched_in_the_pickers_order()
    {
        StoreItem[] listing = [Item("a"), Item("b"), Item("c"), Item("d")];

        IReadOnlyList<StoreItem> toFetch = FirstBooks.ToDownload(listing, Ids("d", "b", "a"), Ids("b"));

        Assert.Equal(["a", "d"], toFetch.Select(i => i.Id));
    }

    [Fact]
    public void The_shelf_match_ignores_the_case_of_an_id()
    {
        StoreItem[] listing = [Item("ABC"), Item("def")];

        Assert.Empty(FirstBooks.ToDownload(listing, Ids("abc"), Ids("ABC")));
    }

    [Fact]
    public void The_total_size_sums_the_ticked_books_and_is_unknown_when_any_has_none()
    {
        StoreItem[] listing = [Item("a", 4_000_000), Item("b", 5_000_000), Item("c")];

        Assert.Equal(9_000_000L, FirstBooks.TotalBytes(listing, Ids("a", "b")));
        Assert.Null(FirstBooks.TotalBytes(listing, Ids("a", "c")));
        Assert.Null(FirstBooks.TotalBytes(listing, Ids()));
    }

    [Theory]
    [InlineData(0, 2, 1)]
    [InlineData(1, 2, 2)]
    [InlineData(2, 2, 2)]
    [InlineData(0, 0, 1)]
    public void The_book_number_of_the_wait_never_passes_the_total(int landed, int total, int expected)
    {
        Assert.Equal(expected, FirstBooks.BookNumber(landed, total));
    }
}
