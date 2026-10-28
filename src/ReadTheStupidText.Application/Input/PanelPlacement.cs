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

    /// <summary>The scale factor for a monitor DPI (96 = 1.0); a missing or zero DPI counts as 100%.</summary>
    public static double ScaleForDpi(uint dpi) => dpi == 0 ? 1.0 : dpi / 96.0;

    /// <summary>The device-pixel client size for a logical size: each side rounded up so nothing is clipped.
    /// A width that is not finite or not above zero (e.g. an Auto or removed XAML width is NaN) uses the design width.</summary>
    public static (int Width, int Height) DeviceSize(double logicalWidth, double logicalHeight, double designWidth, double scale)
    {
        double width = double.IsFinite(logicalWidth) && logicalWidth > 0 ? logicalWidth : designWidth;
        double height = double.IsFinite(logicalHeight) && logicalHeight > 0 ? logicalHeight : 1;
        return ((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale));
    }

    /// <summary>The top-left that pins a window of the given size to the bottom-right of the work area, inset by the margin.</summary>
    public static (int X, int Y) CornerPin(int width, int height, int margin, WorkArea work) =>
        ClampToWorkArea(work.X + work.Width - width - margin, work.Y + work.Height - height - margin, width, height, work);
}
