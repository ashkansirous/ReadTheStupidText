using ReadTheStupidText.Application.Crashes;
using Xunit;

namespace ReadTheStupidText.Tests.Crashes;

/// <summary>Slice 227, review pass 2: the send policy, the tail owner, the path scrub and the bounded tail read.</summary>
public sealed class CrashReviewFixTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rtst-reviewfix-" + Guid.NewGuid().ToString("N"));

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

    [Theory]
    [InlineData(200, true)]
    [InlineData(204, true)]
    [InlineData(400, true)]
    [InlineData(413, true)]
    [InlineData(422, true)]
    [InlineData(401, false)]
    [InlineData(403, false)]
    [InlineData(404, false)]
    [InlineData(405, false)]
    [InlineData(408, false)]
    [InlineData(429, false)]
    [InlineData(302, false)]
    [InlineData(500, false)]
    [InlineData(503, false)]
    public void A_pending_report_is_deleted_only_on_success_or_a_verdict_on_the_payload(int status, bool deleted)
    {
        Assert.Equal(deleted, CrashSendPolicy.DeleteAfter(status));
    }

    [Fact]
    public void A_network_failure_keeps_the_report()
    {
        Assert.False(CrashSendPolicy.DeleteAfter(null));
    }

    private static CrashReport Report(string type, string kind) => new("dino", "android", "1.83.0", "store", kind, type, "m", "s", "t", "2026-10-31T14:05Z");

    [Fact]
    public void Only_the_newest_managed_report_is_the_one_that_gets_the_previous_runs_tail()
    {
        var store = new PendingCrashStore(_folder);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        store.Save(Report("old", CrashKind.Managed), now);
        store.Save(Report("newest managed", CrashKind.Managed), now.AddSeconds(1));
        store.Save(Report("native", CrashKind.Native), now.AddSeconds(2));

        string? owner = store.NewestManagedPath();

        Assert.Equal("newest managed", store.Read(owner!)?.ExceptionType);
        Assert.Null(new PendingCrashStore(Path.Combine(_folder, "empty")).NewestManagedPath());
    }

    [Theory]
    [InlineData("HTTP/1.1 503 from the pack server")]
    [InlineData("failed for https://dinoreader.sirous.uk/api/stories/abc?x=1/2")]
    [InlineData("needs read and/or write, ratio 1/2, version 1.83.0/arm64")]
    public void Http_versions_urls_and_ratios_stay_readable(string message)
    {
        Assert.Equal(message, LogLineFormatter.ScrubMessage(message));
    }

    [Theory]
    [InlineData("could not open /data/user/0/uk.sirous.dinoreader/files/book.pdf now", "could not open <path> now")]
    [InlineData("could not open (/var/mobile/Containers/Data/x.pdf)", "could not open (<path>)")]
    [InlineData(@"could not open C:\Users\me\Documents\x.pdf please", "could not open <path> please")]
    [InlineData(@"could not open Documents\Grandma.pdf please", "could not open <path> please")]
    [InlineData("could not open file:///private/var/x/y.pdf please", "could not open <path> please")]
    [InlineData("/storage/emulated/0/x.pdf", "<path>")]
    public void Absolute_file_paths_are_still_scrubbed(string message, string expected)
    {
        Assert.Equal(expected, LogLineFormatter.ScrubMessage(message));
    }

    [Fact]
    public void The_tail_of_a_big_log_is_read_from_the_end_and_still_has_whole_lines_and_the_cap()
    {
        var log = new RollingLogFile(_folder, 100_000);
        for (int i = 0; i < 1500; i++)
        {
            log.Append($"line {i:D5} " + new string('y', 40), flush: false);
        }

        string tail = log.Tail(maxLines: 300, maxBytes: 4096);
        string[] lines = tail.Split('\n');

        Assert.True(System.Text.Encoding.UTF8.GetByteCount(tail) <= 4096);
        Assert.StartsWith("line 01499", lines[^1]);
        Assert.All(lines, line => Assert.StartsWith("line 0", line));
    }

    [Fact]
    public async Task The_background_tail_holds_only_what_was_written_before_it_was_asked_for()
    {
        var log = new RollingLogFile(_folder);
        log.Append("previous run", flush: true);

        Task<string> tail = log.TailInBackground();
        log.Append("this run", flush: true);

        Assert.Equal("previous run", await tail);
        Assert.EndsWith("this run", log.Tail());
    }
}
