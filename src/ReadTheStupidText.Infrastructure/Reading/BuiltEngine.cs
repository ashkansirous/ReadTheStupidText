using ReadTheStupidText.Application.Reading;
using SherpaOnnx;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// An <see cref="OfflineTts"/> disposed when its last holder releases it. The reader holds one
/// reference while the engine is current; each read and each in-flight generation holds another, so
/// a model swapped out mid-read is disposed only once nothing is still synthesizing with it.
/// </summary>
internal sealed class RefCountedTts(OfflineTts tts)
{
    private int _references = 1;

    public OfflineTts Tts { get; } = tts;

    public bool IsAlive => Volatile.Read(ref _references) > 0;

    public bool TryAcquire()
    {
        int current;
        do
        {
            current = Volatile.Read(ref _references);
            if (current <= 0)
            {
                return false;
            }
        }
        while (Interlocked.CompareExchange(ref _references, current + 1, current) != current);

        return true;
    }

    public void Release()
    {
        if (Interlocked.Decrement(ref _references) == 0)
        {
            Tts.Dispose();
        }
    }
}

/// <summary>
/// An engine and the model it was built from, never changed after creation. A language change within
/// one pack shares the <see cref="RefCountedTts"/>. Each read captures one, so a language or pack
/// change mid-read can never mix models.
/// </summary>
internal sealed record BuiltEngine(RefCountedTts Tts, VoiceModelPaths Model) : IEngineHandle
{
    public bool TryAcquire() => Tts.TryAcquire();

    public void Release() => Tts.Release();
}

/// <summary>
/// How many call stacks still own a read: the original <c>SpeakAsync</c>, plus every Skip that took the
/// playback over. A Skip joins before it supersedes the generation, so the count never reaches zero while
/// playback continues; the last owner out ends the read.
/// </summary>
internal sealed class ReadOwnership
{
    private int _owners = 1;

    /// <summary>False once every owner has left (the read is over and cannot be joined).</summary>
    public bool TryJoin()
    {
        int current;
        do
        {
            current = Volatile.Read(ref _owners);
            if (current <= 0)
            {
                return false;
            }
        }
        while (Interlocked.CompareExchange(ref _owners, current + 1, current) != current);

        return true;
    }

    /// <summary>One owner is done; true for the last one.</summary>
    public bool Leave() => Interlocked.Decrement(ref _owners) == 0;
}

/// <summary>The read now playing: its engine (which a voice change may replace) and who owns it.</summary>
internal sealed class ActiveRead(BuiltEngine engine, ReadOwnership owners)
{
    private BuiltEngine _engine = engine;

    public ReadOwnership Owners { get; } = owners;

    /// <summary>The engine the read speaks with; replaced when a mid-read voice change needs another model.</summary>
    public BuiltEngine Engine
    {
        get => Volatile.Read(ref _engine);
        set => Volatile.Write(ref _engine, value);
    }
}
