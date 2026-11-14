namespace DinoReader.Core;

/// <summary>
/// Tracks a press-and-hold against a clock the caller supplies (a stopwatch's elapsed time), so
/// the gate's "hold for 3 seconds" rule is testable without a UI.
/// </summary>
public sealed class HoldTracker(TimeSpan required)
{
    public static readonly TimeSpan GateHold = TimeSpan.FromSeconds(3);

    private TimeSpan? _startedAt;

    public bool IsHolding => _startedAt is not null;

    public void Start(TimeSpan now) => _startedAt = now;

    public void Cancel() => _startedAt = null;

    /// <summary>0 when not holding, rising to 1 once the required time has passed.</summary>
    public double Progress(TimeSpan now)
    {
        if (_startedAt is not { } start)
        {
            return 0;
        }

        return Math.Clamp((now - start) / required, 0, 1);
    }

    public bool IsComplete(TimeSpan now) => IsHolding && Progress(now) >= 1;
}
