namespace Sirous.Reading.Application.Usage;

/// <summary>
/// Slice 460: one session-scoped "first-run list failure reported" flag. Only the first-run picker's retried list sequence reports (the prefetch is silent); its first failure reports and a later Try again
/// reports nothing, so one failed launch is one store_* row. A sequence that succeeds, or is cancelled, never calls this. Thread-safe.
/// </summary>
public sealed class FirstRunFailureOnce
{
    private int _reported;

    /// <summary>Runs <paramref name="report"/> if no first-run list failure was reported yet in this session, and says whether it ran.</summary>
    public bool TryReport(Action report)
    {
        if (Interlocked.Exchange(ref _reported, 1) != 0)
        {
            return false;
        }

        report();
        return true;
    }
}
