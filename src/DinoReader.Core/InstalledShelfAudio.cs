namespace DinoReader.Core;

/// <summary>
/// The one answer to "which languages can be read" (Slice 278 part B, reviews/s278b-design.md): a snapshot of which (book, language) pre-made audio packs are installed. The shelf offers a
/// language when its audio is installed for at least one shelf book, a book when it is installed for that book; the main language is always offered. An active download neither hides
/// nor shows a language. The owner scans the disk off the UI thread and saves the last snapshot, so a cold start can show the last-known list before the scan corrects it. Pure.
/// </summary>
public sealed class InstalledShelfAudio
{
    private const char PairSeparator = '\n';
    private const char FieldSeparator = '\t';

    private readonly HashSet<(string Language, string BookId)> _installed;

    public InstalledShelfAudio(IEnumerable<(string Language, string BookId)> installed) => _installed = [.. installed];

    /// <summary>Nothing installed (before the first scan, with nothing saved).</summary>
    public static InstalledShelfAudio Empty { get; } = new([]);

    /// <summary>True when the two snapshots hold the same packs.</summary>
    public bool SameAs(InstalledShelfAudio other) => _installed.SetEquals(other._installed);

    /// <summary>The child's list for the shelf: the main language, and each other language whose audio is installed for at least one shelf book.</summary>
    public IReadOnlyList<string> ForShelf(IEnumerable<string> languages, string main) =>
        [.. languages.Where(language => language == main || _installed.Any(pair => pair.Language == language))];

    /// <summary>Whether a book can be read in a language: the main language always, a book with no pack id always (no pack to wait for), otherwise when its audio is installed.</summary>
    public bool OffersBook(string language, string main, string? bookId) =>
        language == main || bookId is null || _installed.Contains((language, bookId));

    /// <summary>Whether this book's audio is on the phone in this language (the main language counts only when it is installed too, unlike <see cref="OffersBook"/>).</summary>
    public bool HasPack(string language, string bookId) => _installed.Contains((language, bookId));

    /// <summary>The text saved to preferences.</summary>
    public string Serialize() => string.Join(PairSeparator, _installed.Select(pair => pair.Language + FieldSeparator + pair.BookId));

    /// <summary>The snapshot saved by <see cref="Serialize"/>; a missing or damaged text gives an empty one.</summary>
    public static InstalledShelfAudio Parse(string? saved)
    {
        if (string.IsNullOrEmpty(saved))
        {
            return Empty;
        }

        var pairs = new List<(string, string)>();
        foreach (string line in saved.Split(PairSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = line.Split(FieldSeparator);
            if (fields.Length == 2 && fields[0].Length > 0 && fields[1].Length > 0)
            {
                pairs.Add((fields[0], fields[1]));
            }
        }

        return new InstalledShelfAudio(pairs);
    }
}
