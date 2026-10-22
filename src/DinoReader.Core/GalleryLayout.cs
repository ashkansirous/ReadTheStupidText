namespace DinoReader.Core;

/// <summary>What one gallery grid slot holds.</summary>
public enum GalleryEntryKind
{
    AddStory,
    Story,
}

/// <summary>One gallery grid slot: the Add tile, or a story.</summary>
public sealed record GalleryEntry(GalleryEntryKind Kind, Story? Story = null)
{
    public static readonly GalleryEntry Add = new(GalleryEntryKind.AddStory);
}

/// <summary>Orders the gallery grid: the Add tile first in grown-up mode only, then the stories in the given order.</summary>
public static class GalleryLayout
{
    public static IReadOnlyList<GalleryEntry> Build(IReadOnlyList<Story> stories, bool grownUp)
    {
        var entries = new List<GalleryEntry>(stories.Count + 1);
        if (grownUp)
        {
            entries.Add(GalleryEntry.Add);
        }

        entries.AddRange(stories.Select(s => new GalleryEntry(GalleryEntryKind.Story, s)));
        return entries;
    }

    /// <summary>The edit overlay shows on the tiles in grown-up mode only.</summary>
    public static bool ShowsOverlay(bool grownUp) => grownUp;
}
