using System.Globalization;

namespace DinoReader.Core;

/// <summary>An integer rectangle in physical pixels (a window or a display's work area).</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    /// <summary>The overlap with <paramref name="other"/>; empty (zero size) when they do not touch.</summary>
    public PixelRect Intersect(PixelRect other)
    {
        int left = Math.Max(X, other.X);
        int top = Math.Max(Y, other.Y);
        int right = Math.Min(Right, other.Right);
        int bottom = Math.Min(Bottom, other.Bottom);
        return right > left && bottom > top ? new PixelRect(left, top, right - left, bottom - top) : new PixelRect(left, top, 0, 0);
    }
}

/// <summary>The window as last closed: its restored rectangle (not the maximised one) and whether it was maximised.</summary>
public sealed record SavedWindow(PixelRect Bounds, bool Maximised)
{
    private const char Separator = ',';
    private const int FieldCount = 5;
    private const int MaxPixelValue = 100_000;

    // Invariant, to match TryParse: a Unicode minus from the current culture (sv-SE) would not read back.
    public string ToText() => string.Join(
        Separator,
        new[] { Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height, Maximised ? 1 : 0 }.Select(n => n.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Reads <see cref="ToText"/>'s output; null for anything else (empty, damaged, a zero size).</summary>
    public static SavedWindow? TryParse(string? text)
    {
        string[] parts = (text ?? string.Empty).Split(Separator);
        if (parts.Length != FieldCount)
        {
            return null;
        }

        var numbers = new int[FieldCount];
        for (int i = 0; i < FieldCount; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return null;
            }
        }

        bool sized = numbers[2] > 0 && numbers[3] > 0 && InRange(numbers);
        return sized ? new SavedWindow(new PixelRect(numbers[0], numbers[1], numbers[2], numbers[3]), numbers[4] != 0) : null;
    }

    // Text with absurd numbers (damaged, or hand-edited) reads as nothing saved, so no later sum can overflow.
    private static bool InRange(int[] numbers) =>
        numbers.Take(4).All(n => Math.Abs((long)n) <= MaxPixelValue);
}

/// <summary>Where and how big a window should open.</summary>
public sealed record WindowOpening(PixelRect Bounds, bool Maximised);

/// <summary>
/// Works out the rectangle Dino Reader's Windows window opens with from the saved one and the displays' work areas (Slice 235 part A).
/// All values are physical pixels; the head multiplies the dp defaults by the display scale first.
/// </summary>
public static class WindowPlacement
{
    /// <summary>A saved window counts as on a display when this much of its title-bar strip (width, height) is inside a work area.</summary>
    public const int MinimumVisibleWidth = 100;
    public const int MinimumVisibleHeight = 16;

    /// <summary>The height, from the window's top edge, of the strip that stands for the title bar.</summary>
    public const int TitleBarStripHeight = 32;

    /// <param name="saved">The last closed window, or null on the first run.</param>
    /// <param name="workAreas">Every display's work area; the first is the main display's.</param>
    /// <param name="defaultSize">The size to open with when nothing is saved (already scaled to pixels).</param>
    /// <param name="minimumSize">The smallest the window may be (already scaled to pixels).</param>
    public static WindowOpening Resolve(SavedWindow? saved, IReadOnlyList<PixelRect> workAreas, (int Width, int Height) defaultSize, (int Width, int Height) minimumSize)
    {
        if (workAreas.Count == 0)
        {
            PixelRect lone = saved?.Bounds ?? new PixelRect(0, 0, defaultSize.Width, defaultSize.Height);
            return new WindowOpening(lone, saved?.Maximised ?? false);
        }

        PixelRect main = workAreas[0];
        if (saved is null)
        {
            return new WindowOpening(CentredOn(main, Clamp(defaultSize, minimumSize, main)), false);
        }

        PixelRect? home = HomeDisplay(saved.Bounds, workAreas);
        if (home is { } area)
        {
            var fitted = Clamp((saved.Bounds.Width, saved.Bounds.Height), minimumSize, area);
            int x = Math.Max(area.X, Math.Min(saved.Bounds.X, area.Right - fitted.Width));
            int y = Math.Max(area.Y, Math.Min(saved.Bounds.Y, area.Bottom - fitted.Height));
            return new WindowOpening(new PixelRect(x, y, fitted.Width, fitted.Height), saved.Maximised);
        }

        var shrunk = Clamp((saved.Bounds.Width, saved.Bounds.Height), minimumSize, main);
        return new WindowOpening(CentredOn(main, shrunk), saved.Maximised);
    }

    /// <summary>True when a strip of the window's title bar is inside some work area, so the user can still grab it.</summary>
    public static bool IsOnAnyDisplay(PixelRect window, IReadOnlyList<PixelRect> workAreas) => HomeDisplay(window, workAreas) is not null;

    // The work area holding the most of the window's title-bar strip, or null when no area shows enough of it.
    private static PixelRect? HomeDisplay(PixelRect window, IReadOnlyList<PixelRect> workAreas)
    {
        var strip = new PixelRect(window.X, window.Y, window.Width, TitleBarStripHeight);
        PixelRect? best = null;
        int bestWidth = 0;
        foreach (PixelRect area in workAreas)
        {
            PixelRect overlap = strip.Intersect(area);
            bool grabbable = overlap.Width >= MinimumVisibleWidth && overlap.Height >= MinimumVisibleHeight;
            if (grabbable && overlap.Width > bestWidth)
            {
                best = area;
                bestWidth = overlap.Width;
            }
        }

        return best;
    }

    // At least the minimum, at most the display; the minimum wins on a display smaller than it.
    private static (int Width, int Height) Clamp((int Width, int Height) size, (int Width, int Height) minimum, PixelRect display) =>
        (Math.Max(minimum.Width, Math.Min(size.Width, display.Width)), Math.Max(minimum.Height, Math.Min(size.Height, display.Height)));

    private static PixelRect CentredOn(PixelRect display, (int Width, int Height) size) =>
        new(display.X + ((display.Width - size.Width) / 2), display.Y + ((display.Height - size.Height) / 2), size.Width, size.Height);
}
