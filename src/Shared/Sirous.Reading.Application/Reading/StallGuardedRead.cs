namespace Sirous.Reading.Application.Reading;

/// <summary>
/// Stall protection for downloads whose stream or handler ignores its <see cref="CancellationToken"/>
/// (Android's handler body stream can block in a Java read on a dead socket). On a stall the work is
/// abandoned off the calling thread and a <see cref="TimeoutException"/> is thrown. A stall is
/// classified by <see cref="PackErrorClassifier"/> as <see cref="PackDownloadResult.Failed"/> (not
/// Offline), so the attempt is retried. After a stall nothing on the caller's path touches the stuck
/// stream, response or request again: their disposal is handed to the abandonment path.
/// </summary>
public static class StallGuardedRead
{
    /// <summary>HttpClient's own default timeout; a header wait is never given up on sooner than this.</summary>
    public static readonly TimeSpan MinimumHeaderTimeout = TimeSpan.FromSeconds(100);

    /// <summary>The header wait to use for a body stall timeout: at least <see cref="MinimumHeaderTimeout"/>.</summary>
    public static TimeSpan HeaderTimeout(TimeSpan bodyStallTimeout) =>
        bodyStallTimeout > MinimumHeaderTimeout ? bodyStallTimeout : MinimumHeaderTimeout;

    /// <summary>Wraps a body stream so each read gives up after the stall timeout. Disposing the wrapper after a stall does nothing.</summary>
    public static Stream Wrap(Stream inner, TimeSpan stallTimeout, DownloadTag? tag = null) => new GuardedStream(inner, null, stallTimeout, tag);

    /// <summary>
    /// Sends the request built by <paramref name="createRequest"/> and waits for its headers, giving up (and
    /// cancelling) after <paramref name="headerTimeout"/>. Owns the request: it is disposed here, or, if the
    /// send is abandoned, only once the abandoned send has finished.
    /// </summary>
    public static async Task<GuardedResponse> SendAsync(HttpClient http, Func<HttpRequestMessage> createRequest, TimeSpan headerTimeout, CancellationToken cancellationToken, DownloadTag? tag = null)
    {
        HttpRequestMessage request = createRequest();
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<HttpResponseMessage> send = http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token);
        try
        {
            HttpResponseMessage response = await send.WaitAsync(headerTimeout, cancellationToken).ConfigureAwait(false);
            linked.Dispose();
            request.Dispose();
            return new GuardedResponse(response);
        }
        catch (Exception ex) when (IsStall(ex, cancellationToken))
        {
            LogStall(ex, PackDownloadLog.HeadersPhase, tag?.Offset ?? 0, tag);
            AbandonSend(linked, send, request);
            throw;
        }
        catch
        {
            linked.Dispose();
            request.Dispose();
            throw;
        }
    }

    // A user cancel counts as a stall for cleanup, but only a real timeout is logged as one.
    internal static void LogStall(Exception ex, string phase, long bytes, DownloadTag? tag)
    {
        if (ex is TimeoutException)
        {
            PackDownloadLog.Stall(phase, bytes, tag?.PackId);
        }
    }

    internal static bool IsStall(Exception ex, CancellationToken cancellationToken) =>
        ex is TimeoutException || (ex is OperationCanceledException && cancellationToken.IsCancellationRequested);

    // The abandoned send may still be using the request and the linked source, and may complete later
    // with a response nobody wants: dispose all three only once it has finished.
    private static void AbandonSend(CancellationTokenSource linked, Task<HttpResponseMessage> send, HttpRequestMessage request)
    {
        PackDownloadLog.CloseOffThread(PackDownloadLog.RequestPart);
        try
        {
            linked.Cancel();
        }
        catch (Exception)
        {
            // no-fallback: a cancellation callback threw; the abandon below still has to happen
        }

        _ = send.ContinueWith(
            static (t, state) =>
            {
                var (source, message) = ((CancellationTokenSource, HttpRequestMessage))state!;
                if (t.IsCompletedSuccessfully)
                {
                    DisposeQuietly(t.Result, PackDownloadLog.ResponsePart);
                }
                else
                {
                    _ = t.Exception; // observe the fault so it never goes unhandled
                }

                DisposeQuietly(message, PackDownloadLog.RequestPart);
                DisposeQuietly(source, PackDownloadLog.RequestPart);
            },
            (linked, request),
            TaskScheduler.Default);
    }

    internal static void DisposeQuietly(IDisposable? target, string what)
    {
        try
        {
            target?.Dispose();
        }
        catch (Exception ex)
        {
            // no-fallback: closing something stuck on a dead socket may throw; it is logged (type only) and nothing else can be done
            PackDownloadLog.CloseFailed(what, ex);
        }
    }
}

