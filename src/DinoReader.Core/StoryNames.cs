using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace DinoReader.Core;

/// <summary>
/// The character names found in one story text. A single capitalised word is a name only
/// when corroborated (see <see cref="NameExtractor.ForStory"/>); runs of two or more words
/// are always names.
/// </summary>
internal sealed class StoryNames(string text, IReadOnlyList<NameMatch> all, HashSet<string> known)
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<int>> _occurrences = new();

    public string Text { get; } = text;

    /// <summary>Accepted names in the story, with absolute positions, sorted by position.</summary>
    public IReadOnlyList<NameMatch> All { get; } = all;

    public bool Accepts(string name) =>
        name.Contains(' ') || known.Contains(name.ToLowerInvariant());

    public IEnumerable<NameMatch> InRange(int start, int end)
    {
        for (int i = LowerBound(start); i < All.Count && All[i].End <= end; i++)
            yield return All[i];
    }

    public NameMatch? FirstFrom(int position, int limit)
    {
        int i = LowerBound(position);
        return i < All.Count && All[i].Start < limit ? All[i] : null;
    }

    /// <summary>End positions of every whole-word occurrence of a name, computed once per name.</summary>
    public IReadOnlyList<int> OccurrenceEnds(string name) =>
        _occurrences.GetOrAdd(name, FindOccurrenceEnds);

    private IReadOnlyList<int> FindOccurrenceEnds(string name) =>
        Regex.Matches(Text, $@"\b{Regex.Escape(name)}\b").Select(m => m.Index + m.Length).ToList();

    private int LowerBound(int position)
    {
        int low = 0, high = All.Count;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (All[mid].Start < position) low = mid + 1; else high = mid;
        }
        return low;
    }
}
