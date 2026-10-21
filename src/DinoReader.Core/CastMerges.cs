namespace DinoReader.Core;

/// <summary>
/// Applies a grown-up's merges (Slice 65) to an attribution: the lines of a merged-away character
/// are re-attributed to the character they joined, whose name, gender and voice they now share.
/// A merge whose either side is no longer a character in the text (the text was fixed) is ignored.
/// </summary>
public static class CastMerges
{
    public static AttributionResult Apply(AttributionResult result, IReadOnlyDictionary<string, string> merges)
    {
        Dictionary<string, string> usable = Usable(result.Characters, merges);
        if (usable.Count == 0)
        {
            return result;
        }

        return new AttributionResult(
            result.Segments.Select(s => s.CharacterId is { } id ? s with { CharacterId = Target(usable, id) } : s).ToList(),
            MergeCharacters(result.Characters, usable));
    }

    /// <summary>The gender a merged character takes: a known one beats unknown, and on a conflict the kept one's stays.</summary>
    public static Gender BetterGender(Gender kept, Gender joined) => kept == Gender.Unknown ? joined : kept;

    /// <summary>Records that <paramref name="dropId"/> joined <paramref name="keepId"/>, repointing earlier merges into it.</summary>
    public static IReadOnlyDictionary<string, string> Add(
        IReadOnlyDictionary<string, string> merges, string keepId, string dropId)
    {
        string keep = Target(merges, keepId);
        var result = merges.ToDictionary(m => m.Key, m => m.Value == dropId ? keep : m.Value);
        if (keep != dropId)
        {
            result[dropId] = keep;
        }

        return result;
    }

    // An entry is usable when both ends are characters in the text and its target is not itself merged away
    // (a cycle, or a key that is also a target of a hand-edited map); such entries are dropped, so both stay.
    private static Dictionary<string, string> Usable(
        IReadOnlyList<Character> characters, IReadOnlyDictionary<string, string> merges)
    {
        var ids = characters.Select(c => c.Id).ToHashSet();
        var chased = merges
            .Where(m => ids.Contains(m.Key))
            .Select(m => (m.Key, Target: Target(merges, m.Key)))
            .Where(m => m.Target != m.Key && ids.Contains(m.Target))
            .ToDictionary(m => m.Key, m => m.Target);
        return chased.Where(m => !chased.ContainsKey(m.Value)).ToDictionary(m => m.Key, m => m.Value);
    }

    // Follows a chain of merges to its end. A cycle returns the id it started from.
    private static string Target(IReadOnlyDictionary<string, string> merges, string id)
    {
        string current = id;
        for (int step = 0; step <= merges.Count && merges.TryGetValue(current, out string? next); step++)
        {
            current = next;
            if (current == id)
            {
                return id;
            }
        }

        return current;
    }

    private static Gender GroupGender(Character kept, IEnumerable<Character> group) =>
        group.Aggregate(kept.Gender, (gender, member) => BetterGender(gender, member.Gender));

    private static List<Character> MergeCharacters(IReadOnlyList<Character> characters, Dictionary<string, string> usable)
    {
        var groups = characters.GroupBy(c => Target(usable, c.Id)).ToDictionary(g => g.Key);
        return characters
            .Where(c => !usable.ContainsKey(c.Id))
            .Select(c => c with { WordCount = groups[c.Id].Sum(m => m.WordCount), Gender = GroupGender(c, groups[c.Id]) })
            .ToList();
    }
}
