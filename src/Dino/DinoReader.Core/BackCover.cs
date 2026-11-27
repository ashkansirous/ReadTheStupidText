using System.Globalization;

namespace DinoReader.Core;

/// <summary>The kinds of line on a back cover (Slice 237); the app words each in the reading language.</summary>
public enum BackCoverLineKind
{
    StoryBy,
    OriginalStoryBy,
    TraditionalTale,
    RetoldBy,
    Illustration,
    Date,
}

/// <summary>One line of a back cover: its kind and the name it holds (none for a traditional tale and for the date).</summary>
public sealed record BackCoverLine(BackCoverLineKind Kind, string? Name = null);

/// <summary>
/// A book's silent back cover (Slice 237): who wrote the story, who retold it, who drew it and the month it was made. It is data, not a page:
/// <see cref="Story.BackCover"/> sits beside <see cref="Story.Pages"/> so it is never read, segmented, translated, counted or recorded.
/// In <c>story.json</c> it travels as one last page whose text is the credits in English, so an older app (which knows nothing of it) reads that page like any other
/// (<see cref="BackCoverEnglish"/>); this app takes the page back out and draws it (<see cref="StoryJson"/>).
/// </summary>
public sealed record BackCover(string StoryBy, bool Retold, string Illustration, int Year, int Month, string? PictureFile = null,
    Dictionary<string, Dictionary<string, string>>? NamesByLanguage = null)
{
    /// <summary>The drawn back-cover picture's file name in the story folder and package (Slice 359); the book's own file is <c>content/dino/&lt;book&gt;/back-cover.jpg</c>.</summary>
    public const string PictureName = "back-cover.jpg";

    /// <summary>The name of the person who retells the classics and draws every book.</summary>
    public const string Maker = "Ashkan Sirous";

    /// <summary>The <see cref="StoryBy"/> of a tale nobody owns: the line reads "A traditional tale" in the reading language.</summary>
    public const string Traditional = "traditional";

    /// <summary>Several names in <see cref="StoryBy"/> are written with this between them (<c>"Elham Shafiei Gol; Ashkan Sirous"</c>); the app joins them with the reading language's own "and".</summary>
    public const char NameSeparator = ';';

    public const int FirstMonth = 1;
    public const int LastMonth = 12;

    private const int MinYear = 1000;
    private const int MaxYear = 9999;
    private const string MonthFormat = "{0:D4}-{1:D2}";
    private const int MonthParts = 2;

    /// <summary>The month as the file keeps it: <c>2026-11</c>.</summary>
    public string MonthText => string.Format(MonthFormat, Year, Month);

    /// <summary>True for the "traditional" marker, whatever its case or surrounding spaces.</summary>
    public static bool IsTraditional(string? storyBy)
    {
        string marker = storyBy?.Trim() ?? string.Empty;
        return string.Equals(marker, Traditional, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The names in a <see cref="StoryBy"/> value, trimmed, in order.</summary>
    public static IReadOnlyList<string> Names(string storyBy)
    {
        const StringSplitOptions clean = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries;
        return storyBy.Split(NameSeparator, clean);
    }

    /// <summary>
    /// Slice 405: a name as the reading language writes it (<c>NamesByLanguage["fa"]["Aesop"]</c>); the name as written when the book has no spelling for that language or name, never an empty line.
    /// </summary>
    public string NameIn(string name, string language)
    {
        string? spelling = null;
        bool found = NamesByLanguage is not null && NamesByLanguage.TryGetValue(language, out Dictionary<string, string>? names) && names.TryGetValue(name, out spelling);
        return found && !string.IsNullOrWhiteSpace(spelling) ? spelling! : name;
    }

    /// <summary>
    /// Slice 405: the spelled names joined for a line. Two or more are joined with the language's "and"; when one of them already holds that "and" word on its own
    /// (French "Jacob et Wilhelm Grimm") they are joined with the language's list separator instead, so it never reads as three people.
    /// </summary>
    public static string JoinSpelled(IReadOnlyList<string> spelled, string andWord, Func<string, string, string> and, Func<string, string, string> list)
    {
        bool holdsAnd = spelled.Any(part => HoldsWord(part, andWord));
        Func<string, string, string> join = holdsAnd ? list : and;
        return spelled.Count == 0 ? string.Empty : spelled.Skip(1).Aggregate(spelled[0], (joined, next) => join(joined, next));
    }

    private static bool HoldsWord(string text, string word) =>
        !string.IsNullOrWhiteSpace(word) && text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(word.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Equality by content: the per-language spellings are compared entry by entry, not by dictionary reference.</summary>
    public bool Equals(BackCover? other) =>
        other is not null && StoryBy == other.StoryBy && Retold == other.Retold && Illustration == other.Illustration && Year == other.Year
        && Month == other.Month && PictureFile == other.PictureFile && SameNames(NamesByLanguage, other.NamesByLanguage);

    public override int GetHashCode() => HashCode.Combine(StoryBy, Retold, Illustration, Year, Month, PictureFile, NamesByLanguage?.Count ?? 0);

    private static bool SameNames(Dictionary<string, Dictionary<string, string>>? a, Dictionary<string, Dictionary<string, string>>? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out Dictionary<string, string>? other)
            && pair.Value.Count == other.Count && pair.Value.All(n => other.TryGetValue(n.Key, out string? v) && v == n.Value));
    }

    /// <summary>The lines in order: the story, the retelling (when it is one), the illustration, the date.</summary>
    public IReadOnlyList<BackCoverLine> Lines()
    {
        var lines = new List<BackCoverLine>();
        if (IsTraditional(StoryBy))
        {
            lines.Add(new BackCoverLine(BackCoverLineKind.TraditionalTale));
        }
        else
        {
            lines.Add(new BackCoverLine(Retold ? BackCoverLineKind.OriginalStoryBy : BackCoverLineKind.StoryBy, StoryBy));
        }

        if (Retold)
        {
            lines.Add(new BackCoverLine(BackCoverLineKind.RetoldBy, Maker));
        }

        lines.Add(new BackCoverLine(BackCoverLineKind.Illustration, Illustration));
        lines.Add(new BackCoverLine(BackCoverLineKind.Date));
        return lines;
    }

    /// <summary>The back cover for the file's fields, or null when they cannot make one (no names, a month that is not <c>yyyy-MM</c>).</summary>
    public static BackCover? TryCreate(string? storyBy, bool retold, string? illustration, string? month, string? pictureFile = null,
        Dictionary<string, Dictionary<string, string>>? namesByLanguage = null)
    {
        if (string.IsNullOrWhiteSpace(storyBy) || !TryParseMonth(month, out int year, out int monthNumber))
        {
            return null;
        }

        return new BackCover(storyBy.Trim(), retold, string.IsNullOrWhiteSpace(illustration) ? Maker : illustration.Trim(), year, monthNumber,
            string.IsNullOrWhiteSpace(pictureFile) ? null : pictureFile.Trim(), namesByLanguage is { Count: > 0 } ? namesByLanguage : null);
    }

    /// <summary>
    /// Every name the back cover shows (each writer unless the book is a traditional tale, the retelling's maker, the illustrator), in order, each once; the names <c>namesByLanguage</c> must spell.
    /// </summary>
    public static IReadOnlyList<string> ShownNames(string storyBy, bool retold, string? illustration)
    {
        var names = new List<string>();
        if (!IsTraditional(storyBy))
        {
            names.AddRange(Names(storyBy));
        }

        if (retold)
        {
            names.Add(Maker);
        }

        names.Add(string.IsNullOrWhiteSpace(illustration) ? Maker : illustration.Trim());
        return names.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>The year and month of <c>yyyy-MM</c> or of a full <c>yyyy-MM-dd</c> date.</summary>
    public static bool TryParseMonth(string? text, out int year, out int month)
    {
        year = 0;
        month = 0;
        string[] parts = (text ?? string.Empty).Split('-');
        return parts.Length >= MonthParts && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out year)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out month)
            && year is >= MinYear and <= MaxYear && month is >= FirstMonth and <= LastMonth;
    }
}

