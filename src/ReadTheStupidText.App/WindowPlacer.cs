using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using ReadTheStupidText.Application.Input;
using ReadTheStupidText.Application.Usage;
using Windows.Graphics;

namespace ReadTheStupidText_App;

/// <summary>
/// The one placement routine for the tray panel and the wait flyout (Slice 170): pick the target point (the saved position,
/// else the primary work area's bottom-right, where the tray is), find that point's monitor, read that monitor's DPI, size
/// the CLIENT area for it, clamp to that work area, and move only when the position actually changes.
/// </summary>
internal static class WindowPlacer
{
    private const int EffectiveDpi = 0; // MDT_EFFECTIVE_DPI
    private const uint DefaultDpi = 96;

    /// <summary>Slice 273 part D: where a placement that fell back is reported (once per session per kind, by the sink's own tally); set at start-up, a no-op until then.</summary>
    public static IFallbackSink Fallbacks { get; set; } = NoFallbackSink.Instance;

    /// <summary>Sizes and places <paramref name="window"/>; <paramref name="saved"/> is the remembered top-left, if any.</summary>
    public static void Place(AppWindow window, double logicalWidth, double logicalHeight, double designWidth, int logicalMargin, PointInt32? saved)
    {
        PointInt32 target = saved ?? CornerOf(DisplayArea.Primary.WorkArea);
        DisplayArea area = DisplayArea.GetFromPoint(target, DisplayAreaFallback.Nearest);
        double scale = PanelPlacement.ScaleForDpi(MonitorDpi(area));
        (int clientW, int clientH) = PanelPlacement.DeviceSize(logicalWidth, logicalHeight, designWidth, scale);
        window.ResizeClient(new SizeInt32(clientW, clientH));
        PointInt32 placed = Target(window, saved, area.WorkArea, (int)(logicalMargin * scale));
        if (saved is { } wanted && !Contains(area.WorkArea, wanted))
        {
            Fallbacks.Report(FallbackArea.Window, FallbackReason.SavedPositionMoved); // Slice 273 part D: the remembered place is off the work area, so the panel is moved back onto it
        }

        MoveIfChanged(window, placed);

        // Crossing to a monitor with another DPI can make Windows rescale the window; size it again for that monitor.
        if (window.ClientSize.Width != clientW || window.ClientSize.Height != clientH)
        {
            window.ResizeClient(new SizeInt32(clientW, clientH));
            MoveIfChanged(window, Target(window, saved, area.WorkArea, (int)(logicalMargin * scale)));
        }
    }

    // A saved point outside every display's work area (a monitor was unplugged) is the only real "moved" case; clamping a point that is on a display is ordinary.
    private static bool Contains(RectInt32 work, PointInt32 point) =>
        point.X >= work.X && point.X < work.X + work.Width && point.Y >= work.Y && point.Y < work.Y + work.Height;

    private static PointInt32 CornerOf(RectInt32 work) => new(work.X + work.Width - 1, work.Y + work.Height - 1);

    private static PointInt32 Target(AppWindow window, PointInt32? saved, RectInt32 work, int margin)
    {
        var area = new WorkArea(work.X, work.Y, work.Width, work.Height);
        int width = window.Size.Width;
        int height = window.Size.Height;
        (int x, int y) = saved is { } point
            ? PanelPlacement.ClampToWorkArea(point.X, point.Y, width, height, area)
            : PanelPlacement.CornerPin(width, height, margin, area);
        return new PointInt32(x, y);
    }

    private static void MoveIfChanged(AppWindow window, PointInt32 target)
    {
        if (window.Position.X != target.X || window.Position.Y != target.Y)
        {
            window.Move(target);
        }
    }

    private static uint MonitorDpi(DisplayArea area)
    {
        IntPtr monitor = Win32Interop.GetMonitorFromDisplayId(area.DisplayId);
        if (GetDpiForMonitor(monitor, EffectiveDpi, out uint dpiX, out _) == 0 && dpiX != 0)
        {
            return dpiX;
        }

        Fallbacks.Report(FallbackArea.Window, FallbackReason.DpiDefaulted); // Slice 273 part D: the panel is sized for 96 dpi
        return DefaultDpi;
    }

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
}
