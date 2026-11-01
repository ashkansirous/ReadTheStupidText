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
public sealed record BackCover(string StoryBy, bool Retold, string Illustration, int Year, int Month)
{
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
    public static BackCover? TryCreate(string? storyBy, bool retold, string? illustration, string? month)
    {
        if (string.IsNullOrWhiteSpace(storyBy) || !TryParseMonth(month, out int year, out int monthNumber))
        {
            return null;
        }

        return new BackCover(storyBy.Trim(), retold, string.IsNullOrWhiteSpace(illustration) ? Maker : illustration.Trim(), year, monthNumber);
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
