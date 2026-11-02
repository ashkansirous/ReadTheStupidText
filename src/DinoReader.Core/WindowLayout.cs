namespace DinoReader.Core;

/// <summary>Which layout family a desktop window's content area gets (Slice 235, Batch 46 mock).</summary>
public enum WindowShape
{
    /// <summary>Narrower than 600 and taller than wide: the phone portrait layouts.</summary>
    PhonePortrait,

    /// <summary>Wide but under 960: the phone landscape rules (Batch 44), with a grid shelf.</summary>
    PhoneLandscape,

    /// <summary>Taller than wide and at least 600 wide: the tablet upright rules (Batch 45).</summary>
    TabletUpright,

    /// <summary>At least 960 wide and not taller than wide: the tablet landscape rules.</summary>
    TabletLandscape,
}

/// <summary>
/// Picks the layout family from the size of a window's content area (the window minus its 32 px title bar), in dp.
/// Pure and not called by any page yet (Slice 235 part B wires it in). Windows head only; phones keep their own rules.
/// </summary>
public static class WindowLayout
{
    public const int TitleBarHeight = 32;
    public const int MinimumWindowWidth = 800;
    public const int MinimumWindowHeight = 560;
    public const int DefaultWindowWidth = 1100;
    public const int DefaultWindowHeight = 760;
    public const int MaximumContentWidth = 1600;

    private const double UprightMinimumWidth = 600;
    private const double LandscapeMinimumWidth = 960;

    public static WindowShape For(double width, double height)
    {
        if (height > width)
        {
            return width >= UprightMinimumWidth ? WindowShape.TabletUpright : WindowShape.PhonePortrait;
        }

        return width >= LandscapeMinimumWidth ? WindowShape.TabletLandscape : WindowShape.PhoneLandscape;
    }

    /// <summary>The width the content takes: capped, then centred by the page while the cream runs edge to edge.</summary>
    public static double ContentWidth(double windowWidth) => Math.Min(windowWidth, MaximumContentWidth);

    /// <summary>The content area of a window of the given size.</summary>
    public static double ContentHeight(double windowHeight) => Math.Max(0, windowHeight - TitleBarHeight);
}
