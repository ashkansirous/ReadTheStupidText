using ReadTheStupidText.Application.Crashes;
using Xunit;

namespace ReadTheStupidText.Tests.Crashes;

public sealed class PendingCrashStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rtst-pending-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
            // a temp folder left behind is harmless
        }
    }

    private static CrashReport Report(string type) => new("dino", "android", "1.83.0", "store", "managed", type, "m", "s", "t", "2026-10-31T14:05Z");

    [Fact]
    public void A_saved_report_is_listed_read_and_deleted()
    {
        var store = new PendingCrashStore(_folder);

        Assert.True(store.Save(Report("A"), DateTimeOffset.UtcNow));

        string path = Assert.Single(store.List());
        Assert.Equal("A", store.Read(path)?.ExceptionType);
        Assert.NotNull(store.ReadBody(path));
        store.Delete(path);
        Assert.Empty(store.List());
    }

    [Fact]
    public void At_most_ten_are_kept_and_the_oldest_are_dropped_first()
    {
        var store = new PendingCrashStore(_folder);
        DateTimeOffset start = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        for (int i = 0; i < 14; i++)
        {
            store.Save(Report($"T{i:D2}"), start.AddMinutes(i));
        }

        IReadOnlyList<string> kept = store.List();

        Assert.Equal(PendingCrashStore.MaxPending, kept.Count);
        Assert.Equal("T04", store.Read(kept[0])?.ExceptionType);
        Assert.Equal("T13", store.Read(kept[^1])?.ExceptionType);
    }

    [Fact]
    public void Clear_removes_every_pending_report_and_a_new_one_can_still_be_saved()
    {
        var store = new PendingCrashStore(_folder);
        store.Save(Report("A"), DateTimeOffset.UtcNow);
        store.Save(Report("B"), DateTimeOffset.UtcNow.AddSeconds(1));

        store.Clear();

        Assert.Empty(store.List());
        Assert.True(store.Save(Report("C"), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_damaged_file_has_no_body_and_does_not_throw()
    {
        Directory.CreateDirectory(_folder);
        string path = Path.Combine(_folder, "pending-00000000000000000001-x.json");
        File.WriteAllText(path, "{ broken");
        var store = new PendingCrashStore(_folder);

        Assert.Null(store.Read(path));
        Assert.Null(store.ReadBody(path));
        Assert.Null(store.ReadBody(Path.Combine(_folder, "missing.json")));
    }

    [Fact]
    public void The_run_marker_shows_an_unclean_exit_once_and_a_clean_background_clears_it()
    {
        var store = new PendingCrashStore(_folder);
        Assert.Null(store.TakeUncleanExit());

        store.MarkRunning();
        DateTimeOffset? since = store.TakeUncleanExit();
        Assert.NotNull(since);
        Assert.True(DateTimeOffset.UtcNow - since.Value < TimeSpan.FromMinutes(1));
        Assert.Null(store.TakeUncleanExit());

        store.MarkRunning();
        store.MarkNotRunning();
        Assert.Null(store.TakeUncleanExit());
    }

    [Fact]
    public void A_store_in_an_unwritable_place_fails_quietly()
    {
        Directory.CreateDirectory(_folder);
        string blocked = Path.Combine(_folder, "file");
        File.WriteAllText(blocked, "x");
        var store = new PendingCrashStore(blocked);

        Assert.False(store.Save(Report("A"), DateTimeOffset.UtcNow));
        Assert.Empty(store.List());
        store.MarkRunning();
        Assert.Null(store.TakeUncleanExit());
    }

    [Fact]
    public void A_report_without_a_log_tail_gets_the_previous_runs_tail_when_it_is_read_for_sending()
    {
        var store = new PendingCrashStore(_folder);
        store.Save(Report("A") with { LogTail = string.Empty }, DateTimeOffset.UtcNow);
        store.Save(Report("B") with { LogTail = "its own tail" }, DateTimeOffset.UtcNow.AddSeconds(1));
        IReadOnlyList<string> files = store.List();

        Assert.Equal("last run end", CrashReportJson.Parse(store.ReadBody(files[0], "last run end"))?.LogTail);
        Assert.Equal("its own tail", CrashReportJson.Parse(store.ReadBody(files[1], "last run end"))?.LogTail);
        Assert.Equal(string.Empty, CrashReportJson.Parse(store.ReadBody(files[0]))?.LogTail);
    }

    [Fact]
    public void A_diagnostic_the_system_delivered_is_never_given_the_previous_runs_tail()
    {
        var store = new PendingCrashStore(_folder);
        store.Save(Report("A") with { Kind = CrashKind.Native, LogTail = string.Empty }, DateTimeOffset.UtcNow);

        Assert.Equal(string.Empty, CrashReportJson.Parse(store.ReadBody(store.List()[0], "last run end"))?.LogTail);
    }

    [Fact]
    public void Every_readable_report_is_listed_with_its_file_and_a_damaged_one_is_skipped()
    {
        var store = new PendingCrashStore(_folder);
        store.Save(Report("A"), DateTimeOffset.UtcNow);
        File.WriteAllText(Path.Combine(_folder, "pending-99999999999999999999-x.json"), "{ broken");

        (string path, CrashReport report) = Assert.Single(store.ReadAll());

        Assert.Equal("A", report.ExceptionType);
        Assert.Equal(store.List()[0], path);
    }
}
