namespace ReadTheStupidText.Application.Usage;

/// <summary>
/// One fallback row per read (Slice 273 part B). A read can hit several fallback points (a stream that fails, then the whole-text retry of the same read through the same engine);
/// the first one it hits is its reason and the rest add nothing. A read is told apart by its activity id, so a new read starts fresh and counts again, and a call with no id
/// is its own read. Only the id of the latest reported read is kept, nothing about earlier reads, so no failure is ever hidden by an older one.
/// </summary>
public sealed class PerReadFallbacks(IFallbackSink sink)
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IFallbackSink, PerReadFallbacks> Shared = new();

    private readonly object _gate = new();
    private int? _reportedRead;

    /// <summary>The one instance for <paramref name="sink"/>: the composite reader and the phone voice reader of an app share it, so a read that the composite already counted (the natural voice was not ready) adds no second row from the phone reader.</summary>
    public static PerReadFallbacks Of(IFallbackSink sink) => Shared.GetValue(sink, s => new PerReadFallbacks(s));

    /// <summary>Reports <paramref name="reason"/> unless this read (<paramref name="readId"/>) already reported one.</summary>
    public void Report(int? readId, string area, string reason, string? language = null)
    {
        if (!Claim(readId))
        {
            return;
        }

        sink.Report(area, reason, language);
    }

    /// <summary>Marks the read as already counted (a fallback reported where it happened, such as an engine that failed to build), so nothing else is added for it.</summary>
    public void MarkReported(int? readId) => Claim(readId);

    private bool Claim(int? readId)
    {
        lock (_gate)
        {
            if (readId is not { } id)
            {
                _reportedRead = null;
                return true;
            }

            if (_reportedRead == id)
            {
                return false;
            }

            _reportedRead = id;
            return true;
        }
    }
}
