namespace DinoReader.Core;

/// <summary>How the text of a scanned or PDF import is put together and told apart from the grown-up's edits (Slice 64).</summary>
public static class ImportedText
{
    /// <summary>One page's text: its recognised lines, one per row, in reading order.</summary>
    public static string PageText(IEnumerable<string> lines) => string.Join('\n', lines);

    /// <summary>Whether the text in the editor is still what the import produced (line endings and
    /// trailing blanks aside). Only then do the page pictures still match it and get kept.</summary>
    public static bool Unchanged(string current, string imported) => Normalize(current) == Normalize(imported);

    /// <summary>Splits a document's own flowing text (one string for all pages) into one text per page, by where each
    /// page's recognised lines sit in it. A page's text runs from its first found line to the next page's first found
    /// line (the first page from the start), so the lines only decide the cut points and the text keeps its own
    /// paragraphs, joins and hyphenation. A page none of whose lines is found has an empty text; null when no line of
    /// any page is found at all (the text and the lines do not belong together).</summary>
    public static IReadOnlyList<string>? SplitByPages(string text, IReadOnlyList<IReadOnlyList<string>> pageLines)
    {
        int[] cuts = Enumerable.Repeat(-1, pageLines.Count).ToArray();
        foreach (LocatedLine located in RangeToBoxMapper.Locate(text, pageLines))
        {
            cuts[located.Ref.Page] = cuts[located.Ref.Page] < 0 ? located.Start : cuts[located.Ref.Page];
        }

        if (cuts.All(c => c < 0))
        {
            return null;
        }

        int firstFound = Array.FindIndex(cuts, c => c >= 0);
        cuts[firstFound] = 0;
        var pages = new List<string>();
        for (int page = 0; page < cuts.Length; page++)
        {
            int next = Array.FindIndex(cuts, page + 1, c => c >= 0);
            int end = cuts[page] < 0 ? 0 : next < 0 ? text.Length : cuts[next];
            pages.Add(cuts[page] < 0 ? string.Empty : text[cuts[page]..end].Trim());
        }

        return pages;
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").Trim();
}
