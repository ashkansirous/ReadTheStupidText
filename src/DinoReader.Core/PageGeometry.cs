namespace DinoReader.Core;

/// <summary>The size of a generated page and how its text is set, all in the same unit (dp).
/// <see cref="HighlightOutset"/> is how far a sentence's box reaches past the text on each side.</summary>
public readonly record struct PageGeometry(
    double Width,
    double Height,
    double PaddingX,
    double PaddingY,
    double LineHeight,
    double LineGap,
    double HighlightOutset)
{
    public double TextWidth => Width - 2 * PaddingX;

    public int LinesPerPage =>
        Math.Max(1, (int)Math.Floor((Height - 2 * PaddingY + LineGap) / (LineHeight + LineGap)));
}
