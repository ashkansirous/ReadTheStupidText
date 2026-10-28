using System.Collections.Concurrent;
using ReadTheStupidText.Application.Reading;
using Xunit;

namespace ReadTheStupidText.Tests;

public sealed class PackDownloadLogTests
{
    private static readonly TimeSpan ShortStall = TimeSpan.FromMilliseconds(200);

    [Fact]
    public void LinesHaveTheDocumentedFormats()
    {
        List<string> lines = Capture(() =>
        {
            PackDownloadLog.Start("p1", 5);
            PackDownloadLog.Range("p1", 5);
            PackDownloadLog.Stall(PackDownloadLog.HeadersPhase, 0, "p1");
            PackDownloadLog.Retry("p1", 2, PackDownloadResult.Failed, TimeSpan.FromSeconds(1.5));
            PackDownloadLog.Done("p1", 100, TimeSpan.FromSeconds(2));
            PackDownloadLog.Failed("p1", 3, PackDownloadResult.Offline);
        });

        lines = lines.Where(line => line.Contains("pack=p1")).ToList(); // other tests may write to the shared sink at the same time

        Assert.Equal(
            [
                "PACKDL start pack=p1 offset=5",
                "PACKDL range pack=p1 offset=5",
                "PACKDL stall pack=p1 phase=headers bytes=0",
                "PACKDL retry pack=p1 attempt=2 result=Failed backoffSec=1.5",
                "PACKDL done pack=p1 bytes=100 seconds=2",
                "PACKDL failed pack=p1 attempts=3 result=Offline",
            ],
            lines);
    }

    [Fact]
    public void StallLineWithoutAPackOmitsIt()
    {
        List<string> lines = Capture(() => PackDownloadLog.Stall(PackDownloadLog.BodyPhase, 987654));

        Assert.Contains("PACKDL stall phase=body bytes=987654", lines);
    }

    [Fact]
    public void ThrowingSinkNeverEscapes()
    {
        PackDownloadLog.Sink = _ => throw new InvalidOperationException("sink");
        try
        {
            PackDownloadLog.Start("p-throw", 0);
        }
        finally
        {
            PackDownloadLog.Sink = null;
        }
    }

    [Fact]
    public async Task BodyStallReportsPackAndTotalPositionIncludingTheResumeOffset()
    {
        using var stream = new ThenHangStream(10);
        await using Stream guarded = StallGuardedRead.Wrap(stream, ShortStall, new DownloadTag("p-body", 1000));

        List<string> lines = await CaptureAsync(async () =>
        {
            Assert.Equal(10, await guarded.ReadAsync(new byte[64]));
            await Assert.ThrowsAsync<TimeoutException>(async () => await guarded.ReadAsync(new byte[64]));
        });

        Assert.Contains("PACKDL stall pack=p-body phase=body bytes=1010", lines);
    }

    [Fact]
    public async Task UserCancelIsNotLoggedAsAStall()
    {
        using var stream = new ThenHangStream(0);
        using var cts = new CancellationTokenSource();
        await using Stream guarded = StallGuardedRead.Wrap(stream, TimeSpan.FromSeconds(30), new DownloadTag("p-cancel", 0));

        List<string> lines = await CaptureAsync(async () =>
        {
            ValueTask<int> read = guarded.ReadAsync(new byte[16], cts.Token);
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await read);
        });

        Assert.DoesNotContain(lines, line => line.Contains("stall"));
    }

    private static List<string> Capture(Action action) => CaptureAsync(() => { action(); return Task.CompletedTask; }).GetAwaiter().GetResult();

    private static async Task<List<string>> CaptureAsync(Func<Task> action)
    {
        var lines = new ConcurrentQueue<string>();
        PackDownloadLog.Sink = lines.Enqueue;
        try
        {
            await action();
        }
        finally
        {
            PackDownloadLog.Sink = null;
        }

        return lines.ToList();
    }

    // Returns the given bytes on the first read, then never completes (ignoring its token, like a dead socket).
    private sealed class ThenHangStream(int firstBytes) : Stream
    {
        private readonly TaskCompletionSource<int> _never = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _served;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_served && firstBytes > 0)
            {
                _served = true;
                return new(firstBytes);
            }

            return new(_never.Task);
        }
    }
}
