using System.Collections.Concurrent;
using System.Diagnostics;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using SherpaOnnx;

namespace ReadTheStupidText.Mobile.Shared;

// The synthesis half of the iOS neural reader: acquiring the engine for the language's pack,
// turning a chunk into audio through the sentence cache (Slice 47), and writing it to a temp file.
public sealed partial class IosSupertonicSpeechReader
{
    // A chunk is joined from its sentences' audio, each fetched from the cache or synthesized.
    // The sentences of one chunk are requested together; `_slots` bounds how many actually
    // synthesize at once. Every file written is recorded in `files` so the generation's drain
    // can delete it.
    private async Task<string> GenerateChunkAsync(
        IosBuiltEngine engine, string chunk, int index, VoiceInfo voice, IVoicedAudioTransform? transform,
        ConcurrentQueue<string> files, CancellationToken token)
    {
        long started = Stopwatch.GetTimestamp();
        IReadOnlyList<string> units = SentenceUnits.Split(chunk);
        byte[] wav = await JoinSentencesAsync(engine, units, voice, transform, token);
        token.ThrowIfCancellationRequested();
        RecordChunkDuration(index, WavFile.TryGetDuration(wav) ?? TimeSpan.Zero);
        _log.Debug($"chunk {index} ({chunk.Length} chars, {units.Count} sentence(s)) ready in " +
            $"{Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms");
        string path = await WriteChunkFileAsync(wav);
        files.Enqueue(path);
        return path;
    }

    // Fetches every sentence's clip and joins them. A cached clip that will not join (corrupt, or
    // a different sample rate) is a miss, not a failed read: the chunk's cached clips are dropped
    // and synthesized afresh. A second failure is real and propagates.
    private async Task<byte[]> JoinSentencesAsync(
        IosBuiltEngine engine, IReadOnlyList<string> units, VoiceInfo voice, IVoicedAudioTransform? transform,
        CancellationToken token)
    {
        byte[][] clips = await GetClipsAsync(engine, units, voice, transform, token);
        try
        {
            return JoinClips(clips, transform);
        }
        catch (InvalidDataException ex)
        {
            _log.Warning("cached sentence audio would not join; resynthesizing the chunk", error: ex);
            units.ToList().ForEach(u => _sentences.Invalidate(KeyFor(engine, u, voice, transform)));
            return JoinClips(await GetClipsAsync(engine, units, voice, transform, token), transform);
        }
    }

    private async Task<byte[][]> GetClipsAsync(
        IosBuiltEngine engine, IReadOnlyList<string> units, VoiceInfo voice, IVoicedAudioTransform? transform,
        CancellationToken token)
    {
        byte[][] clips = await Task.WhenAll(units.Select(u => GetSentenceAsync(engine, u, voice, transform, token)));
        token.ThrowIfCancellationRequested();
        return clips;
    }

    private static byte[] JoinClips(byte[][] clips, IVoicedAudioTransform? transform) =>
        clips.Length == 1 ? ValidClip(clips[0]) : WavFile.Concat(clips, VoicedAudio.GapOf(transform));

    private static byte[] ValidClip(byte[] clip) =>
        WavFile.IsComplete(clip, clip.LongLength) ? clip : throw new InvalidDataException("Not a WAV written by this app.");

    // The key carries the voice, engine, language and model, so audio is never reused across
    // them; the engine and voice come from the read's own captured model.
    private Task<byte[]> GetSentenceAsync(
        IosBuiltEngine engine, string sentence, VoiceInfo voice, IVoicedAudioTransform? transform, CancellationToken token)
    {
        SentenceCacheKey key = KeyFor(engine, sentence, voice, transform);
        return _sentences.GetClipAsync(
            key, t => SynthesizeSentenceAsync(engine, sentence, voice, transform, t), token);
    }

