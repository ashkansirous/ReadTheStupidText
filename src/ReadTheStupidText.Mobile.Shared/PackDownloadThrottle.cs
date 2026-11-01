#if DEBUG
using System.Diagnostics;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Debug-only device-test hook (Slice 183): when the marker file <c>throttle-pack-downloads</c> exists in the app's external files
/// folder (Android; adb can write there) or the environment variable <c>RTST_THROTTLE_PACK_DOWNLOADS</c> is set, the HTTP pack copy runs at
/// about 200 KB/s, so a test can reach the notification's Cancel on a pack that would otherwise finish in seconds. Compiled out of Release.
/// </summary>
internal static class PackDownloadThrottle
{
    internal const string MarkerFile = "throttle-pack-downloads";
    private const string Variable = "RTST_THROTTLE_PACK_DOWNLOADS";
    private const double BytesPerSecond = 200 * 1024;

    internal static bool IsOn()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable)))
        {
            return true;
        }

#if ANDROID
        try
        {
            string? folder = Microsoft.Maui.ApplicationModel.Platform.AppContext.GetExternalFilesDir(null)?.AbsolutePath;
            return folder is not null && File.Exists(Path.Combine(folder, MarkerFile));
        }
        catch (Exception)
        {
            return false;
        }
#else
        return false;
#endif
    }

    private static volatile bool _dropRequested;

    /// <summary>Slice 233: the <c>drop-pack-network</c> hook arms one dropped connection (a running dataSync service is exempt from the per-app network block, so adb cannot cut it).</summary>
    internal static void RequestDrop() => _dropRequested = true;

    /// <summary>A download copy starts with no leftover request from a hook sent while nothing ran.</summary>
    internal static void ClearDrop() => _dropRequested = false;

    /// <summary>Throws what a dropped connection throws, once, so the app's real failure path (classify, notice) runs.</summary>
    internal static void ThrowIfDropRequested()
    {
        if (_dropRequested)
        {
            _dropRequested = false;
            throw new HttpRequestException(HttpRequestError.ConnectionError, "Test hook drop-pack-network: connection dropped.");
        }
    }

    private static long _stallArmedAt; // Stopwatch timestamp of the request, 0 = none

    private static readonly TimeSpan StallRequestLife = TimeSpan.FromMinutes(2);

    /// <summary>Slice 241: the <c>stall-pack-network</c> hook arms one hung transfer (what a dead socket does: Android's body read never returns and ignores its token; iOS's progress callbacks stop), so the stall timeout, its PACKDL stall line and the retry run for real. A request nobody took within two minutes lapses, so it cannot hang a later unrelated download.</summary>
    internal static void RequestStall() => Interlocked.Exchange(ref _stallArmedAt, Stopwatch.GetTimestamp());

    /// <summary>True once per request (and clears it), unless the request has lapsed.</summary>
    internal static bool TakeStallRequest()
    {
        long armed = Interlocked.Exchange(ref _stallArmedAt, 0);
        return armed != 0 && Stopwatch.GetElapsedTime(armed) < StallRequestLife;
    }

    /// <summary>Drops an unused request when a download ends.</summary>
    internal static void ClearStall() => Interlocked.Exchange(ref _stallArmedAt, 0);

    /// <summary>Wraps the raw body stream; the first read after the hook hangs for good, every later stream (the retry) is clean.</summary>
    internal static Stream WrapForStallHook(Stream inner) => new StallOnDemandStream(inner);

    private sealed class StallOnDemandStream(Stream inner) : Stream
    {
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

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (PackDownloadThrottle.TakeStallRequest())
            {
                await Task.Delay(Timeout.Infinite, CancellationToken.None).ConfigureAwait(false);
            }

            return await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    internal static Task PauseAsync(int bytes, CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromSeconds(bytes / BytesPerSecond), cancellationToken);
}
#endif
