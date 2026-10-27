namespace ReadTheStupidText.Application.Input;

/// <summary>A screen work area in pixels.</summary>
public readonly record struct WorkArea(int X, int Y, int Width, int Height);

/// <summary>Where the control panel may sit (Slice 164).</summary>
public static class PanelPlacement
{
    /// <summary>Keeps a point within the work area so a window of the given size stays fully
    /// on-screen (after the panel grew, or a monitor was removed or rearranged).</summary>
    public static (int X, int Y) ClampToWorkArea(int x, int y, int width, int height, WorkArea work)
    {
        int maxX = Math.Max(work.X, work.X + work.Width - width);
        int maxY = Math.Max(work.Y, work.Y + work.Height - height);
        return (Math.Clamp(x, work.X, maxX), Math.Clamp(y, work.Y, maxY));
    }
}
