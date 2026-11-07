namespace DinoReader.Core;

/// <summary>
/// What opening a book remembers for the quiet audio start (Slice 285): whether a store listing fetched a moment ago can be reused, and which (language, book) runs already failed in this app run,
/// so reopening the book does not start the same failing run again. Pure and thread-safe; the app passes the clock.
/// </summary>
public sealed class ShelfAudioOpenMemory
{
    /// <summary>How long a listing fetched for one open serves the next opens.</summary>
    public static readonly TimeSpan ListingLife = TimeSpan.FromMinutes(5);

    private const string AnyBook = "*";
    private readonly object _lock = new();
    private readonly HashSet<string> _failed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether a listing fetched at <paramref name="fetchedAt"/> (null: never) is still fresh at <paramref name="now"/>.</summary>
    public static bool ListingIsFresh(DateTimeOffset? fetchedAt, DateTimeOffset now) =>
        fetchedAt is { } at && now >= at && now - at < ListingLife;

    /// <summary>Remembers that a run for <paramref name="language"/> failed for <paramref name="bookId"/>, or for the whole language when the book is null (the plan or the run itself failed).</summary>
    public void MarkFailed(string language, string? bookId)
    {
        lock (_lock)
        {
            _failed.Add(Key(language, bookId));
        }
    }

    /// <summary>Forgets a failure of that book (its pack is in now).</summary>
    public void Clear(string language, string bookId)
    {
        lock (_lock)
        {
            _failed.Remove(Key(language, bookId));
        }
    }

    /// <summary>Whether opening <paramref name="bookId"/> must not start a run again: that book, or the whole language, failed earlier in this app run.</summary>
    public bool HasFailed(string language, string bookId)
    {
        lock (_lock)
        {
            return _failed.Contains(Key(language, bookId)) || _failed.Contains(Key(language, null));
        }
    }

    private static string Key(string language, string? bookId) => $"{language}|{bookId ?? AnyBook}";
}
