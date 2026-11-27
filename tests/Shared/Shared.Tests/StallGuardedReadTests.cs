using Sirous.Reading.Application.Reading;
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

        await Assert.ThrowsAsync<TimeoutException>(async () => await guarded.ReadAsync(new byte[16], TestContext.Current.CancellationToken));

        await hung.WaitForDisposeAsync();
    }

    [Fact]
    public async Task StuckDisposeNeverBlocksTheCallerOrItsOwnDispose()
    {
        using var hung = new HungStream { BlockOnDispose = true };
        Stream guarded = StallGuardedRead.Wrap(hung, ShortStall);

        await Assert.ThrowsAsync<TimeoutException>(async () => await guarded.ReadAsync(new byte[16], TestContext.Current.CancellationToken));
        await guarded.DisposeAsync().AsTask().WaitAsync(LongWait, TestContext.Current.CancellationToken); // the caller's own dispose must not touch the stuck stream

        await hung.WaitForDisposeStartedAsync();
        hung.ReleaseDispose();
    }

    [Fact]
    public async Task AbandonedStreamRefusesFurtherReads()
    {
        using var hung = new HungStream();
        await using Stream guarded = StallGuardedRead.Wrap(hung, ShortStall);
        await Assert.ThrowsAsync<TimeoutException>(async () => await guarded.ReadAsync(new byte[16], TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<TimeoutException>(async () => await guarded.ReadAsync(new byte[16], TestContext.Current.CancellationToken));
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
            while ((read = await guarded.ReadAsync(buffer, TestContext.Current.CancellationToken)) > 0)
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

        await Assert.ThrowsAsync<TimeoutException>(async () => await guarded.ReadAsync(new byte[16], TestContext.Current.CancellationToken));

        await hung.WaitForDisposeAsync();
        hung.ThrowOnDispose = false; // so the test's own cleanup does not throw
    }

    [Fact]
    public async Task CooperativeStreamIsCancelledThroughItsToken()
    {
        using var cooperative = new HungStream { Cooperative = true };
        await using Stream guarded = StallGuardedRead.Wrap(cooperative, ShortStall);

        await Assert.ThrowsAsync<TimeoutException>(async () => await guarded.ReadAsync(new byte[16], TestContext.Current.CancellationToken));

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

    [Fact]
    public async Task BodyNotReadToItsEndIsClosedOffTheCallingThread()
    {
        var source = new ThreadRecordingStream(new byte[100]);
        Stream guarded = StallGuardedRead.Wrap(source, LongWait);
        Assert.True(await guarded.ReadAsync(new byte[10], TestContext.Current.CancellationToken) > 0);
        source.HoldDispose = true; // a close that drains a socket takes time: run inline it would block here until released (5 s) and be finished when DisposeAsync returns

        await guarded.DisposeAsync();

        Assert.False(source.Disposed); // Slice 400: deterministic, no timing threshold: the close is still pending on the pool
        source.ReleaseDispose();
        await source.WaitForDisposeAsync();
    }

    [Fact]
    public async Task ReadWithAnEmptyBufferDoesNotCountAsTheEndOfTheBody()
    {
        var source = new ThreadRecordingStream(new byte[100]);
        Stream guarded = StallGuardedRead.Wrap(source, LongWait);
        Assert.Equal(0, await guarded.ReadAsync(Memory<byte>.Empty, TestContext.Current.CancellationToken));
        source.HoldDispose = true;

        await guarded.DisposeAsync();

        Assert.False(source.Disposed);
        source.ReleaseDispose();
        await source.WaitForDisposeAsync();
    }

    [Fact]
    public async Task UnfinishedBodyAndItsResponseAreClosedByOneOffThreadTaskInOrder()
    {
        var order = new CloseOrder();
        var source = new ThreadRecordingStream(new byte[100], order);
        using GuardedResponse response = new(new HttpResponseMessage { Content = new OrderedContent(source, order) });
        var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using IDisposable capture = PackDownloadLog.CaptureScope(lines.Enqueue);
        Stream body = await response.OpenBodyAsync(LongWait, TestContext.Current.CancellationToken);
        Assert.True(await body.ReadAsync(new byte[10], TestContext.Current.CancellationToken) > 0);
        source.HoldDispose = true;

        await body.DisposeAsync(); // the stream leaves the close to its response
        response.Dispose();

        Assert.False(source.Disposed); // still pending on the pool, not run on this thread
        source.ReleaseDispose();
        await order.WaitForCountAsync(2);
        Assert.Equal(["body", "response"], order.Items);
        Assert.Equal(order.ThreadIds[0], order.ThreadIds[1]); // one task, never two closing the same stream
        Assert.Single(lines, l => l.Contains($"{PackDownloadLog.CloseOffThreadEvent} what={PackDownloadLog.ResponsePart}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FinishedBodyAndItsResponseAreClosedInlineInOrder()
    {
        var order = new CloseOrder();
        var source = new ThreadRecordingStream(new byte[100], order);
        var response = new GuardedResponse(new HttpResponseMessage { Content = new OrderedContent(source, order) });
        var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using IDisposable capture = PackDownloadLog.CaptureScope(lines.Enqueue);
        Stream body = await response.OpenBodyAsync(LongWait, TestContext.Current.CancellationToken);
        byte[] buffer = new byte[64];
        while (await body.ReadAsync(buffer, TestContext.Current.CancellationToken) > 0)
        {
        }

        await body.DisposeAsync();
        response.Dispose();

        Assert.Equal(["body", "response"], order.Items); // already finished when Dispose returned: inline
        Assert.DoesNotContain(lines, l => l.Contains(PackDownloadLog.CloseOffThreadEvent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResponseWhoseBodyWasNeverOpenedIsClosedOffThreadWithoutALogLine()
    {
        var order = new CloseOrder();
        using var gate = new ManualResetEventSlim();
        var response = new GuardedResponse(new HttpResponseMessage { Content = new OrderedContent(new MemoryStream(), order) { Gate = gate } });
        var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using IDisposable capture = PackDownloadLog.CaptureScope(lines.Enqueue);

        response.Dispose(); // e.g. a refusal whose body is never read: the live stream may drain on close

        Assert.Empty(order.Items); // still pending on the pool, not run on this thread
        gate.Set();
        await order.WaitForCountAsync(1);
        Assert.Equal(["response"], order.Items);
        Assert.Empty(lines); // no body was opened: no close-off-thread line
    }

    [Fact]
    public async Task ErrorBodyReadOnlyToItsCapIsClosedOffThreadWithItsResponse()
    {
        // The StoreClient 400-message path: the body is opened through the guard, read up to a cap (not to its end) and the stream disposed, then the response.
        var order = new CloseOrder();
        var source = new ThreadRecordingStream(new byte[5000], order);
        var response = new GuardedResponse(new HttpResponseMessage { Content = new OrderedContent(source, order) });
        await using (Stream body = await response.OpenBodyAsync(LongWait, TestContext.Current.CancellationToken))
        {
            Assert.True(await body.ReadAsync(new byte[2048], TestContext.Current.CancellationToken) > 0);
        }

        source.HoldDispose = true;
        response.Dispose();

        Assert.False(source.Disposed); // pending on the pool, not run on this thread
        source.ReleaseDispose();
        await order.WaitForCountAsync(2);
        Assert.Equal(["body", "response"], order.Items);
    }

    [Fact]
    public async Task SecondOpenBodyIsRefusedAndTheFirstStreamIsStillClosedWithTheResponse()
    {
        var order = new CloseOrder();
        var source = new ThreadRecordingStream(new byte[100], order);
        var response = new GuardedResponse(new HttpResponseMessage { Content = new OrderedContent(source, order) });
        Stream first = await response.OpenBodyAsync(LongWait, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => response.OpenBodyAsync(LongWait, TestContext.Current.CancellationToken));
        Assert.True(await first.ReadAsync(new byte[10], TestContext.Current.CancellationToken) > 0); // the first stream still works
        response.Dispose();

        await order.WaitForCountAsync(2);
        Assert.Equal(["body", "response"], order.Items);
    }

    [Fact]
    public async Task BodyReadToItsContentLengthWithoutAFinalZeroReadIsClosedInline()
    {
        var response = new GuardedResponse(new HttpResponseMessage { Content = new ByteArrayContent(new byte[100]) });
        var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using IDisposable capture = PackDownloadLog.CaptureScope(lines.Enqueue);
        Stream body = await response.OpenBodyAsync(LongWait, TestContext.Current.CancellationToken);
        Assert.Equal(100, await body.ReadAsync(new byte[100], TestContext.Current.CancellationToken));

        await body.DisposeAsync();
        response.Dispose();

        Assert.DoesNotContain(lines, l => l.Contains(PackDownloadLog.CloseOffThreadEvent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task BodyShortOfItsContentLengthIsClosedOffThread()
    {
        var response = new GuardedResponse(new HttpResponseMessage { Content = new ByteArrayContent(new byte[100]) });
        var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using IDisposable capture = PackDownloadLog.CaptureScope(lines.Enqueue);
        Stream body = await response.OpenBodyAsync(LongWait, TestContext.Current.CancellationToken);
        Assert.Equal(10, await body.ReadAsync(new byte[10], TestContext.Current.CancellationToken));

        await body.DisposeAsync();
        response.Dispose();

        Assert.Single(lines, l => l.Contains($"{PackDownloadLog.CloseOffThreadEvent} what={PackDownloadLog.ResponsePart}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DisposeAfterAStallAbandonDoesNothingMoreAndClosesOnce()
    {
        var order = new CloseOrder();
        var hung = new HungStream();
        var response = new GuardedResponse(new HttpResponseMessage { Content = new OrderedContent(hung, order) });
        Stream body = await response.OpenBodyAsync(ShortStall, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<TimeoutException>(async () => await body.ReadAsync(new byte[10], TestContext.Current.CancellationToken));

        await body.DisposeAsync();
        response.Dispose();
        response.Dispose();

        await hung.WaitForDisposeAsync(); // the abandon task closed the stuck body
        await order.WaitForCountAsync(1);
        Assert.Equal(["response"], order.Items); // and then the response, once
    }

    private sealed class CloseOrder
    {
        private readonly object _gate = new();
        private readonly List<string> _items = [];
        private readonly List<int> _threadIds = [];
        public IReadOnlyList<string> Items { get { lock (_gate) { return [.. _items]; } } }
        public IReadOnlyList<int> ThreadIds { get { lock (_gate) { return [.. _threadIds]; } } }

        public void Add(string what)
        {
            lock (_gate)
            {
                _items.Add(what);
                _threadIds.Add(Environment.CurrentManagedThreadId);
            }
        }

        public async Task WaitForCountAsync(int count)
        {
            DateTime stop = DateTime.UtcNow + LongWait;
            while (Items.Count < count && DateTime.UtcNow < stop)
            {
                await Task.Delay(10);
            }
        }
    }

    // Hands out the recording stream as its body and records when the response content is closed.
    private sealed class OrderedContent : HttpContent
    {
        private readonly Stream _body;
        private readonly CloseOrder _order;
        private readonly long? _length;

        /// <summary>When set, the close waits here: a close that is still pending proves it did not run on the calling thread.</summary>
        public ManualResetEventSlim? Gate { get; set; }

        // No length: only a 0 read ends the body.
        public OrderedContent(Stream body, CloseOrder order, long? length = null, string? encoding = null)
        {
            _body = body;
            _order = order;
            _length = length;
            if (encoding is not null)
            {
                Headers.ContentEncoding.Add(encoding);
            }
        }

        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult(_body);
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => Task.CompletedTask;
        protected override bool TryComputeLength(out long length) { length = _length ?? 0; return _length is not null; }

        protected override void Dispose(bool disposing)
        {
            Gate?.Wait(LongWait);
            _order.Add("response");
            base.Dispose(disposing);
        }
    }

    [Fact]
    public async Task UnopenedResponseWithNoContentIsClosedInline()
    {
        var order = new CloseOrder();
        var response = new GuardedResponse(new HttpResponseMessage { Content = new OrderedContent(new MemoryStream(), order, 0) });

        response.Dispose();

        Assert.Equal(["response"], order.Items); // already closed, no wait
        Assert.Equal(Environment.CurrentManagedThreadId, order.ThreadIds[0]);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ContentLengthOfAnEncodedBodyDoesNotEndIt()
    {
        var order = new CloseOrder();
        var source = new ThreadRecordingStream(new byte[100], order);
        var content = new OrderedContent(source, order, 40, "gzip"); // 40 wire bytes, more decoded bytes follow
        var response = new GuardedResponse(new HttpResponseMessage { Content = content });
        Stream body = await response.OpenBodyAsync(LongWait, TestContext.Current.CancellationToken);
        Assert.Equal(40, await body.ReadAsync(new byte[40], TestContext.Current.CancellationToken));
        source.HoldDispose = true;

        response.Dispose();

        Assert.False(source.Disposed); // still pending on the pool: the encoded Content-Length did not mark the end
        source.ReleaseDispose();
        await source.WaitForDisposeAsync();
    }

    [Fact]
    public async Task ContentLengthOfAnUnencodedBodyEndsItWithoutAFinalZeroRead()
    {
        var order = new CloseOrder();
        var source = new ThreadRecordingStream(new byte[100], order);
        var response = new GuardedResponse(new HttpResponseMessage { Content = new OrderedContent(source, order, 40) });
        Stream body = await response.OpenBodyAsync(LongWait, TestContext.Current.CancellationToken);
        Assert.Equal(40, await body.ReadAsync(new byte[40], TestContext.Current.CancellationToken));
        int caller = Environment.CurrentManagedThreadId;

        response.Dispose();

        Assert.Equal(caller, source.DisposeThreadId);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task BodyReadToItsEndIsClosedOnTheCallingThread()
    {
        var source = new ThreadRecordingStream(new byte[100]);
        Stream guarded = StallGuardedRead.Wrap(source, LongWait);
        byte[] buffer = new byte[64];
        while (await guarded.ReadAsync(buffer, TestContext.Current.CancellationToken) > 0)
        {
        }

        int caller = Environment.CurrentManagedThreadId;
        await guarded.DisposeAsync();

        Assert.True(source.Disposed);
        Assert.Equal(caller, source.DisposeThreadId);
    }

    private sealed class ThreadRecordingStream(byte[] data, CloseOrder? order = null) : MemoryStream(data)
    {
        private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _release = new();
        public bool Disposed { get; private set; }
        public bool HoldDispose { get; set; }
        public int DisposeThreadId { get; private set; }
        public void ReleaseDispose() => _release.Set();
        public Task WaitForDisposeAsync() => _disposed.Task.WaitAsync(LongWait);

        protected override void Dispose(bool disposing)
        {
            if (Disposed)
            {
                return; // HttpContent closes the read stream it handed out as well: only the first close counts
            }

            if (HoldDispose)
            {
                _release.Wait(TimeSpan.FromSeconds(5));
            }

            Disposed = true;
            DisposeThreadId = Environment.CurrentManagedThreadId;
            order?.Add("body");
            base.Dispose(disposing);
            _disposed.TrySetResult();
        }
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