/// <summary>
/// Slice 400: the one place that knows whether a body was opened and read to its end, and so whether closing it may
/// drain a socket. Closing an unfinished body makes Android's okhttp read the rest on the closing thread, and a caller
/// on the UI thread then dies with NetworkOnMainThreadException: that close goes to the thread pool, as ONE task that
/// runs every step in order (never two tasks closing the same stream). A body never opened, or read to its end (an
/// empty destination buffer returning 0 is not the end), has nothing to drain and closes inline.
/// </summary>
internal sealed class BodyCloseState
{
    private int _opened;
    private int _ended;
    private int _noContent;
    private long _expectedLength = -1;
    private long _bytesRead;

    public void MarkOpened() => Volatile.Write(ref _opened, 1);

    public void MarkEnded() => Volatile.Write(ref _ended, 1);

    /// <summary>The body's Content-Length when the server sent one: reading that many bytes is the end, even if no final 0 read follows.</summary>
    public void SetExpectedLength(long? length) => Volatile.Write(ref _expectedLength, length ?? -1);

    /// <summary>Records one read: a 0 into a non-empty buffer, or reaching the known length, is the end of the body.</summary>
    public void ObserveRead(int count, int destinationLength)
    {
        long total = Interlocked.Add(ref _bytesRead, count);
        long expected = Volatile.Read(ref _expectedLength);
        if ((destinationLength > 0 && count == 0) || (expected >= 0 && total >= expected))
        {
            MarkEnded();
        }
    }

    /// <summary>A response with no content (null, or a known length of 0) has nothing to drain: the one case that closes inline without having been read.</summary>
    public void MarkNoContent() => Volatile.Write(ref _noContent, 1);

    /// <summary>Content never read to its end (never opened, or opened and not ended) may still be a live socket stream, so it closes off-thread.</summary>
    public bool NeedsOffThread => Volatile.Read(ref _noContent) == 0 && Volatile.Read(ref _ended) == 0;

    /// <summary>Runs <paramref name="closeInOrder"/> inline, or as one thread-pool task when closing could drain a socket; the log line is written only when a body was opened.</summary>
    public void Close(Action closeInOrder, string what)
    {
        if (!NeedsOffThread)
        {
            closeInOrder();
            return;
        }

        if (Volatile.Read(ref _opened) != 0)
        {
            PackDownloadLog.CloseOffThread(what);
        }

        _ = Task.Run(closeInOrder);
    }
}

/// <summary>
/// A response whose disposal does nothing once a body read on it stalled and was abandoned: the stuck
/// content is then being closed off-thread, and a second dispose here could block on it.
/// </summary>
public sealed class GuardedResponse(HttpResponseMessage message) : IDisposable
{
    private int _abandoned;
    private int _closed;
    private int _bodyOpened;
    private Stream? _body;

    public HttpResponseMessage Message { get; } = message;

    internal BodyCloseState CloseState { get; } = new();

    /// <summary>
    /// The body as a stream whose reads give up after the stall timeout. <paramref name="wrapInner"/> (a Debug test hook, Slice 241) wraps the raw body stream under the guard.
    /// A response has one body: a second open is refused (the first stream would otherwise be orphaned and never closed).
    /// </summary>
    public async Task<Stream> OpenBodyAsync(TimeSpan stallTimeout, CancellationToken cancellationToken, DownloadTag? tag = null, Func<Stream, Stream>? wrapInner = null)
    {
        if (Interlocked.Exchange(ref _bodyOpened, 1) != 0)
        {
            throw new InvalidOperationException("The response body was already opened.");
        }

        Stream inner = await Message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        if (wrapInner is not null)
        {
            inner = wrapInner(inner);
        }

        _body = inner;
        // Content-Length is the encoded wire size when a Content-Encoding is present, so it cannot mark the end of the decoded body: then only a 0 read does.
        CloseState.SetExpectedLength(Message.Content.Headers.ContentEncoding.Count == 0 ? Message.Content.Headers.ContentLength : null);
        CloseState.MarkOpened();
        return new GuardedStream(inner, this, stallTimeout, tag);
    }

