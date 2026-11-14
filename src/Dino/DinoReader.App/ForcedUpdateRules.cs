namespace DinoReader;

/// <summary>
/// The pure rules behind the Debug-only forced book update (Slice 209 hook <c>stage-book-update force=true</c>), kept free of MAUI types so they are unit-tested (Slice 225 item 16).
/// The hook marks a book "forced", and the next cold start's apply lowers the book's recorded version so the staged package counts as an update.
/// </summary>
public static class ForcedUpdateRules
{
    /// <summary>
    /// How long a force-apply mark counts. The hook stages, then the check cold-starts the app within minutes; a mark older than this was left by a stage that failed
    /// (or a check that never came), and must not force a later, ordinary update of the same book.
    /// </summary>
    public static readonly TimeSpan MarkLifetime = TimeSpan.FromMinutes(30);

    /// <summary>True when a mark written at <paramref name="markedAtUtcTicks"/> (0 or less: no mark) still forces the apply at <paramref name="now"/>.</summary>
    public static bool IsMarkLive(long markedAtUtcTicks, DateTimeOffset now)
    {
        if (markedAtUtcTicks <= 0)
        {
            return false;
        }

        TimeSpan age = now - new DateTimeOffset(markedAtUtcTicks, TimeSpan.Zero);
        return age >= TimeSpan.Zero && age <= MarkLifetime;
    }
}

/// <summary>
/// Remembers the recorded version each forced book had before the apply lowered it, so a forced entry that is dropped (up to date, edited, a bad package) puts the version back and
/// does not leave a lowered one that makes a later ordinary update look newer than it is. One per apply run.
/// </summary>
public sealed class ForcedVersionTracker
{
    private readonly Dictionary<string, int?> _original = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Notes the version the book had before it was lowered. The first note wins (a second lowering must not record the lowered value as the original).</summary>
    public void Lowered(string bookId, int? originalVersion) => _original.TryAdd(bookId, originalVersion);

    /// <summary>Forgets the note without restoring anything (the book was swapped, and the swap recorded the new version).</summary>
    public void Forget(string bookId) => _original.Remove(bookId);

    /// <summary>
    /// True when the book was lowered by this run: <paramref name="originalVersion"/> is what to put back (null: the book had none, so remove it). The note is used up,
    /// so a book is restored once.
    /// </summary>
    public bool TryTakeRestore(string bookId, out int? originalVersion)
    {
        if (_original.Remove(bookId, out originalVersion))
        {
            return true;
        }

        originalVersion = null;
        return false;
    }
}
