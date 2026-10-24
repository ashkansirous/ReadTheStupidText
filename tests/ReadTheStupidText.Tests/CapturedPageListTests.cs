using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Tests;

public class CapturedPageListTests
{
    private static CapturedPageList ListOf(params string[] locations)
    {
        var list = new CapturedPageList();
        foreach (string location in locations)
        {
            list.Add(location);
        }

        return list;
    }

    [Fact]
    public void Add_keeps_capture_order_and_starts_reading()
    {
        CapturedPageList list = ListOf("a.jpg", "b.jpg", "c.jpg");

        Assert.Equal(["a.jpg", "b.jpg", "c.jpg"], list.Pages.Select(p => p.Location));
        Assert.All(list.Pages, p => Assert.Equal(CapturedPageState.Reading, p.State));
    }

    [Fact]
    public void Pages_settle_in_any_order_and_texts_stay_in_page_order()
    {
        CapturedPageList list = ListOf("a", "b", "c");

        list.Settle(list.Pages[2].Id, "third");
        list.Settle(list.Pages[0].Id, "first");
        list.Settle(list.Pages[1].Id, "second");

        Assert.Equal("first\n\nsecond\n\nthird", ScannedText.JoinPages(list.Texts()));
    }

    [Fact]
    public void Removing_the_middle_page_keeps_the_others_in_order()
    {
        CapturedPageList list = ListOf("a", "b", "c");
        list.Settle(list.Pages[0].Id, "one");
        list.Settle(list.Pages[1].Id, "two");
        list.Settle(list.Pages[2].Id, "three");

        CapturedPage? removed = list.Remove(list.Pages[1].Id);

        Assert.Equal("b", removed?.Location);
        Assert.Equal(2, list.Count);
        Assert.Equal("one\n\nthree", ScannedText.JoinPages(list.Texts()));
    }

    [Fact]
    public void Removing_an_unknown_page_returns_null()
    {
        Assert.Null(ListOf("a").Remove(Guid.NewGuid()));
    }

    [Fact]
    public void A_late_result_for_a_removed_page_is_ignored()
    {
        CapturedPageList list = ListOf("a", "b");
        Guid removedId = list.Pages[0].Id;
        list.Remove(removedId);

        list.Settle(removedId, "late");

        Assert.Equal(1, list.Count);
    }

    [Fact]
    public void A_failed_page_is_reported_by_its_number_and_skipped_in_the_join()
    {
        CapturedPageList list = ListOf("a", "b", "c");
        list.Settle(list.Pages[0].Id, "one");
        list.Settle(list.Pages[1].Id, null);
        list.Settle(list.Pages[2].Id, "three");

        IReadOnlyList<string?> texts = list.Texts();

        Assert.Equal(CapturedPageState.Failed, list.Pages[1].State);
        Assert.Equal([2], ScannedText.FailedPageNumbers(texts));
        Assert.Equal("one\n\nthree", ScannedText.JoinPages(texts));
    }

    [Fact]
    public void Failed_page_numbers_follow_removals()
    {
        CapturedPageList list = ListOf("a", "b", "c");
        list.Settle(list.Pages[0].Id, "one");
        list.Settle(list.Pages[1].Id, "two");
        list.Settle(list.Pages[2].Id, null);
        list.Remove(list.Pages[0].Id);

        Assert.Equal([2], ScannedText.FailedPageNumbers(list.Texts()));
    }
}
