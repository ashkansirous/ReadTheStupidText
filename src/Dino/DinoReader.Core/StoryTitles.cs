namespace DinoReader.Core;

/// <summary>Picks a title for a story the grown-up did not name.</summary>
public static class StoryTitles
{
    private const int MaxLength = 40;

    /// <summary>The first line of <paramref name="text"/>, cut at a word boundary to at most 40 characters; empty if the text is blank.</summary>
    public static string FromText(string text)
    {
        string firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
        return firstLine.Length <= MaxLength ? firstLine : CutAtWord(firstLine);
    }

    private static string CutAtWord(string line)
    {
        string head = line[..MaxLength];
        int space = head.LastIndexOf(' ');
        return (space > 0 ? head[..space] : head).TrimEnd();
    }
}
