using ReadTheStupidText.Application.Crashes;
using Xunit;

namespace ReadTheStupidText.Tests.Crashes;

public sealed class RollingLogFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rtst-roll-" + Guid.NewGuid().ToString("N"));

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

    [Fact]
    public void Lines_come_back_oldest_first_and_a_warning_is_on_disk_at_once()
    {
        var log = new RollingLogFile(_folder);
        log.Append("one", flush: false);
        log.Append("two", flush: true);

        Assert.Equal("one\ntwo", new RollingLogFile(_folder).Tail());
    }

    [Fact]
    public void The_two_files_swap_when_full_so_the_disk_use_stays_bounded_and_the_tail_is_the_newest()
    {
        const int fileBytes = 2000;
        var log = new RollingLogFile(_folder, fileBytes);
        for (int i = 0; i < 400; i++)
        {
            log.Append($"line {i:D4} " + new string('x', 40), flush: false);
        }

        log.Flush();
        string[] files = Directory.GetFiles(_folder);
        Assert.Equal(2, files.Length);
        Assert.All(files, f => Assert.True(new FileInfo(f).Length < fileBytes + 200));
        string[] lines = log.Tail(maxLines: 20).Split('\n');
        Assert.Equal(20, lines.Length);
        Assert.StartsWith("line 0399", lines[^1]);
        Assert.StartsWith("line 0380", lines[0]);
    }

    [Fact]
    public void The_tail_is_capped_in_bytes_and_a_fresh_instance_carries_on_in_the_newest_file()
    {
        var first = new RollingLogFile(_folder, 4000);
        for (int i = 0; i < 100; i++)
        {
            first.Append($"first {i:D3}", flush: false);
        }

        first.Flush();
        var second = new RollingLogFile(_folder, 4000);
        second.Append("second run", flush: true);

        string tail = second.Tail(maxLines: 300, maxBytes: 64);

        Assert.True(System.Text.Encoding.UTF8.GetByteCount(tail) <= 64);
        Assert.EndsWith("second run", tail);
        Assert.Contains("first 099", tail);
    }

    [Fact]
    public void A_log_that_cannot_be_written_never_throws()
    {
        Directory.CreateDirectory(_folder);
        string blocked = Path.Combine(_folder, "file");
        File.WriteAllText(blocked, "a file where the folder should be");
        var log = new RollingLogFile(blocked);

        log.Append("lost", flush: true);
        log.Flush();

        Assert.Equal(string.Empty, log.Tail());
    }
}
