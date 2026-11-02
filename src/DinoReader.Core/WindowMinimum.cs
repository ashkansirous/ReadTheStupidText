namespace DinoReader.Core;

/// <summary>
/// Keeps a window at or above its minimum size by construction (Slice 235 part A). The presenter's preferred minimum only
/// bounds a user drag; a programmatic resize (UI Automation's TransformPattern.Resize) can still go below it.
/// Pure: pixel sizes in, the size to resize to out.
/// </summary>
public static class WindowMinimum
{
    /// <summary>The size the window must be resized to, or null when it already meets the minimum.</summary>
    public static (int Width, int Height)? Enforce((int Width, int Height) size, (int Width, int Height) minimum)
    {
        int width = Math.Max(size.Width, minimum.Width);
        int height = Math.Max(size.Height, minimum.Height);
        bool changed = width != size.Width || height != size.Height;
        return changed ? (width, height) : null;
    }
}
