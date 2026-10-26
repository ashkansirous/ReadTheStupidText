using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>What a reader lends <see cref="SiblingRescue"/>: its engine cache, one native take, and the synthesis slots it holds.</summary>
public sealed record SiblingEngineOps<TEngine>(
    Func<VoiceModelPaths, TEngine?> Acquire, Action<TEngine> Release, Func<TEngine, int, Task<SpokenTake>> Take,
    SemaphoreSlim Slots, Action<string, Exception> Warn)
    where TEngine : class;

/// <summary>
/// The sibling-voice take of the silent-output guard (Slice 149), shared by the Windows, Android and iOS readers
/// so the three cannot drift. The caller holds one synthesis slot when it calls.
/// </summary>
public static class SiblingRescue
{
    /// <summary>
    /// One take of <paramref name="voice"/>'s sibling, or null when it has none or its model will not load. A sibling
    /// in another model folder needs its own engine: the engine cache builds it on the first rescue and keeps it. The
    /// slot is handed back while the model loads (the other sentences keep going) and taken again after.
    /// A cancelled <paramref name="token"/> stops before the load and after the slot is back.
    /// </summary>
    public static async Task<SpokenTake?> TakeAsync<TEngine>(
        TEngine engine, VoiceModelPaths model, VoiceInfo voice, string text, SiblingEngineOps<TEngine> ops, CancellationToken token)
        where TEngine : class
    {
        if (SiblingVoice.Pick(model, voice) is not { } sibling)
        {
            return null;
        }

        bool sameEngine = sibling.ModelDir == model.ModelDir;
        token.ThrowIfCancellationRequested();
        TEngine? target = sameEngine ? engine : await LoadAsync(model.ForVoice(sibling), ops, token);
        if (target is null)
        {
            return null;
        }

        try
        {
            return await ops.Take(target, sibling.SpeakerId);
        }
        finally
        {
            if (!sameEngine)
            {
                ops.Release(target);
            }
        }
    }

    private static async Task<TEngine?> LoadAsync<TEngine>(VoiceModelPaths paths, SiblingEngineOps<TEngine> ops, CancellationToken token)
        where TEngine : class
    {
        ops.Slots.Release();
        TEngine? loaded = null;
        try
        {
            loaded = await Task.Run(() => ops.Acquire(paths));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ops.Warn("the sibling voice could not be loaded; the silent take stands", ex);
        }
        finally
        {
            // Not cancellable: the caller's own finally releases the slot, so it must be held again here.
            await ops.Slots.WaitAsync(CancellationToken.None);
        }

        if (loaded is not null && token.IsCancellationRequested)
        {
            ops.Release(loaded);
            token.ThrowIfCancellationRequested();
        }

        return loaded;
    }
}
