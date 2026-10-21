namespace DinoReader.Core;

/// <summary>The flat colours of a generated cover (Dino Reader v2 mock, gallery).</summary>
public static class CoverPalette
{
    public static IReadOnlyList<string> Colours { get; } = ["#7FD0FF", "#FFC24D", "#B39CFF", "#FF9FB8"];

    /// <summary>The colour for any index, wrapping round the palette (negative indexes too).</summary>
    public static string ColourAt(int index) =>
        Colours[((index % Colours.Count) + Colours.Count) % Colours.Count];
}
