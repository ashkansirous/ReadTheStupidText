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
