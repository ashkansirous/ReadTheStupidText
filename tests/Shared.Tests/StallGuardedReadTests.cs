using ReadTheStupidText.Application.Reading;
using Xunit;

namespace ReadTheStupidText.Tests;

public sealed class StallGuardedReadTests
{
    // Short stall against long waits: the assertions wait up to 10 s for the abandon work, so a loaded machine cannot flake them.
    private static readonly TimeSpan ShortStall = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan LongWait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task HungStreamThatIgnoresTheTokenTimesOutAndIsDisposedOffThread()
    {
        using var hung = new HungStream();
        await using Stream guarded = StallGuardedRead.Wrap(hung, ShortStall);

        await Assert.ThrowsAsync<TimeoutException>(async () => await guarded.ReadAsync(new byte[16]));

        await hung.WaitForDisposeAsync();
    }

    [Fact]
    public async Task StuckDisposeNeverBlocksTheCallerOrItsOwnDispose()
    {
        using var hung = new HungStream { BlockOnDispose = true };
        Stream guarded = StallGuardedRead.Wrap(hung, ShortStall);

        await Assert.ThrowsAsync<TimeoutException>(async () => await guarded.ReadAsync(new byte[16]));
        await guarded.DisposeAsync().AsTask().WaitAsync(LongWait); // the caller's own dispose must not touch the stuck stream

        await hung.WaitForDisposeStartedAsync();
        hung.ReleaseDispose();
    }

    [Fact]
    public async Task AbandonedStreamRefusesFurtherReads()
    {
        using var hung = new HungStream();
        await using Stream guarded = StallGuardedRead.Wrap(hung, ShortStall);
        await Assert.ThrowsAsync<TimeoutException>(async () => await guarded.ReadAsync(new byte[16]));

        await Assert.ThrowsAsync<TimeoutException>(async () => await guarded.ReadAsync(new byte[16]));
    }

    [Fact]
    public async Task NormalStreamIsReadToTheEndAndDisposedWithTheWrapper()
    {
        byte[] data = Enumerable.Range(0, 1000).Select(i => (byte)i).ToArray();
        var source = new TrackingMemoryStream(data);
        using var copy = new MemoryStream();
        byte[] buffer = new byte[64];

        await using (Stream guarded = StallGuardedRead.Wrap(source, LongWait))
        {
            int read;
            while ((read = await guarded.ReadAsync(buffer)) > 0)
            {
                copy.Write(buffer, 0, read);
            }
        }

        Assert.Equal(data, copy.ToArray());
        Assert.True(source.Disposed);
    }

    [Fact]
    public async Task CallerCancellationStillCancelsAndAbandons()
    {
        using var hung = new HungStream();
        await using Stream guarded = StallGuardedRead.Wrap(hung, LongWait);
        using var cts = new CancellationTokenSource(ShortStall);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await guarded.ReadAsync(new byte[16], cts.Token));