/// <summary>The back cover's credits in English: the text of the page an older app reads (Slice 237). This app never shows it; it draws <see cref="BackCover.Lines"/> in the reading language.</summary>
public static class BackCoverEnglish
{
    private const string LineSeparator = "\n\n";

    private static readonly string[] Months =
    [
        "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December",
    ];

    public static string PageText(BackCover cover)
    {
        IEnumerable<string> paragraphs = cover.Lines().Select(l => LineText(l, cover));
        return string.Join(LineSeparator, paragraphs);
    }

    private static string Joined(string? names)
    {
        IReadOnlyList<string> parts = BackCover.Names(names ?? string.Empty);
        return string.Join(" and ", parts);
    }

    private static string LineText(BackCoverLine line, BackCover cover) => line.Kind switch
    {
        BackCoverLineKind.StoryBy => $"Story by {Joined(line.Name)}",
        BackCoverLineKind.OriginalStoryBy => $"Original story by {Joined(line.Name)}",
        BackCoverLineKind.TraditionalTale => "A traditional tale",
        BackCoverLineKind.RetoldBy => $"Retold for young readers by {line.Name}",
        BackCoverLineKind.Illustration => $"Illustration: {line.Name}",
        _ => $"{Months[cover.Month - BackCover.FirstMonth]} {cover.Year}",
    };
}
