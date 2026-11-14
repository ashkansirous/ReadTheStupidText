using System.Diagnostics;

namespace DinoReader.Core;

/// <summary>
/// Slice 259 item 1: the stopwatch of one read's start. It belongs to the read whose id it was made with, and answers only for that id, so an event that belongs to another
/// read (a Playing raised for the read before, a voicing that finished after a newer read began) gets no time and can neither log into nor clear this read's trace.
/// </summary>
public sealed class ReadStartClock
{
    private readonly int _readId;
    private readonly Func<long> _elapsedMilliseconds;

    public ReadStartClock(int readId) : this(readId, StopwatchMilliseconds()) { }

    public ReadStartClock(int readId, Func<long> elapsedMilliseconds)
    {
        _readId = readId;
        _elapsedMilliseconds = elapsedMilliseconds;
    }

    /// <summary>Milliseconds since this read began when <paramref name="readId"/> is the read this clock belongs to; null for any other read.</summary>
    public long? ElapsedFor(int readId) => readId == _readId ? _elapsedMilliseconds() : null;

    private static Func<long> StopwatchMilliseconds()
    {
        var watch = Stopwatch.StartNew();
        return () => watch.ElapsedMilliseconds;
    }
}
