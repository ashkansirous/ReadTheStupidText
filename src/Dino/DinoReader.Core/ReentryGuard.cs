namespace DinoReader.Core;

/// <summary>
/// Slice 351 (review): lets a piece of code run once per thread at a time, so a report raised from inside it that comes back round to it is skipped instead of looping.
/// <c>using</c> the result of <see cref="TryEnter"/> when it is not null.
/// </summary>
public sealed class ReentryGuard
{
    private readonly ThreadLocal<bool> _inside = new(() => false);

    /// <summary>A scope to dispose when done, or null when this thread is already inside.</summary>
    public IDisposable? TryEnter()
    {
        if (_inside.Value)
        {
            return null;
        }

        _inside.Value = true;
        return new Scope(this);
    }

    private sealed class Scope(ReentryGuard guard) : IDisposable
    {
        public void Dispose() => guard._inside.Value = false;
    }
}
