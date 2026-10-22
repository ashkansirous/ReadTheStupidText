namespace DinoReader.Core;

/// <summary>The pure list operations behind the wizard's page grid (Slice 78): move, remove and insert a page, and where a page
/// cover index ends up after each, so the cover stays on the same picture.</summary>
public static class PageOrder
{
    /// <summary>Moves the item at <paramref name="from"/> so it ends up at <paramref name="to"/>; an index out of range does nothing.</summary>
    public static void Move<T>(IList<T> items, int from, int to)
    {
        if (!InRange(items.Count, from) || !InRange(items.Count, to) || from == to)
        {
            return;
        }

        T item = items[from];
        items.RemoveAt(from);
        items.Insert(to, item);
    }

    public static void Remove<T>(IList<T> items, int index)
    {
        if (InRange(items.Count, index))
        {
            items.RemoveAt(index);
        }
    }

    /// <summary>Inserts at <paramref name="at"/>, clamped into 0..Count (so an out-of-range index appends).</summary>
    public static void InsertAt<T>(IList<T> items, int at, T item) => items.Insert(Math.Clamp(at, 0, items.Count), item);

    /// <summary>Where the cover page is after the page at <paramref name="from"/> moved to <paramref name="to"/>.</summary>
    public static int? CoverAfterMove(int? cover, int from, int to)
    {
        if (cover is not { } c || from == to)
        {
            return cover;
        }

        if (c == from)
        {
            return to;
        }

        if (from < c && to >= c)
        {
            return c - 1;
        }

        return from > c && to <= c ? c + 1 : c;
    }

    /// <summary>Where the cover page is after the page at <paramref name="removed"/> went; null when it was the cover itself.</summary>
    public static int? CoverAfterRemove(int? cover, int removed)
    {
        if (cover is not { } c || c == removed)
        {
            return null;
        }

        return removed < c ? c - 1 : c;
    }

    /// <summary>Where the cover page is after a page was inserted at <paramref name="at"/>.</summary>
    public static int? CoverAfterInsert(int? cover, int at) => cover is { } c && at <= c ? c + 1 : cover;

    private static bool InRange(int count, int index) => index >= 0 && index < count;
}
