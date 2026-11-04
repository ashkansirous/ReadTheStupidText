using System.Text.Json;

namespace DinoReader.Core;

/// <summary>Slice 260: the closed set of tags a store book can carry, in the order a card shows them. The user sets them by hand in a book's <c>book.json</c>.</summary>
public enum StoreTag
{
    Hot,
    Popular,
    Recommended,
    New,
}

/// <summary>Reads, names and orders <see cref="StoreTag"/>s (Slice 260). Names are case-sensitive: "Hot" is a tag, "hot" is not.</summary>
public static class StoreTags
{
    /// <summary>A card shows at most this many badges, in list order.</summary>
    public const int MaxBadges = 2;

    /// <summary>The tag for an exact name, or null for anything else.</summary>
    public static StoreTag? Parse(string? name) =>
        Enum.GetValues<StoreTag>().Cast<StoreTag?>().FirstOrDefault(tag => string.Equals(tag.ToString(), name, StringComparison.Ordinal));

    /// <summary>The tags of a listing's <c>tags</c> array, deduplicated and in list order; a missing or wrong-typed value is none, an unknown or wrong-case name is dropped.</summary>
    public static IReadOnlyList<StoreTag> FromJson(JsonElement? tags)
    {
        if (tags is not { ValueKind: JsonValueKind.Array } array)
        {
            return [];
        }

        var found = new HashSet<StoreTag>();
        foreach (JsonElement element in array.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String))
        {
            if (Parse(element.GetString()) is { } tag)
            {
                found.Add(tag);
            }
        }

        return found.Order().ToList();
    }

    /// <summary>The tags a card shows: the first <see cref="MaxBadges"/> in list order.</summary>
    public static IReadOnlyList<StoreTag> Badges(IEnumerable<StoreTag> tags) => tags.Distinct().Order().Take(MaxBadges).ToList();

    /// <summary>Books with at least one tag first, then the given order inside each group (stable).</summary>
    public static IReadOnlyList<T> TaggedFirst<T>(IEnumerable<T> items, Func<T, IReadOnlyList<StoreTag>> tagsOf)
    {
        List<T> all = items.ToList();
        return all.Where(i => tagsOf(i).Count > 0).Concat(all.Where(i => tagsOf(i).Count == 0)).ToList();
    }
}
