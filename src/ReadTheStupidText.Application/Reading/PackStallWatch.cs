using System.Diagnostics;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The pure decisions behind the pack download's stall and progress logging (Slice 241). A body that gives no byte for
/// <see cref="StallTimeout"/> is a stall (the HTTP path ends it in <see cref="StallGuardedRead"/>; the iOS background session has no read to time out, so its
/// watcher ticks a <see cref="PackStallClock"/>). A body that is moving writes a <c>PACKDL progress</c> line at most every <see cref="ProgressInterval"/>, so a silent log
/// next to a file that does not grow means "no progress and no stall detected", which is the bug to look for.
/// </summary>
public static class PackStallWatch
{
    public static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);

    public static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(5);
}

/// <summary>
/// The iOS watcher's no-byte clock (Slice 241 review). It does not run until <see cref="Arm"/> (the response began: a slow connect or TLS handshake is governed by the request
/// timeout, not this clock). Once armed it counts idle time only across ticks taken while the app is active and the transfer is running, and any progress, an
/// inactive tick (network down, app in the background) or a tick gap longer than <c>maxGap</c> (the app was suspended, so the timer did not run) gives the download a fresh timeout.
/// </summary>
public sealed class PackStallClock(TimeSpan timeout, TimeSpan maxGap, long start)
{
    private readonly object _gate = new();
    private long _lastTick = start;
    private TimeSpan _idle = TimeSpan.Zero;
    private bool _armed;

    /// <summary>The response began (headers or the first byte): the clock starts now. Does nothing once armed.</summary>
    public void Arm(long now)
    {
        lock (_gate)
        {
            if (!_armed)
            {
                _armed = true;
                _idle = TimeSpan.Zero;
                _lastTick = now;
            }
        }
    }

    /// <summary>A byte arrived (or the transfer resumed): the clock is armed and the idle time starts again.</summary>
    public void Reset(long now)
    {
        lock (_gate)
        {
            _armed = true;
            _idle = TimeSpan.Zero;
            _lastTick = now;
        }
    }

    /// <summary>One watcher tick. True when the response began, the transfer ran and the app was active for the whole timeout without a byte.</summary>
    public bool Tick(long now, bool active)
    {
        lock (_gate)
        {
            TimeSpan gap = Stopwatch.GetElapsedTime(_lastTick, now);
            _lastTick = now;
            _idle = !_armed || !active || gap > maxGap ? TimeSpan.Zero : _idle + gap;
            return _armed && _idle >= timeout;
        }
    }
}

/// <summary>Lets a progress line through at most once per interval. Not thread-safe by design: one gate per download loop.</summary>
public sealed class PackProgressLogGate(TimeSpan interval)
{
    private long _lastLogged = long.MinValue;

    /// <summary>True (and the gate resets) on the first call, or when <paramref name="now"/> is at least the interval after the last logged one.</summary>
    public bool ShouldLog(long now)
    {
        if (_lastLogged != long.MinValue && Stopwatch.GetElapsedTime(_lastLogged, now) < interval)
        {
            return false;
        }

        _lastLogged = now;
        return true;
    }
}
