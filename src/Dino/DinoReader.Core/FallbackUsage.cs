using Sirous.Reading.Application.Usage;

namespace DinoReader.Core;

/// <summary>
/// The <c>fallback</c> count of Dino Reader (Slice 273): its detail is <c>&lt;area&gt;:&lt;reason&gt;</c>, both from the closed <see cref="FallbackArea"/> and <see cref="FallbackReason"/> lists
/// the Worker also holds. No free text, file name or book text can be carried.
/// </summary>
public static class FallbackUsage
{
    private const char Separator = ':';

    /// <summary>The detail of a <c>fallback</c> count; null when either half is not on its closed list, so nothing outside the lists is ever queued.</summary>
    public static string? Detail(string area, string reason) =>
        FallbackArea.All.Contains(area) && FallbackReason.All.Contains(reason) ? string.Concat(area, Separator, reason) : null;

    private static readonly HashSet<string> BookReasons =
    [
        FallbackReason.StoryIdMismatch, FallbackReason.StoryEmpty, FallbackReason.StoryInvalid, FallbackReason.StoryNewerVersion,
        FallbackReason.StoryNoPages, FallbackReason.StoryMissingFields, FallbackReason.BackupBookUnapplied,
    ];

    /// <summary>Slice 425: true for the reasons whose detail may end with the store book's catalogue id (the Worker's <c>FALLBACK_BOOK_REASONS</c>; a test keeps them the same).</summary>
    public static bool NamesStoreBook(string reason) => BookReasons.Contains(reason);

    /// <summary>Slice 425: the detail with the store book's catalogue id as a third part, <c>library:story_empty:&lt;id&gt;</c>; the id is normalised to 32 lower-case hex digits. A reason that does not name a store book, or an id that is not one, gives the two-part detail (or null when the kinds are off the lists), never a free-text part.</summary>
    public static string? Detail(string area, string reason, string? book)
    {
        string? plain = Detail(area, reason);
        return plain is not null && NamesStoreBook(reason) && UsageEvent.ShelfId(book) is { } id ? string.Concat(plain, Separator, id) : plain;
    }

    /// <summary>Slice 425: the normalised id of <paramref name="book"/> when it is a 32-hex id the caller's <paramref name="isCatalogueId"/> accepts, else null: an imported own package's id is never a store id and is never sent.</summary>
    public static string? CatalogueBookOf(string? book, Func<string, bool> isCatalogueId) =>
        UsageEvent.ShelfId(book) is { } id && isCatalogueId(id) ? id : null;

    /// <summary>Slice 425: the fallback reason of a story file that could not be read.</summary>
    public static string ReasonOf(StoryFormatError error) => error switch
    {
        StoryFormatError.Empty => FallbackReason.StoryEmpty,
        StoryFormatError.NewerVersion => FallbackReason.StoryNewerVersion,
        StoryFormatError.NoPages => FallbackReason.StoryNoPages,
        StoryFormatError.MissingFields => FallbackReason.StoryMissingFields,
        _ => FallbackReason.StoryInvalid,
    };

    /// <summary>Slice 425: the reason a failed shelf rebuild reports, from the exception it caught: a timeout (also a cancel that wraps one), a cancel, a file or network error, a library read error, a screen-building error, else other.</summary>
    public static string ShelfRebuildReasonOf(Exception ex) => ex switch
    {
        TimeoutException or OperationCanceledException { InnerException: TimeoutException } => FallbackReason.ShelfRebuildTimeout,
        OperationCanceledException => FallbackReason.ShelfRebuildCancelled,
        IOException or UnauthorizedAccessException or HttpRequestException => FallbackReason.ShelfRebuildIo,
        StoryFormatException or StoryBackupException or StoryPackageException => FallbackReason.ShelfRebuildLibrary,
        ObjectDisposedException or NullReferenceException or KeyNotFoundException => FallbackReason.ShelfRebuildOther, // code faults, not layout
        InvalidOperationException or ArgumentException or IndexOutOfRangeException => FallbackReason.ShelfRebuildLayout,
        _ => FallbackReason.ShelfRebuildOther,
    };

    /// <summary>The voices a book suggested that the roster does not hold at all (plain membership: a voice that is in the roster but taken or of the other gender is not missing). Empty and null suggestions are none.</summary>
    public static IReadOnlyList<string> MissingFromRoster(IEnumerable<string?> suggested, IEnumerable<string> rosterIds)
    {
        var roster = new HashSet<string>(rosterIds);
        return suggested.OfType<string>().Where(id => id.Length > 0 && !roster.Contains(id)).Distinct().ToList();
    }
}
