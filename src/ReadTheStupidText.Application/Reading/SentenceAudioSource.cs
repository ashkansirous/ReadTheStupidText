using System.Collections.Concurrent;
using ReadTheStupidText.Application.Logging;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Hands out the audio for one sentence: from the <see cref="SentenceAudioCache"/> when it
/// has it, otherwise from the engine (Slice 47, Decision 66). Two callers asking for the same
/// sentence at once (a head-start prepare and the read that follows it) share one synthesis.
/// A caller that was cancelled never stores or plays what it was waiting for, so a superseded
/// read leaves nothing behind.
/// </summary>
internal sealed class SentenceAudioSource
{
    private readonly SentenceAudioCache _cache;
    private readonly ISystemLog _log;
    private readonly ConcurrentDictionary<string, Lazy<Task<byte[]>>> _inFlight = new();

    public SentenceAudioSource(SentenceAudioCache cache, ISystemLog log)
    {
        _cache = cache;
        _log = log;
    }

    /// <summary>
    /// The WAV for <paramref name="key"/>. <paramref name="synthesize"/> runs only on a miss
    /// and receives the token of the caller that started it; it returns a WAV in the layout
    /// of <see cref="WavFile"/>. Throws <see cref="OperationCanceledException"/> when
    /// <paramref name="token"/> is cancelled.
    /// </summary>
    public async Task<byte[]> GetAsync(
        SentenceCacheKey key, Func<CancellationToken, Task<byte[]>> synthesize, CancellationToken token,
        int? activityId = null)
    {
        token.ThrowIfCancellationRequested();
        if (await _cache.TryGetAsync(key) is { } cached)
        {
            _log.Debug($"cache hit {key.ShortHash} ({key.Text.Length} chars)", activityId);
            return cached;
        }

        _log.Debug($"cache miss {key.ShortHash} ({key.Text.Length} chars)", activityId);
        byte[] wav = await SynthesizeSharedAsync(key, synthesize, token, activityId);
        token.ThrowIfCancellationRequested(); // a cancelled caller drops the clip for playback; it is already cached
        return wav;
    }

    /// <summary>Indexes the cache's folder now instead of on first use.</summary>
    public void LoadIndex() => _cache.LoadIndex();

    /// <summary>Whether the clip is in the cache (by index only; the file is not read).</summary>
    public bool IsCached(SentenceCacheKey key) => _cache.Contains(key.Id);

    /// <summary>Forgets a cached clip that proved unusable (it would not join with its
    /// neighbours), so the next request for it synthesizes afresh.</summary>
    public void Invalidate(SentenceCacheKey key)
    {
        _cache.Invalidate(key);
        // A finished synthesis stays registered until its clean-up continuation runs; drop it
        // now so the next request cannot join it and get the clip that was just invalidated.
        if (_inFlight.TryGetValue(key.Id, out Lazy<Task<byte[]>>? shared)
            && shared.IsValueCreated && shared.Value.IsCompleted)
        {
            _inFlight.TryRemove(new KeyValuePair<string, Lazy<Task<byte[]>>>(key.Id, shared));
        }
    }

    // The cache is an optimization: a full or cleared disk costs a later re-synthesis, never
    // the read in progress.
    private async Task TryStoreAsync(SentenceCacheKey key, byte[] wav, int? activityId)
    {
        try
        {
            await _cache.PutAsync(key, wav);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warning($"cache store failed for {key.ShortHash}", activityId, ex);
        }
    }

    // Joins the synthesis already running for this sentence, or starts one. If the caller that
    // started it was cancelled while this caller was not, the shared task is cancelled too and
    // this caller starts a fresh one, so one caller's cancel never fails another's read. The
    // retry is bounded by the caller's own token.
    private async Task<byte[]> SynthesizeSharedAsync(
        SentenceCacheKey key, Func<CancellationToken, Task<byte[]>> synthesize, CancellationToken token,
        int? activityId)
    {
        while (true)
        {
            var own = new Lazy<Task<byte[]>>(() => StartShared(key, synthesize, token, activityId));
            Lazy<Task<byte[]>> shared = _inFlight.GetOrAdd(key.Id, own);
            try
            {
                return await shared.Value.WaitAsync(token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                _inFlight.TryRemove(new KeyValuePair<string, Lazy<Task<byte[]>>>(key.Id, shared));
                if (ReferenceEquals(shared, own))
                {
                    // Our own synthesis was cancelled although our token was not: retrying
                    // would loop, and surfacing a cancel the caller never asked for is wrong.
                    throw new IOException("Synthesis was cancelled unexpectedly.");
                }
            }
        }
    }

    // The shared task stores the clip itself when the synthesis finishes, whichever caller is
    // still waiting (or none): the clip is content-addressed, so it can never be stale, and a
    // native synthesis that ran to the end is not wasted because its starter was cancelled.
    private Task<byte[]> StartShared(
        SentenceCacheKey key, Func<CancellationToken, Task<byte[]>> synthesize, CancellationToken token,
        int? activityId)
    {
        Task<byte[]> task = SynthesizeAndStoreAsync(key, synthesize, token, activityId);
        _ = task.ContinueWith(_ => ForgetAndObserve(key.Id, task), TaskScheduler.Default);
        return task;
    }

    private async Task<byte[]> SynthesizeAndStoreAsync(
        SentenceCacheKey key, Func<CancellationToken, Task<byte[]>> synthesize, CancellationToken token,
        int? activityId)
    {
        // Re-checked here: a clip stored between the caller's cache miss and this task starting
        // (the previous in-flight synthesis finishing) is a hit, not a second synthesis.
        if (await _cache.TryGetAsync(key) is { } cached)
        {
            return cached;
        }

        byte[] wav = await synthesize(token);
        await TryStoreAsync(key, wav, activityId);
        return wav;
    }

    // The finished synthesis leaves the in-flight map, and a fault nobody was left waiting on
    // is observed here so it never surfaces as an unobserved task exception.
    private void ForgetAndObserve(string id, Task<byte[]> finished)
    {
        _ = finished.Exception;
        if (_inFlight.TryGetValue(id, out Lazy<Task<byte[]>>? current) && current.Value == finished)
        {
            _inFlight.TryRemove(new KeyValuePair<string, Lazy<Task<byte[]>>>(id, current));
        }
    }
}
