using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using SherpaOnnx;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Hands the neural reader the audio of a chunk or a sentence (Slice 56, the Windows twin of the Android
/// reader's Slice 47 cache). Every sentence is fetched from the persistent <see cref="SentenceAudioCache"/>
/// or synthesized, and a chunk's sentences are joined into one WAV that the caller plays from its own
/// temp file, so a cached clip is never opened by the player or deleted by playback. One pool of
/// synthesis slots is shared by reads and head-start prepares, so their combined concurrency never
/// exceeds the cap.
/// </summary>
internal sealed class NeuralSentenceSource
{
    // Silence between the sentences of one chunk (each was synthesized on its own).
    private static readonly TimeSpan SentenceGap = TimeSpan.FromMilliseconds(150);

    private readonly SentenceAudioSource _sentences;
    private readonly ISystemLog _log;
    private readonly SemaphoreSlim _slots;

    public NeuralSentenceSource(string folder, ISystemLog log, int maxConcurrency)
    {
        _sentences = new SentenceAudioSource(new SentenceAudioCache(new FileSentenceStore(folder)), log);
        _log = log;
        _slots = new SemaphoreSlim(maxConcurrency);
    }

    /// <summary>
    /// The chunk's audio as one WAV, joined from its sentences. A cached clip that will not join
    /// (corrupt, or a different sample rate) is a miss, not a failed read: the chunk's cached clips are
    /// dropped and synthesized afresh. A second failure is real and propagates.
    /// </summary>
    public async Task<byte[]> JoinChunkAsync(
        BuiltEngine engine, string chunk, VoiceInfo voice, int? activityId, CancellationToken token)
    {
        IReadOnlyList<string> units = SentenceUnits.Split(chunk);
        byte[][] clips = await GetClipsAsync(engine, units, voice, activityId, token);
        try
        {
            return JoinClips(clips);
        }
        catch (InvalidDataException ex)
        {
            _log.Warning("cached sentence audio would not join; resynthesizing the chunk", activityId, ex);
            units.ToList().ForEach(u => _sentences.Invalidate(KeyFor(engine, u, voice)));
            return JoinClips(await GetClipsAsync(engine, units, voice, activityId, token));
        }
    }

    /// <summary>Indexes the persistent cache now (a folder scan), so no read has to.</summary>
    public void LoadIndex() => _sentences.LoadIndex();

    /// <summary>True when every sentence already has cached audio for this engine and voice.</summary>
    public bool AreCached(BuiltEngine engine, IEnumerable<string> sentences, VoiceInfo voice) =>
        sentences.All(s => _sentences.IsCached(KeyFor(engine, s, voice)));

    /// <summary>Synthesizes the given sentences into the cache without playing them (head start).</summary>
    public Task PrepareAsync(BuiltEngine engine, IEnumerable<string> sentences, VoiceInfo voice, CancellationToken token) =>
        Task.WhenAll(sentences.Select(s => GetSentenceAsync(engine, s, voice, null, token)));

    private async Task<byte[][]> GetClipsAsync(
        BuiltEngine engine, IReadOnlyList<string> units, VoiceInfo voice, int? activityId, CancellationToken token)
    {
        byte[][] clips = await Task.WhenAll(units.Select(u => GetSentenceAsync(engine, u, voice, activityId, token)));
        token.ThrowIfCancellationRequested();
        return clips;
    }

    private static byte[] JoinClips(byte[][] clips) =>
        clips.Length == 1 ? ValidClip(clips[0]) : WavFile.Concat(clips, SentenceGap);

    private static byte[] ValidClip(byte[] clip) =>
        WavFile.IsComplete(clip, clip.LongLength) ? clip : throw new InvalidDataException("Not a WAV written by this app.");

    // The key carries the voice, engine, language and model, so audio is never reused across them; the
    // engine and voice come from the read's own captured model.
    private Task<byte[]> GetSentenceAsync(
        BuiltEngine engine, string sentence, VoiceInfo voice, int? activityId, CancellationToken token)
    {
        SentenceCacheKey key = KeyFor(engine, sentence, voice);
        return _sentences.GetAsync(key, t => SynthesizeAsync(engine, sentence, voice.SpeakerId, t), token, activityId);
    }

    private static SentenceCacheKey KeyFor(BuiltEngine engine, string sentence, VoiceInfo voice) =>
        new(sentence, engine.Model.Kind, engine.Model.Language, voice.Id, engine.Model.RootDir, engine.Model.PackVersion);

    // The native Generate call cannot be interrupted, and its result may be wanted by another caller
    // that joined this synthesis, so a cancelled caller only stops waiting for a slot; a call already
    // running finishes and its audio goes to whoever is still waiting.
    private async Task<byte[]> SynthesizeAsync(BuiltEngine engine, string sentence, int speakerId, CancellationToken token)
    {
        await _slots.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested(); // superseded while queued: never start the native call
            OfflineTtsGeneratedAudio audio = await GenerateGuardedAsync(engine, sentence, speakerId);
            try
            {
                return WavFile.Encode(audio.Samples, audio.SampleRate);
            }
            finally
            {
                audio.Dispose();
            }
        }
        finally
        {
            _slots.Release();
        }
    }

    // The native call keeps running after its caller is cancelled, so it holds its own reference on the
    // engine, released when the call returns: a retired engine is disposed only once it returns.
    private static Task<OfflineTtsGeneratedAudio> GenerateGuardedAsync(BuiltEngine engine, string text, int speakerId)
    {
        if (!engine.TryAcquire())
        {
            throw new ObjectDisposedException(nameof(NeuralSentenceSource));
        }

        return Task.Run(() =>
        {
            try
            {
                return SherpaEngineFactory.Generate(engine, text, speakerId);
            }
            finally
            {
                engine.Release();
            }
        });
    }
}