    // A stalled read: the stuck body and then the response are closed by one pool task, and this response's own Dispose then does nothing.
    internal void Abandon(Stream? stuckBody)
    {
        if (Interlocked.Exchange(ref _abandoned, 1) == 0)
        {
            PackDownloadLog.CloseOffThread(PackDownloadLog.ResponsePart);
            _ = Task.Run(() => CloseInOrder(stuckBody));
        }
    }

    // Slice 400: the response owns the close of its body and itself, in that order. When the body was opened and not read to its end, closing drains the rest on the closing thread
    // (NetworkOnMainThreadException on Android's UI thread), so both closes run as one thread-pool task; otherwise they run inline.
    public void Dispose()
    {
        if (Volatile.Read(ref _abandoned) != 0 || Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        if (Volatile.Read(ref _bodyOpened) == 0 && (Message.Content is null || Message.Content.Headers.ContentLength == 0))
        {
            CloseState.MarkNoContent();
        }

        CloseState.Close(() => CloseInOrder(_body), PackDownloadLog.ResponsePart);
    }

    private void CloseInOrder(Stream? body)
    {
        StallGuardedRead.DisposeQuietly(body, PackDownloadLog.BodyPart);
        StallGuardedRead.DisposeQuietly(Message, PackDownloadLog.ResponsePart);
    }
}

// Reads go into the stream's own buffer and are copied out, so an abandoned read that later completes
// writes into a buffer nobody reuses, never into the caller's.
internal sealed class GuardedStream(Stream inner, GuardedResponse? owner, TimeSpan stallTimeout, DownloadTag? tag = null) : Stream
{
    private byte[] _buffer = [];
    private int _abandoned;
    private readonly BodyCloseState _state = owner?.CloseState ?? OpenedState();
    private long _bytesRead = tag?.Offset ?? 0; // total position in the file, including the resume offset

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

    public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _abandoned) != 0)
        {
            throw new TimeoutException("The download stalled and was abandoned.");
        }

        if (_buffer.Length < destination.Length)
        {
            _buffer = new byte[destination.Length];
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        byte[] target = _buffer;
        Task<int> read = inner.ReadAsync(target.AsMemory(0, destination.Length), linked.Token).AsTask();
        try
        {
            int count = await read.WaitAsync(stallTimeout, cancellationToken).ConfigureAwait(false);
            _state.ObserveRead(count, destination.Length); // a 0 from an empty destination buffer is not the end of the body

            target.AsMemory(0, count).CopyTo(destination);
            _bytesRead += count;
            return count;
        }
        catch (Exception ex) when (StallGuardedRead.IsStall(ex, cancellationToken))
        {
            StallGuardedRead.LogStall(ex, PackDownloadLog.BodyPhase, _bytesRead, tag);
            Abandon(linked, read);
            throw;
        }
    }

    private static BodyCloseState OpenedState()
    {
        var state = new BodyCloseState();
        state.MarkOpened();
        return state;
    }

    private void Abandon(CancellationTokenSource linked, Task<int> stuck)
    {
        Interlocked.Exchange(ref _abandoned, 1);
        _buffer = []; // the stuck read keeps the old array; the next read (none are allowed) would get a fresh one
        _ = stuck.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        try
        {
            linked.Cancel();
        }
        catch (Exception)
        {
            // no-fallback: a cancellation callback threw; the abandon below still has to happen
        }

        if (owner is not null)
        {
            owner.Abandon(inner); // the response closes the stuck body, then itself, in one task
            return;
        }

        PackDownloadLog.CloseOffThread(PackDownloadLog.BodyPart);
        _ = Task.Run(() => StallGuardedRead.DisposeQuietly(inner, PackDownloadLog.BodyPart));
    }

    // Slice 400: a stream with an owning response leaves the close to that response (one close of body then response, see GuardedResponse.Dispose); a bare wrapped stream closes itself,
    // off-thread unless it was read to its end.
    protected override void Dispose(bool disposing)
    {
        if (disposing && owner is null && Volatile.Read(ref _abandoned) == 0)
        {
            _state.Close(() => StallGuardedRead.DisposeQuietly(inner, PackDownloadLog.BodyPart), PackDownloadLog.BodyPart);
        }

        base.Dispose(disposing);
    }
}
