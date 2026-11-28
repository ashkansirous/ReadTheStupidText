namespace Sirous.Reading.Application.Usage;

/// <summary>
/// A fallback sink that can also name the store book a fallback is about (Slice 425). Only the reasons that name a store book (<c>FallbackUsage.NamesStoreBook</c>) send it; a sink that is not one
/// of these drops the book and reports the reason alone, so <see cref="IFallbackSink"/> and every existing caller stay as they are.
/// </summary>
public interface IBookFallbackSink : IFallbackSink
{
    /// <summary>Reports a fallback about the store book <paramref name="book"/> (its catalogue id, 32 lower-case hex digits).</summary>
    void ReportBook(string area, string reason, string book);
}

/// <summary>Reports through <see cref="IBookFallbackSink"/> when the sink is one, else as a plain fallback with no book.</summary>
public static class BookFallbackSinks
{
    public static void ReportBook(this IFallbackSink sink, string area, string reason, string? book)
    {
        if (book is not null && sink is IBookFallbackSink withBook)
        {
            withBook.ReportBook(area, reason, book);
            return;
        }

        sink.Report(area, reason);
    }
}
