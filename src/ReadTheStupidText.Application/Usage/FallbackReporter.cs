namespace ReadTheStupidText.Application.Usage;

/// <summary>
/// Reports that a feature fell back (Slice 273) through the app's <see cref="IUsageRecorder"/>, with the same once-per-session rule as Dino Reader (the shared <see cref="FallbackTally"/>):
/// the first of each (area, reason, language) is queued at once with count 1, and <see cref="FlushRepeats"/> queues one more row per kind that happened again, with the session total.
/// Not wired into the RTST app yet: parts B-D of the slice call <see cref="Report"/> at their fallback points and <see cref="FlushRepeats"/> from the app's session flush.
/// Never throws: counting must not change what the fallback does.
/// </summary>
public sealed class FallbackReporter(IUsageRecorder recorder)
{
    private readonly FallbackTally _tally = new();

    /// <summary>Notes the fallback and queues its count when it is the first of its kind; <paramref name="area"/> and <paramref name="reason"/> are <see cref="FallbackArea"/> and <see cref="FallbackReason"/> values.</summary>
    public void Report(string area, string reason, string? language = null)
    {
        try
        {
            int count = _tally.Note(area, reason, string.Empty, language ?? string.Empty);
            if (FallbackTally.IsFirst(count))
            {
                Queue(area, reason, count, language);
            }
        }
        catch (Exception)
        {
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
            // Best effort, as in Report.
        }
    }

    private void Queue(string area, string reason, int count, string? language) =>
        recorder.Record(context => UsageEvents.Fallback(context, area, reason, count, language));
}
