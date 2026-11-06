namespace ReadTheStupidText.Application.Usage;

/// <summary>
/// Where a feature fell back to something else (Slice 273): the closed list of the <c>fallback</c> count's area. The Worker's <c>FALLBACK_AREAS</c> must be the same list
/// (a test in each app's suite compares them). Later parts of the slice append their areas here and there, inside their own block.
/// </summary>
public static class FallbackArea
{
    /// <summary>Dino Reader: a read that should have used the book's pre-made audio used the device voice for some or all pieces.</summary>
    public const string PremadeAudio = "premade_audio";

    public static IReadOnlyList<string> All { get; } = [PremadeAudio];
}

/// <summary>Why it fell back (Slice 273): the closed list of the <c>fallback</c> count's reason; the Worker's <c>FALLBACK_REASONS</c> must be the same list.</summary>
public static class FallbackReason
{
    public const string NoPack = "no_pack";
    public const string PackFailed = "pack_failed";
    public const string TextMismatch = "text_mismatch";
    public const string ClipMissing = "clip_missing";

    /// <summary>Some pieces of a read had a clip and some fell back to the device voice.</summary>
    public const string Partial = "partial";

    public static IReadOnlyList<string> All { get; } = [NoPack, PackFailed, TextMismatch, ClipMissing, Partial];
}

/// <summary>
/// One fallback kind for one book and language, with how many times it happened in the session (Slice 273). <see cref="Book"/> is the book's real id (an own book's too), or empty for a count about no book.
/// </summary>
public readonly record struct FallbackTotal(string Area, string Reason, string Book, string Language, int Count);

/// <summary>
/// Counts the fallbacks of one app session (Slice 273), shared by both apps, so a fallback inside a loop is one row, not hundreds: the first of each (area, reason, book, language) is sent at once with
/// count 1 (<see cref="IsFirst"/>); on any flush <see cref="TakeRepeats"/> hands out one row per kind whose count is above what was already sent, with the session total, and marks it sent, so a row never repeats for an
/// unchanged count. Queries take max(count) per session, area, reason and book. In memory only; a new launch starts at zero.
/// </summary>
public sealed class FallbackTally
{
    private const int FirstTime = 1;
    private readonly object _lock = new();
    private readonly Dictionary<(string Area, string Reason, string Book, string Language), (int Count, int Sent)> _counts = [];

    /// <summary>Notes one more fallback of <paramref name="area"/> and <paramref name="reason"/> for <paramref name="book"/> (its real id, or empty for no book) read in <paramref name="language"/> and returns how many there are now.</summary>
    public int Note(string area, string reason, string book = "", string language = "")
    {
        lock (_lock)
        {
            (int count, int sent) = _counts.GetValueOrDefault((area, reason, book, language), (0, 0));
            _counts[(area, reason, book, language)] = (count + 1, count == 0 ? FirstTime : sent);
            return count + 1;
        }
    }

    /// <summary>True for the first fallback of its kind in the session, the only one sent at once.</summary>
    public static bool IsFirst(int count) => count == FirstTime;

    /// <summary>
    /// The kinds that happened again since their last row, each with its total so far; asking marks them sent, so a second flush with nothing new returns none.
    /// </summary>
    public IReadOnlyList<FallbackTotal> TakeRepeats()
    {
        lock (_lock)
        {
            List<FallbackTotal> repeats = [];
            foreach (((string area, string reason, string book, string language), (int count, int sent)) in _counts.ToArray())
            {
                if (count > sent)
                {
                    repeats.Add(new FallbackTotal(area, reason, book, language, count));
                    _counts[(area, reason, book, language)] = (count, count);
                }
            }

            return repeats;
        }
    }
}
