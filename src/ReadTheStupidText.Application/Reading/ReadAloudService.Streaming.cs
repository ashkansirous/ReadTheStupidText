using System.Text;
using System.Diagnostics;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Domain.Activity;

namespace ReadTheStupidText.Application.Reading;

// Slice 75: a translated read on a reader that can stream starts speaking the first translated sentences
// while the rest is still being translated, instead of waiting for the whole translation.
public sealed partial class ReadAloudService
{
    /// <summary>Raised as translation packs download or finish, for the panel's note (Slice 75).</summary>
    public event EventHandler<TranslationPackProgress>? TranslationPackProgress
    {
        add
        {
            if (_translation is not null)
            {
                _translation.PackProgress += value;
            }
        }
        remove
        {
            if (_translation is not null)
            {
                _translation.PackProgress -= value;
            }
        }
    }

    // True when the read was handled here (streamed, or the original read because translation could not
    // run); false when the caller should translate the whole text first, as before.
    private async Task<bool> TryStreamReadAsync(ActivityEntry entry, string text)
    {
        if (_translation is null || !TranslateEnabled || !_reader.SupportsStreaming)
        {
            return false;
        }

        var cts = new CancellationTokenSource();
        CancelPendingPrepare(Interlocked.Exchange(ref _translationCts, cts));
        long started = Stopwatch.GetTimestamp();
        try
        {
            TranslationStreamStart? start = await PlanStreamAsync(entry, text, cts.Token);
            if (start is null)
            {
                return false;
            }

            if (start.Terminal is { } terminal)
            {
                await ReadOriginalAsync(entry, terminal, cts, text.Length);
                return true;
            }

            await ReadStreamAsync(entry, start.Stream!, cts.Token, text.Length, started);
            return true;
        }
        catch (StreamingUnavailableException ex)
        {
            _systemLog.Warning("the reader could not stream; reading the whole translated text", entry.Id, ex);
            return false; // the reader now reports SupportsStreaming false; the caller reads the whole text
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            CountTiming(UsageStage.Translate, UsageOutcome.Cancelled, Stopwatch.GetElapsedTime(started), null, null);
            return true; // superseded: the entry was finalized by Supersede
        }
        finally
        {
            if (Interlocked.CompareExchange(ref _translationCts, null, cts) == cts)
            {
                cts.Dispose();
            }
        }
    }

    // Null when the read should translate the whole text instead (single block, or planning failed).
    private async Task<TranslationStreamStart?> PlanStreamAsync(ActivityEntry entry, string text, CancellationToken token)
    {
        try
        {
            return await _translation!.PlanStreamAsync(text, entry.Id, token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _systemLog.Warning("streamed translation could not start; translating the whole text", entry.Id, ex);
            return null;
        }
    }

    // Translation could not run for this text (same language, pack downloading, ...): say why, read the original.
    private async Task ReadOriginalAsync(ActivityEntry entry, ReadTranslationResult terminal, CancellationTokenSource cts, int originalLength)
    {
        if (!cts.IsCancellationRequested && terminal.IsNotable)
        {
            TranslationFinished?.Invoke(this, terminal);
        }

        if (!ReferenceEquals(_activeEntry, entry))
        {
            PublishGenerating();
            return;
        }

        CountReadStarted(entry, originalLength, terminal.Source, false, null);
        ReadTextChanged?.Invoke(this, new ReadText(terminal.Text, false));
        Task speak = _reader.SpeakAsync(terminal.Text, entry.Id);
        StopIfSuperseded(entry);
        await speak;
        await FailIfNeverPlayedAsync(entry);
    }

    private async Task ReadStreamAsync(ActivityEntry entry, TranslationStream stream, CancellationToken token, int originalLength, long translateStarted)
    {
        // Cancelling pumpCts also cancels a translation still running when the read ends or fails.
        using var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        await using IAsyncEnumerator<string> blocks = stream.BlocksAsync(pumpCts.Token).GetAsyncEnumerator(pumpCts.Token);
        if (!await blocks.MoveNextAsync() || !ReferenceEquals(_activeEntry, entry))
        {
            PublishGenerating(); // superseded while the first block translated
            return;
        }

        string first = blocks.Current;
        var spoken = new StringBuilder(first);
        var firstRead = new ReadText(first, stream.Outcome == TranslationOutcome.Translated);
        CountStreamStarted(entry, stream, originalLength, translateStarted);
        DropHeadStartIfTranslated(firstRead);
        ReadTextChanged?.Invoke(this, firstRead);

        Task speak = _reader.SpeakStreamAsync(first, entry.Id);
        StopIfSuperseded(entry);
        Task pump = PumpBlocksAsync(entry, stream, blocks, spoken, pumpCts.Token);
        try
        {
            await speak;
        }
        finally
        {
            await pumpCts.CancelAsync(); // the read ended or failed: stop translating text nobody will hear
            await pump;
        }

        await FailIfNeverPlayedAsync(entry);
    }

    // Translates the remaining blocks and hands each to the reader the moment it is ready. The reading text
    // box learns the longer text first, so the new chunks' source ranges refer to text it already has.
    private async Task PumpBlocksAsync(
        ActivityEntry entry, TranslationStream stream, IAsyncEnumerator<string> blocks, StringBuilder spoken, CancellationToken token)
    {
        try
        {
            while (await blocks.MoveNextAsync())
            {
                if (!ReferenceEquals(_activeEntry, entry))
                {
                    return;
                }

                spoken.Append(ChunkFeed.BlockJoin).Append(blocks.Current);
                ReadTextChanged?.Invoke(this, new ReadText(spoken.ToString(), stream.Outcome == TranslationOutcome.Translated, Extends: true));
                _reader.AppendText(blocks.Current, entry.Id);
            }

            TranslationFinished?.Invoke(this, new ReadTranslationResult(spoken.ToString(), stream.Outcome, stream.Source, stream.Target));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Superseded or stopped: the reader is stopped by whoever did that.
        }
        catch (Exception ex)
        {
            _systemLog.Warning("streamed translation stopped; reading what was translated", entry.Id, ex);
        }
        finally
        {
            _reader.CompleteStream(entry.Id);
        }
    }
}