    private static SentenceCacheKey KeyFor(IosBuiltEngine engine, string sentence, VoiceInfo voice, IVoicedAudioTransform? transform) =>
        new(sentence, engine.Model.Kind, engine.Model.Language, voice.Id, engine.Model.RootDir, engine.Model.PackVersion, SiblingVoice.CacheTag(voice, transform));

    // The native Generate call cannot be interrupted, and its result may be wanted by another
    // caller that joined this synthesis, so a cancelled caller only stops waiting for a slot; a
    // call already running finishes and its audio goes to whoever is still waiting.
    private async Task<SentenceClip> SynthesizeSentenceAsync(
        IosBuiltEngine engine, string sentence, VoiceInfo voice, IVoicedAudioTransform? transform, CancellationToken token)
    {
        RescueResult rescue;
        await _slots.WaitAsync(token);
        try
        {
            float pace = VoicedAudio.PaceOf(transform);
            token.ThrowIfCancellationRequested(); // superseded while queued: never start the native call
            rescue = await SilentOutputGuard.RescueUnitAsync(
                sentence, voice.Id, pace, SiblingVoice.IsDropProne(voice), phrase => TakeAsync(engine, phrase, voice.SpeakerId, pace),
                message => _log.Warning(message), token, phrase => SiblingTakeAsync(engine, phrase, voice, pace, token));
        }
        finally
        {
            _slots.Release();
        }

        // The product's voice transform is plain DSP: it runs after the slot is released so the next sentence can start synthesising.
        // A take the guard gave up on is played this once but never cached, so the next read tries again.
        SpokenTake take = rescue.Take;
        return new SentenceClip(WavFile.Encode(VoicedAudio.Apply(transform, take.Samples, take.SampleRate), take.SampleRate), !rescue.GaveUp);
    }

    // The pack's other voice speaks the unit when this one stays silent (Slice 149; shared logic in SiblingRescue).
    private Task<SpokenTake?> SiblingTakeAsync(IosBuiltEngine engine, string text, VoiceInfo voice, float pace, CancellationToken token) =>
        SiblingRescue.TakeAsync(
            engine, engine.Model, voice, text,
            new SiblingEngineOps<IosBuiltEngine>(
                paths => _engines.Acquire(paths), e => e.Release(), (e, speakerId) => TakeAsync(e, text, speakerId, pace), _slots,
                (m, e) => _log.Warning(m, error: e)),
            token);

    // One take of the voice, copied out so the native audio is released at once (Slice 149).
    private static async Task<SpokenTake> TakeAsync(IosBuiltEngine engine, string text, int speakerId, float pace)
    {
        OfflineTtsGeneratedAudio audio = await GenerateGuardedAsync(engine, text, speakerId, pace);
        try
        {
            return new SpokenTake(audio.Samples, audio.SampleRate);
        }
        finally
        {
            audio.Dispose();
        }
    }

    // The native Generate call keeps running after its caller is cancelled, so it holds its own
    // reference on the engine, released by the task itself when the call returns (success, fault
    // or abandonment alike, so the count stays balanced): a retired engine is disposed only once
    // it returns.
    private static Task<OfflineTtsGeneratedAudio> GenerateGuardedAsync(IosBuiltEngine engine, string chunk, int speakerId, float pace)
    {
        if (!engine.TryAcquire())
        {
            throw new ObjectDisposedException(nameof(IosSupertonicSpeechReader));
        }

        return Task.Run(() =>
        {
            try
            {
                return IosTtsEngine.GenerateTracked(engine, chunk, speakerId, pace);
            }
            finally
            {
                engine.Release();
            }
        });
    }

    private void RecordChunkDuration(int index, TimeSpan duration)
    {
        bool totalWasUnknown = _timing.CurrentTiming(TimeSpan.Zero).Total is null;
        _timing.RecordChunkDuration(index, duration);
        if (totalWasUnknown && _timing.CurrentTiming(TimeSpan.Zero).Total is not null)
        {
            RaiseTiming(CurrentChunkPosition());
        }
    }

