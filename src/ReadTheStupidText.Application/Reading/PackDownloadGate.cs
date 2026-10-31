using System.Collections.Concurrent;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// One download of a voice pack at a time (Slice 173): two callers asking for the same pack (the voice installer and the story-language downloads) would otherwise
/// write the same zip together. The second waits for the first, then finds the pack installed and has nothing left to fetch.
/// </summary>
public static class PackDownloadGate
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    /// <summary>Waits for any download of <paramref name="packId"/> in flight; dispose the result when this download ends.</summary>
    public static async Task<IDisposable> EnterAsync(string packId, CancellationToken cancellationToken)
    {
        SemaphoreSlim gate = Gates.GetOrAdd(packId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(gate);
    }

    /// <summary>
    /// Takes the gate of <paramref name="packId"/> only if nobody holds it: null when a download is in flight (or another caller holds it, and so queued
    /// callers sit behind it). Dispose the result to release. Used by the launch scan (Slice 208) so a delete and a start cannot interleave.
    /// </summary>
    public static IDisposable? TryEnter(string packId)
    {
        SemaphoreSlim gate = Gates.GetOrAdd(packId, _ => new SemaphoreSlim(1, 1));
        return gate.Wait(0) ? new Releaser(gate) : null;
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                gate.Release();
            }
        }
    }
}
