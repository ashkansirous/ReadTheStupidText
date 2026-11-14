namespace DinoReader.Core;

/// <summary>The pastel page colours of generated pages (Dino Reader v2 mock screens 3, 5 and 14), cycled by page number.</summary>
public static class PagePalette
{
    public static IReadOnlyList<string> Colours { get; } = ["#FFE9B0", "#DDEBFF", "#DFF5E3"];

    public static string ColourAt(int pageIndex) => Colours[((pageIndex % Colours.Count) + Colours.Count) % Colours.Count];
}