    // Runs once a generation is over (finished, stopped, skipped or superseded). Waits for its
    // synthesis tasks, swallowing every outcome so none goes unobserved, then deletes every chunk
    // file the generation wrote. Deleting a file that is still playing is safe (the open handle
    // stays valid) and the player is released by whoever ended the read.
    private static async Task DrainAsync(List<Task<string>> generations, ConcurrentQueue<string> files)
    {
        foreach (Task<string> generation in generations)
        {
            try
            {
                await generation;
            }
            catch
            {
                // Cancelled or failed: a real failure already surfaced on the consumer's await.
            }
        }

        foreach (string path in files)
        {
            TryDelete(path);
        }
    }

    // Returns the engine for the language's current model with a reference taken for the caller,
    // who must Release() it. A different pack folder, version or engine kind is another engine (a
    // pack update is a new folder, so it is picked up here); the same folder under another
    // language reuses the model (a Kokoro pack excepted: each language has an engine of its own). An engine nobody asks for any more is disposed once the last
    // read or generation holding it lets go.
    // A warm-up (Slice 177) is refused (null) rather than evict an engine that is in use.
    private IosBuiltEngine? AcquireEngine(string? voiceId = null, VoiceModelPaths? model = null, bool forWarmUp = false)
    {
        if ((model ?? _model.Paths) is not { } current)
        {
            return null;
        }

        // Aimed at the voice that will speak: voices of one pack can each have their own model folder.
        VoiceModelPaths paths = current.ForVoice(PickVoice(current, voiceId));
        if (!forWarmUp && voiceId is not null)
        {
            Volatile.Write(ref _lastReadVoiceId, voiceId);
        }

        if (ModelKey(paths) == Volatile.Read(ref _failedModelKey))
        {
            // This exact model already failed to load: don't retry the multi-hundred-MB build on every
            // chunk or read. A different pack folder, engine or version clears the block.
            throw new NeuralEngineUnavailableException();
        }

        try
        {
            return AcquireEngineCore(paths, forWarmUp);
        }
        catch (Exception ex) when (ex is not NeuralEngineUnavailableException)
        {
            Volatile.Write(ref _failedModelKey, ModelKey(paths));
            _log.Warning("the neural engine could not be built", error: ex);
            RefreshModelInBackground();
            throw new NeuralEngineUnavailableException(ex);
        }
    }

    private static string ModelKey(VoiceModelPaths paths) => $"{paths.RootDir}|{paths.Kind}|{paths.PackVersion}|{paths.ModelDir}|{KokoroLanguageOf(paths)}";

    // A Kokoro engine is configured for one language (Slice 118), so a failure to build one is that language's only; any other engine kind has no language in its key.
    private static string KokoroLanguageOf(VoiceModelPaths paths) => paths.Kind == VoiceEngineKind.Kokoro ? paths.Language : string.Empty;

    // A pack that is "ready" but cannot be loaded: the model re-checks the installed packs.
    private void RefreshModelInBackground() =>
        _ = _model.InitializeAsync().ContinueWith(
            t => _log.Warning("voice model refresh failed", error: t.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    private IosBuiltEngine? AcquireEngineCore(VoiceModelPaths paths, bool forWarmUp) => _engines.Acquire(paths, forWarmUp);

    private static async Task<string> WriteChunkFileAsync(byte[] wav)
    {
        string path = Path.Combine(FileSystem.Current.CacheDirectory, $"{ChunkFilePrefix}{Guid.NewGuid():N}{ChunkFileExtension}");
        await File.WriteAllBytesAsync(path, wav);
        return path;
    }

    private void DeletePreviousChunkFile()
    {
        if (_previousChunkFile is { } path)
        {
            TryDelete(path);
            _previousChunkFile = null;
        }
    }

    private void DeleteCurrentChunkFile()
    {
        if (_currentChunkFile is { } path)
        {
            TryDelete(path);
            _currentChunkFile = null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort cleanup; the cache directory is reclaimed by the OS regardless.
        }
    }
}