        await hung.WaitForDisposeAsync(); // the stuck read is abandoned and observed, not left behind
    }

    [Fact]
    public async Task ThrowingDisposeNeverHidesTheTimeout()
    {
        using var hung = new HungStream { ThrowOnDispose = true };
        await using Stream guarded = StallGuardedRead.Wrap(hung, ShortStall);

        await Assert.ThrowsAsync<TimeoutException>(async () => await guarded.ReadAsync(new byte[16]));

        await hung.WaitForDisposeAsync();
        hung.ThrowOnDispose = false; // so the test's own cleanup does not throw
    }

    [Fact]
    public async Task CooperativeStreamIsCancelledThroughItsToken()
    {
        using var cooperative = new HungStream { Cooperative = true };
        await using Stream guarded = StallGuardedRead.Wrap(cooperative, ShortStall);

        await Assert.ThrowsAsync<TimeoutException>(async () => await guarded.ReadAsync(new byte[16]));

        Assert.True(cooperative.TokenWasCancelled);
    }

    [Fact]
    public async Task HungSendTimesOutAndTheRequestIsDisposedOnlyAfterTheSendFinishes()
    {
        var handler = new HungHandler();
        var content = new TrackingContent();
        using var http = new HttpClient(handler);

        await Assert.ThrowsAsync<TimeoutException>(async () => await StallGuardedRead.SendAsync(
            http, () => new HttpRequestMessage(HttpMethod.Post, "http://example.test/") { Content = content }, ShortStall, CancellationToken.None));

        Assert.False(content.Disposed); // the abandoned send may still be using the request
        handler.Finish();
        await content.WaitForDisposeAsync();
        await handler.WaitForLateResponseDisposedAsync();
    }

    [Fact]
    public async Task SendReturnsTheResponseWhenHeadersArrive()
    {
        using var http = new HttpClient(new OkHandler());

        using GuardedResponse response = await StallGuardedRead.SendAsync(
            http, () => new HttpRequestMessage(HttpMethod.Get, "http://example.test/"), LongWait, CancellationToken.None);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.Message.StatusCode);
    }

    [Fact]
    public void HeaderTimeoutIsNeverShorterThanHttpClientsDefault()
    {
        Assert.Equal(TimeSpan.FromSeconds(100), StallGuardedRead.HeaderTimeout(TimeSpan.FromSeconds(30)));
        Assert.Equal(TimeSpan.FromSeconds(150), StallGuardedRead.HeaderTimeout(TimeSpan.FromSeconds(150)));
    }

    [Fact]
    public void AStallIsAFailedAttemptSoItRetries()
    {
        Assert.Equal(PackDownloadResult.Failed, PackErrorClassifier.Classify(new TimeoutException()));
    }

    private sealed class TrackingMemoryStream(byte[] data) : MemoryStream(data)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class TrackingContent : HttpContent
    {
        private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed => _disposed.Task.IsCompleted;
        public Task WaitForDisposeAsync() => _disposed.Task.WaitAsync(LongWait);
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => Task.CompletedTask;
        protected override bool TryComputeLength(out long length) { length = 0; return true; }

        protected override void Dispose(bool disposing)
        {
            _disposed.TrySetResult();
            base.Dispose(disposing);
        }
    }

    private sealed class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }

    // Never answers and ignores the token until Finish, then answers with a response nobody wants.
    private sealed class HungHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _go = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TrackingContent _lateBody = new();
        public void Finish() => _go.TrySetResult();
        public Task WaitForLateResponseDisposedAsync() => _lateBody.WaitForDisposeAsync();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await _go.Task;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = _lateBody };
        }
    }

    // ReadAsync never completes and ignores the token, like a blocking Java InputStream read.
    private sealed class HungStream : Stream
    {
        private readonly TaskCompletionSource<int> _never = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _disposeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _releaseDispose = new();
        public bool ThrowOnDispose { get; set; }
        public bool BlockOnDispose { get; init; }
        public bool Cooperative { get; init; }
        public bool TokenWasCancelled { get; private set; }
        public void ReleaseDispose() => _releaseDispose.Set();
        public Task WaitForDisposeStartedAsync() => _disposeStarted.Task.WaitAsync(LongWait);
        public Task WaitForDisposeAsync() => _disposed.Task.WaitAsync(LongWait);
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
            if (Cooperative)
            {
                cancellationToken.Register(() =>
                {
                    TokenWasCancelled = true;
                    _never.TrySetCanceled(cancellationToken);
                });
            }

            return new(_never.Task);
        }

        protected override void Dispose(bool disposing)
        {
            _disposeStarted.TrySetResult();
            if (BlockOnDispose)
            {
                _releaseDispose.Wait(TimeSpan.FromSeconds(30)); // a Close stuck on a dead socket
            }

            _never.TrySetException(new IOException("closed"));
            base.Dispose(disposing);
            _disposed.TrySetResult();
            if (ThrowOnDispose)
            {
                throw new ObjectDisposedException(nameof(HungStream));
            }
        }
    }
}
