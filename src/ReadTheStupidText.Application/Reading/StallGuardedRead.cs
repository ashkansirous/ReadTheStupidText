namespace ReadTheStupidText.Application.Reading;

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
        try
        {
            linked.Cancel();
        }
        catch (Exception)
        {
            // a cancellation callback threw; the abandon below still has to happen
        }

        _ = send.ContinueWith(
            static (t, state) =>
            {
                var (source, message) = ((CancellationTokenSource, HttpRequestMessage))state!;
                if (t.IsCompletedSuccessfully)
                {
                    DisposeQuietly(t.Result);
                }
                else
                {
                    _ = t.Exception; // observe the fault so it never goes unhandled
                }

                DisposeQuietly(message);
                DisposeQuietly(source);
            },
            (linked, request),
            TaskScheduler.Default);
    }

    internal static void DisposeQuietly(IDisposable? target)
    {
        try
        {
            target?.Dispose();
        }
        catch (Exception)
        {
            // closing something stuck on a dead socket may throw; nothing to do about it
        }
    }
}

/// <summary>
/// A response whose disposal does nothing once a body read on it stalled and was abandoned: the stuck
/// content is then being closed off-thread, and a second dispose here could block on it.
/// </summary>
public sealed class GuardedResponse(HttpResponseMessage message) : IDisposable
{
    private int _abandoned;

    public HttpResponseMessage Message { get; } = message;

    /// <summary>The body as a stream whose reads give up after the stall timeout.</summary>
    public async Task<Stream> OpenBodyAsync(TimeSpan stallTimeout, CancellationToken cancellationToken, DownloadTag? tag = null)
    {
        Stream inner = await Message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return new GuardedStream(inner, this, stallTimeout, tag);
    }

    internal void Abandon()
    {
        if (Interlocked.Exchange(ref _abandoned, 1) == 0)
        {
            _ = Task.Run(() => StallGuardedRead.DisposeQuietly(Message));
        }
    }

    public void Dispose()
    {
        if (Volatile.Read(ref _abandoned) == 0)
        {
            Message.Dispose();
        }
    }
}

// Reads go into the stream's own buffer and are copied out, so an abandoned read that later completes
// writes into a buffer nobody reuses, never into the caller's.
internal sealed class GuardedStream(Stream inner, GuardedResponse? owner, TimeSpan stallTimeout, DownloadTag? tag = null) : Stream
{
    private byte[] _buffer = [];
    private int _abandoned;
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
            // a cancellation callback threw; the abandon below still has to happen
        }

        if (owner is not null)
        {
            owner.Abandon();
        }

        _ = Task.Run(() => StallGuardedRead.DisposeQuietly(inner));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Volatile.Read(ref _abandoned) == 0)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
