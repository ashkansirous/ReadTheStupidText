using DinoReader;
using Xunit;

namespace DinoReader.Tests;

public class PurchaseStampsTests
{
    private const string Book = "uk.sirous.dinoreader.book.goldilocks";
    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    private static IReadOnlySet<string> Set(params string[] items) => new HashSet<string>(items);

    [Fact]
    public void A_purchase_that_ended_bought_is_stamped_until_the_window_ends()
    {
        long now = 0;
        var stamps = new PurchaseStamps(() => now);
        stamps.NoteBought(Book);
        now = System.Diagnostics.Stopwatch.Frequency * 10;
        Assert.Equal(TimeSpan.FromSeconds(10), stamps.SinceBought([Book]));
        now = (long)(System.Diagnostics.Stopwatch.Frequency * (PurchaseLagRetry.Window.TotalSeconds + 1));
        Assert.Null(stamps.SinceBought([Book]));
    }

    [Fact]
    public void A_pending_purchase_of_this_run_is_stamped_when_a_read_reports_it_owned()
    {
        var stamps = new PurchaseStamps(() => 0);
        stamps.NotePending(Book);
        stamps.NoteRead(None, Set(Book));
        Assert.Null(stamps.SinceBought([Book]));
        stamps.NoteRead(Set(Book), None);
        Assert.NotNull(stamps.SinceBought([Book]));
    }

    [Fact]
    public void A_restore_or_sync_that_finds_an_older_purchase_stamps_nothing()
    {
        var stamps = new PurchaseStamps(() => 0);
        stamps.NoteRead(None, None);
        stamps.NoteRead(Set(Book), None);
        Assert.Null(stamps.SinceBought([Book]));
    }

    [Fact]
    public void One_read_that_lists_a_pending_purchase_as_neither_owned_nor_pending_does_not_drop_it()
    {
        long now = 0;
        var stamps = new PurchaseStamps(() => now);
        stamps.NotePending(Book);
        stamps.NoteRead(None, None);
        now = System.Diagnostics.Stopwatch.Frequency * 30;
        stamps.NoteRead(Set(Book), None);
        Assert.NotNull(stamps.SinceBought([Book]));
    }

    [Fact]
    public void A_purchase_kept_pending_past_the_window_is_forgotten_even_while_the_store_lists_it_pending()
    {
        long now = 0;
        var stamps = new PurchaseStamps(() => now);
        stamps.NotePending(Book);
        now = (long)(System.Diagnostics.Stopwatch.Frequency * (PurchaseLagRetry.Window.TotalSeconds + 1));
        stamps.NoteRead(None, Set(Book));
        stamps.NoteRead(Set(Book), None);
        Assert.Null(stamps.SinceBought([Book]));
    }

    [Fact]
    public void A_pending_purchase_never_owned_is_forgotten_once_the_window_ends()
    {
        long now = 0;
        var stamps = new PurchaseStamps(() => now);
        stamps.NotePending(Book);
        now = (long)(System.Diagnostics.Stopwatch.Frequency * (PurchaseLagRetry.Window.TotalSeconds + 1));
        stamps.NoteRead(None, None);
        stamps.NoteRead(Set(Book), None);
        Assert.Null(stamps.SinceBought([Book]));
    }
}
