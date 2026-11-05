using System.Text.Json;

namespace DinoReader.Core;

/// <summary>
/// The order a grown-up gave the shelf (Slice 264): a list of book ids kept in <c>shelf-order.json</c> in the library root. Pure rules only; the file is read and
/// written by <see cref="FileStoryLibrary"/>. A book missing from the list (a new store, own or restored book) comes first, then the listed ids in their order;
/// listed ids that are no longer on the shelf are dropped. No list, or one that does not parse, leaves today's order.
/// </summary>
public static class ShelfOrder
{
    public const string FileName = "shelf-order.json";

    /// <summary>The books in shelf order: unlisted ones first (in the order given), then the listed ones in the stored order.</summary>
    public static List<T> Apply<T>(IReadOnlyList<T> items, Func<T, string> idOf, IReadOnlyList<string> stored)
    {
        if (stored.Count == 0)
        {
            return [.. items];
        }

        var position = new Dictionary<string, int>();
        foreach (string id in stored)
        {
            position.TryAdd(id, position.Count);
        }

        List<T> unlisted = [.. items.Where(item => !position.ContainsKey(idOf(item)))];
        IEnumerable<T> listed = items.Where(item => position.ContainsKey(idOf(item))).OrderBy(item => position[idOf(item)]);
        return [.. unlisted, .. listed];
    }

    /// <summary>The order with <paramref name="id"/> taken out and put just before <paramref name="targetId"/>; a null or unknown target puts it last. 1..6 with 5 before 3 gives 1,2,5,3,4,6.</summary>
    public static List<string> MoveBefore(IReadOnlyList<string> order, string id, string? targetId)
    {
        if (targetId == id)
        {
            return [.. order];
        }

        List<string> result = [.. order.Where(other => other != id)];
        int index = targetId is null ? -1 : result.IndexOf(targetId);
        if (index < 0)
        {
            result.Add(id);
        }
        else
        {
            result.Insert(index, id);
        }

        return result;
    }

    /// <summary>The order with <paramref name="id"/> taken out and put at <paramref name="slot"/> (0-based, clamped), as a drop or an arrow key does. 1..4 with 1 at slot 3 gives 2,3,4,1.</summary>
    public static List<string> MoveToSlot(IReadOnlyList<string> order, string id, int slot)
    {
        List<string> result = [.. order.Where(other => other != id)];
        result.Insert(Math.Clamp(slot, 0, result.Count), id);
        return result;
    }

    /// <summary>
    /// The order to store after a reorder of the books <paramref name="shown"/>: stored ids that were not shown (on disk but not on the screen) stay, each right after the id that preceded it
    /// in the stored list (first if none did), so they keep their old relative place instead of coming back at the front.
    /// </summary>
    public static List<string> MergeShown(IReadOnlyList<string> stored, IReadOnlyList<string> shown)
    {
        List<string> result = [.. shown];
        var shownSet = new HashSet<string>(shown);
        for (int i = 0; i < stored.Count; i++)
        {
            if (shownSet.Contains(stored[i]) || result.Contains(stored[i]))
            {
                continue;
            }

            int at = i == 0 ? 0 : result.IndexOf(stored[i - 1]) + 1; // the previous stored id is already in result (shown, or kept just before)
            result.Insert(Math.Max(at, 0), stored[i]);
        }

        return result;
    }

    /// <summary>The stored ids without <paramref name="id"/>, so a book removed from the shelf and added again does not return to its old slot.</summary>
    public static List<string> Without(IReadOnlyList<string> stored, string id) => [.. stored.Where(other => other != id)];

    /// <summary>The ids in a stored file; an empty list when the text is missing or is not a JSON list of strings.</summary>
    public static IReadOnlyList<string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)?.Where(id => !string.IsNullOrEmpty(id)).ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Serialize(IReadOnlyList<string> ids) => JsonSerializer.Serialize(ids);
}
