namespace DinoReader.Core;

/// <summary>
/// Single-flight with a dirty flag, for a scan that many events ask for (Slice 278 part B, reviews/s278b-check2.md 2+3): a request while a scan runs does not start a second one, it makes the
/// running loop scan once more after the current scan. <see cref="Request"/> says whether the caller must start the loop; the loop calls <see cref="TakeNext"/> until it returns false. Pure; thread-safe.
/// </summary>
public sealed class ScanRequests
{
    private readonly object _lock = new();
    private bool _running;
    private bool _dirty;

    /// <summary>Marks a scan as wanted; true when no loop is running, so the caller starts one.</summary>
    public bool Request()
    {
        lock (_lock)
        {
            _dirty = true;
            if (_running)
            {
                return false;
            }

            _running = true;
            return true;
        }
    }

    /// <summary>For the loop: true when a scan was asked for since the last one (and clears the flag); false ends the loop.</summary>
    public bool TakeNext()
    {
        lock (_lock)
        {
            if (!_dirty)
            {
                _running = false;
                return false;
            }

            _dirty = false;
            return true;
        }
    }
}
