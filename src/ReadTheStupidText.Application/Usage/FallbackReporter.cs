namespace ReadTheStupidText.Application.Usage;

/// <summary>
/// Reports that a feature fell back (Slice 273) through the app's <see cref="IUsageRecorder"/>, with the same once-per-session rule as Dino Reader (the shared <see cref="FallbackTally"/>):
/// the first of each (area, reason, language) is queued at once with count 1, and <see cref="FlushRepeats"/> queues one more row per kind that happened again, with the session total.
/// It is Read The Stupid Text's <see cref="IFallbackSink"/> (Slice 273 part B): shared code calls <see cref="Report"/> at its fallback points, and the app calls <see cref="FlushRepeats"/> at its session flush.
/// Never throws: counting must not change what the fallback does.
/// </summary>
public sealed class FallbackReporter(IUsageRecorder recorder) : IFallbackSink
{
    private readonly FallbackTally _tally = new();

    /// <summary>Notes the fallback and queues its count when it is the first of its kind; <paramref name="area"/> and <paramref name="reason"/> are <see cref="FallbackArea"/> and <see cref="FallbackReason"/> values.</summary>
    public void Report(string area, string reason, string? language = null)
    {
        try
        {
            string? code = UsageEvents.Language(language); // one code for the tally key and the event, so "en-US" and "en" are one kind
            int count = _tally.Note(area, reason, string.Empty, code ?? string.Empty);
            if (FallbackTally.IsFirst(count))
            {
                Queue(area, reason, count, code);
            }
        }
        catch (Exception)
        {
            // no-fallback: counting is best effort and never reports itself
            // Counting is best effort and a fallback is never instrumented from inside the usage code (no loop).
        }
    }

    /// <summary>At the session flush: queues one row with the session total for each kind that happened again since its last row.</summary>
    public void FlushRepeats()
    {
        try
        {
            foreach (FallbackTotal total in _tally.TakeRepeats())
            {
                Queue(total.Area, total.Reason, total.Count, total.Language.Length == 0 ? null : total.Language);
            }
        }
        catch (Exception)
        {
            // no-fallback: counting is best effort and never reports itself
            // Best effort, as in Report.
        }
    }

    private void Queue(string area, string reason, int count, string? language) =>
        recorder.Record(context => UsageEvents.Fallback(context, area, reason, count, language));
}
