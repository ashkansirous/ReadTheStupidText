namespace DinoReader.Core;

/// <summary>Whether a book cover wears the "..." menu (Slice 418 decision 10): only while the shelf is unlocked, and never on a book a lapsed subscription locked (Slice 398).</summary>
public static class TileMenuRule
{
    public static bool ShowMore(bool unlocked, bool lapsed) => unlocked && !lapsed;
}
