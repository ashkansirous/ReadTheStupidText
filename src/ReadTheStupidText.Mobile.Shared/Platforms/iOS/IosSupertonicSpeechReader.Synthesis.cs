using System.Collections.Concurrent;
using System.Diagnostics;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using SherpaOnnx;

namespace ReadTheStupidText.Mobile;

// The synthesis half of the iOS neural reader: acquiring the engine for the language's pack,
// turning a chunk into audio through the sentence cache (Slice 47), and writing it to a temp file.
public sealed partial class IosSupertonicSpeechReader
{
    private async Task<string> GenerateAfterAsync(
        Task gate, IosBuiltEngine engine, string chunk, int index, VoiceInfo voice,
        ConcurrentQueue<string> files, CancellationToken token)
    {
        try
        {
            await gate;
        }
        catch
        {
            // Swallowed: the first chunk's real outcome surfaces on its own await.
        }

        return await GenerateChunkAsync(engine, chunk, index, voice, files, token);
    }

    // A chunk is joined from its sentences' audio, each fetched from the cache or synthesized.
    // The sentences of one chunk are requested together; `_slots` bounds how many actually
    // synthesize at once. Every file written is recorded in `files` so the generation's drain
    // can delete it.
    private async Task<string> GenerateChunkAsync(
        IosBuiltEngine engine, string chunk, int index, VoiceInfo voice,
        ConcurrentQueue<string> files, CancellationToken token)
    {
        long started = Stopwatch.GetTimestamp();
        IReadOnlyList<string> units = SentenceUnits.Split(chunk);
        byte[] wav = await JoinSentencesAsync(engine, units, voice, token);
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
        IosBuiltEngine engine, IReadOnlyList<string> units, VoiceInfo voice, CancellationToken token)
    {
        byte[][] clips = await GetClipsAsync(engine, units, voice, token);
        try
        {
            return JoinClips(clips);
        }
        catch (InvalidDataException ex)
        {
            _log.Warning("cached sentence audio would not join; resynthesizing the chunk", error: ex);
            units.ToList().ForEach(u => _sentences.Invalidate(KeyFor(engine, u, voice)));
            return JoinClips(await GetClipsAsync(engine, units, voice, token));
        }
    }

    private async Task<byte[][]> GetClipsAsync(
        IosBuiltEngine engine, IReadOnlyList<string> units, VoiceInfo voice, CancellationToken token)
    {
        byte[][] clips = await Task.WhenAll(units.Select(u => GetSentenceAsync(engine, u, voice, token)));
        token.ThrowIfCancellationRequested();
        return clips;
    }

    private static byte[] JoinClips(byte[][] clips) =>
        clips.Length == 1 ? ValidClip(clips[0]) : WavFile.Concat(clips, SentenceGap);

    private static byte[] ValidClip(byte[] clip) =>
        WavFile.IsComplete(clip, clip.LongLength) ? clip : throw new InvalidDataException("Not a WAV written by this app.");

    // The key carries the voice, engine, language and model, so audio is never reused across
    // them; the engine and voice come from the read's own captured model.
    private Task<byte[]> GetSentenceAsync(IosBuiltEngine engine, string sentence, VoiceInfo voice, CancellationToken token)
    {
        SentenceCacheKey key = KeyFor(engine, sentence, voice);
        return _sentences.GetAsync(key, t => SynthesizeSentenceAsync(engine, sentence, voice.SpeakerId, t), token);
    }

    private static SentenceCacheKey KeyFor(IosBuiltEngine engine, string sentence, VoiceInfo voice) =>
        new(sentence, engine.Model.Kind, engine.Model.Language, voice.Id, engine.Model.RootDir, engine.Model.PackVersion);

    // The native Generate call cannot be interrupted, and its result may be wanted by another
    // caller that joined this synthesis, so a cancelled caller only stops waiting for a slot; a
    // call already running finishes and its audio goes to whoever is still waiting.
    private async Task<byte[]> SynthesizeSentenceAsync(
        IosBuiltEngine engine, string sentence, int speakerId, CancellationToken token)
    {
        await _slots.WaitAsync(token);
        try
        {
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

    // The native Generate call keeps running after its caller is cancelled, so it holds its own
    // reference on the engine, released by the task itself when the call returns (success, fault
    // or abandonment alike, so the count stays balanced): a retired engine is disposed only once
    // it returns.
    private static Task<OfflineTtsGeneratedAudio> GenerateGuardedAsync(IosBuiltEngine engine, string chunk, int speakerId)
    {
        if (!engine.TryAcquire())
        {
            throw new ObjectDisposedException(nameof(IosSupertonicSpeechReader));
        }

        return Task.Run(() =>
        {
            try
            {
                return IosTtsEngine.Generate(engine, chunk, speakerId);
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
    // who must Release() it. A different pack or engine kind rebuilds the OfflineTts; the same
    // pack under another language reuses it with the new model. The superseded OfflineTts is
    // disposed once the last read or generation holding it lets go.
    private IosBuiltEngine? AcquireEngine()
    {
        if (_model.Paths is not { } paths)
        {
            return null;
        }

        lock (_ttsGate)
        {
            if (_disposed)
            {
                return null;
            }

            if (_engine is null || _engine.Model != paths)
            {
                _engine = IosTtsEngine.Swap(_engine, paths);
            }

            return TryAcquireCurrent(paths);
        }
    }

    // Takes the caller's reference on the current engine. A refused reference means the engine was
    // already disposed, so it is rebuilt once from scratch; if that is refused too the read is not
    // ready. A disposed engine is never handed out and a reference that was not acquired is never
    // released. Called under _ttsGate.
    private IosBuiltEngine? TryAcquireCurrent(VoiceModelPaths paths)
    {
        if (_engine!.TryAcquire())
        {
            return _engine;
        }

        _engine = IosTtsEngine.Swap(null, paths);
        return _engine.TryAcquire() ? _engine : null;
    }

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
