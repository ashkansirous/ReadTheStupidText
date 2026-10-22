using DinoReader.Core;

namespace DinoReader.Tests;

public class PageOrderTests
{
    [Fact]
    public void Move_PutsTheItemAtTheTarget_AndIgnoresBadIndexes()
    {
        var items = new List<string> { "a", "b", "c", "d" };

        PageOrder.Move(items, 3, 0);
        Assert.Equal(["d", "a", "b", "c"], items);

        PageOrder.Move(items, 0, 2);
        Assert.Equal(["a", "b", "d", "c"], items);

        PageOrder.Move(items, 1, 9);
        PageOrder.Move(items, -1, 1);
        Assert.Equal(["a", "b", "d", "c"], items);
    }

    [Fact]
    public void Remove_AndInsertAt_Clamp()
    {
        var items = new List<string> { "a", "b", "c" };

        PageOrder.Remove(items, 1);
        PageOrder.Remove(items, 7);
        Assert.Equal(["a", "c"], items);

        PageOrder.InsertAt(items, 1, "x");
        PageOrder.InsertAt(items, 99, "y");
        Assert.Equal(["a", "x", "c", "y"], items);
    }

    [Theory]
    [InlineData(2, 2, 0)]
    [InlineData(2, 0, 3)]
    [InlineData(2, 3, 0)]
    [InlineData(2, 3, 1)]
    [InlineData(2, 0, 1)]
    [InlineData(2, 2, 2)]
    [InlineData(0, 0, 3)]
    public void CoverAfterMove_FollowsThePicture(int cover, int from, int to)
    {
        var items = new List<int> { 0, 1, 2, 3 };
        int coverItem = items[cover];

        PageOrder.Move(items, from, to);

        Assert.Equal(items.IndexOf(coverItem), PageOrder.CoverAfterMove(cover, from, to));
    }

    [Fact]
    public void CoverAfterRemoveAndInsert_ShiftOrDrop()
    {
        Assert.Equal(1, PageOrder.CoverAfterRemove(2, 0));
        Assert.Equal(2, PageOrder.CoverAfterRemove(2, 3));
        Assert.Null(PageOrder.CoverAfterRemove(2, 2));
        Assert.Null(PageOrder.CoverAfterRemove(null, 0));

        Assert.Equal(3, PageOrder.CoverAfterInsert(2, 1));
        Assert.Equal(3, PageOrder.CoverAfterInsert(2, 2));
        Assert.Equal(2, PageOrder.CoverAfterInsert(2, 3));
        Assert.Null(PageOrder.CoverAfterInsert(null, 0));
    }
}
