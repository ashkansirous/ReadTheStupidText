namespace DinoReader.Core;

/// <summary>One gallery grid slot: a story. (The Add tile left with Slice 172, and its entry kind with Slice 325.)</summary>
public sealed record GalleryEntry(Story Story);

/// <summary>Orders the gallery grid: the stories in the given order.</summary>
public static class GalleryLayout
{
    public static IReadOnlyList<GalleryEntry> Build(IReadOnlyList<Story> stories) =>
        stories.Select(s => new GalleryEntry(s)).ToList();

    /// <summary>The edit overlay shows on the tiles in grown-up mode only.</summary>
    public static bool ShowsOverlay(bool grownUp) => grownUp;
}
