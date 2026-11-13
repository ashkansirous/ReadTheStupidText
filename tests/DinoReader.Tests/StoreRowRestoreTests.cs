using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>The restore that puts the bought books on the shelf (Slice 364, mock S3): which books it fetches and which line it shows.</summary>
public class StoreRowRestoreTests
{
    [Theory]
    [InlineData(0, 0, 0, RestoreLine.NothingToRestore)]
    [InlineData(1, 1, 0, RestoreLine.RestoredOne)]
    [InlineData(2, 2, 0, RestoreLine.RestoredMany)]
    [InlineData(3, 2, 0, RestoreLine.RestoredMany)]
    [InlineData(2, 1, 0, RestoreLine.RestoredOne)]
    [InlineData(2, 0, 0, RestoreLine.None)]
    [InlineData(0, 1, 1, RestoreLine.RestoredOne)] // a book that was already arriving counts as restored
    [InlineData(1, 2, 1, RestoreLine.RestoredMany)]
    [InlineData(0, 0, 1, RestoreLine.None)] // the only book arriving failed: no "nothing to restore"
    public void The_line_follows_how_many_books_arrived(int fetched, int arrived, int alreadyDownloading, RestoreLine expected)
    {
        Assert.Equal(expected, StoreRowRestore.LineFor(fetched, arrived, alreadyDownloading));
    }

    [Theory]
    [InlineData(BookState.Owned, StoreAction.Get, true)]
    [InlineData(BookState.Owned, StoreAction.OnShelf, false)]
    [InlineData(BookState.Free, StoreAction.Get, false)]
    [InlineData(BookState.Buyable, StoreAction.Buy, false)]
    [InlineData(BookState.Pending, StoreAction.Waiting, false)]
    [InlineData(BookState.KeptFromBefore, StoreAction.OnShelf, false)]
    public void Only_an_owned_book_that_is_not_on_the_shelf_is_fetched(BookState state, StoreAction action, bool expected)
    {
        Assert.Equal(expected, StoreRowRestore.NeedsFetch(new StoreCardPlan(state, action, null)));
    }
}
