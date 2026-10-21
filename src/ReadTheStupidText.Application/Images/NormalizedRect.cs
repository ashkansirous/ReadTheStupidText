namespace ReadTheStupidText.Application.Images;

/// <summary>
/// A rectangle on a page or image in fractions of its size (all values 0..1; the origin is the
/// top-left corner), so a box stays valid whatever resolution the page image is shown at (Slice 63).
/// </summary>
public readonly record struct NormalizedRect(double Left, double Top, double Width, double Height)
{
    /// <summary>
    /// Builds a rect from a box in the page's own units (pixels or points, origin top-left),
    /// dividing by the page size and clamping the result into 0..1. An empty page gives an empty rect.
    /// </summary>
    public static NormalizedRect FromBox(
        double left, double top, double width, double height, double pageWidth, double pageHeight)
    {
        if (pageWidth <= 0 || pageHeight <= 0)
        {
            return default;
        }

        double l = Clamp(left / pageWidth);
        double t = Clamp(top / pageHeight);
        double r = Clamp((left + width) / pageWidth);
        double b = Clamp((top + height) / pageHeight);
        return new NormalizedRect(l, t, r - l, b - t);
    }

    /// <summary>The smallest rect covering both this and <paramref name="other"/>.</summary>
    public NormalizedRect Union(NormalizedRect other)
    {
        double l = Math.Min(Left, other.Left);
        double t = Math.Min(Top, other.Top);
        double r = Math.Max(Left + Width, other.Left + other.Width);
        double b = Math.Max(Top + Height, other.Top + other.Height);
        return new NormalizedRect(l, t, r - l, b - t);
    }

    private static double Clamp(double value) => Math.Clamp(value, 0d, 1d);
}
