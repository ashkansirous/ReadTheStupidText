using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile.Shared;

// The head start (Slice 47, ported to iOS in Slice 55): the opening sentences of the text are
// synthesized into the sentence cache before Play, in the voice that would read them now.
public sealed partial class IosSupertonicSpeechReader
{
    // A head start prepares the opening of the text only: this many chunks (the two-sentence head
    // plus a few after it), not the whole file, so a long document costs no battery.
    private const int PrepareMaxChunks = 4;

    // Only this much of the text is even chunked for a head start: planning the whole of a large
    // document just to keep its first few chunks would burn CPU for nothing.
    private const int PrepareMaxChars = 2000;

    // Synthesizes the sentences of the text's first chunks into the cache without playing. A newer
    // prepare, the start of a read, CancelPrepare and Dispose all cancel it; audio already cached stays.
    public Task PrepareAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return HeadStartRun.Skipped;
        }

        // The token is taken here, synchronously and in call order, so a later call always
        // supersedes an earlier one and CancelPrepare reaches a prepare still queued on the pool.
        return BeginPrepare() is { } token
            ? Task.Run(() => HeadStartRun.CompleteAsync(token, t => PrepareOffCallerAsync(text, t)))
            : HeadStartRun.Skipped;
    }

    // Runs on the thread pool, so acquiring the engine (which may build the model), planning the
    // chunks and indexing the cache never block the UI thread that asked.
    private async Task<bool> PrepareOffCallerAsync(string text, CancellationToken token)
    {
        if (token.IsCancellationRequested || AcquireEngine() is not { } engine)
        {
            return false;
        }

        try
        {
            token.ThrowIfCancellationRequested();
            await PrepareChunksAsync(engine, text, token);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Warning("head start failed", error: ex);
            return false;
        }
        finally
        {
            engine.Release();
        }
    }

    private async Task PrepareChunksAsync(IosBuiltEngine engine, string text, CancellationToken token)
    {
        VoiceInfo voice = ResolveVoice(engine, Volatile.Read(ref _voiceId));
        string prefix = TrimToSentenceBoundary(text);
        IEnumerable<string> sentences = SpeechChunkPlanner.ForText(prefix, HeadPolicy)
            .Take(PrepareMaxChunks)
            .SelectMany(chunk => SentenceUnits.Split(chunk.Text));

        await Task.WhenAll(sentences.Select(s => GetSentenceAsync(engine, s, voice, null, token)));
    }

    // The first PrepareMaxChars characters, cut back to the last sentence end so a sentence is never
    // synthesized half-way (it would be cached under a key no read ever asks for). With no boundary
    // in the prefix the prefix is kept whole.
    private static string TrimToSentenceBoundary(string text)
    {
        if (text.Length <= PrepareMaxChars)
        {
            return text;
        }

        string prefix = text[..PrepareMaxChars];
        for (int i = prefix.Length - 1; i > 0; i--)
        {
            bool wide = prefix[i] is '。' or '！' or '？';
            bool terminator = wide || prefix[i] is '.' or '!' or '?' or '؟';
            if (prefix[i] == (char)10 || (terminator && (wide || char.IsWhiteSpace(text[i + 1]))))
            {
                return prefix[..(i + 1)];
            }
        }

        return prefix;
    }

    public void CancelPrepare()
    {
        lock (_gate)
        {
            CancelPrepareLocked();
        }
    }

    // Called under _gate.
    private void CancelPrepareLocked()
    {
        _prepareCts?.Cancel();
        _prepareCts?.Dispose();
        _prepareCts = null;
    }

    // A newer prepare supersedes the one in flight; null when a read is in progress, which must
    // never share the engine with a prepare. Checked under the same lock that BeginRead takes.
    private CancellationToken? BeginPrepare()
    {
        lock (_gate)
        {
            if (_activeRead != null)
            {
                return null;
            }

            CancelPrepareLocked();
            _prepareCts = new CancellationTokenSource();
            return _prepareCts.Token;
        }
    }
}
