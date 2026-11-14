namespace DinoReader.Core;

/// <summary>A rectangle in page units: 0..1 across the page's width and height, so the same line
/// boxes fit a generated page at any size and a real page picture (Slices 63/64) at any scale.</summary>
public readonly record struct NormalizedRect(double X, double Y, double Width, double Height)
{
    public double Bottom => Y + Height;

    public double Right => X + Width;
}
