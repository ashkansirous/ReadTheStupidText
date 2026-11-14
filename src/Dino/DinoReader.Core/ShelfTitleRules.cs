namespace DinoReader.Core;

/// <summary>How much of the shelf's title the header shows (frame K1-de): the title is the first thing to give way, the Grown-ups pill and the flag never do.</summary>
public enum ShelfTitle
{
    Full,
    Short,
    Hidden,
}

/// <summary>
/// The kids' shelf of the 2026-11-08 design (Slice 325 part C, grown-up mock frames K1-K5, KL1, KT1, KT2, KW1) as pure rules: the same chrome kind and rail widths as the corner
/// (<see cref="CornerLayout.ChromeOf"/>), the title give-way, the columns and the header, rail and cover sizes. Sizes are in dp.
/// </summary>
public static class ShelfTitleRules
{
    public const int PhonePortraitColumns = 2;
    public const int TabletPortraitColumns = 3;
    public const int TabletLandscapeColumns = 4;
    public const int PhoneLandscapeMaxColumns = 4;
    public const int WindowsMinColumns = 4;
    public const int MaxColumns = 6;

    private const double PhoneLandscapeShelfPitch = 110;
    private const double WindowsShelfPitch = 190;

    /// <summary>
    /// The title that fits <paramref name="availableWidth"/>: the full form ("My stories") if it does, else the short one ("Stories"), else none (the Dino icon stands alone).
    /// Widths are measured by the page in the title's own font; this is the one rule for all three.
    /// </summary>
    public static ShelfTitle Fit(double availableWidth, double fullWidth, double shortWidth)
    {
        if (fullWidth <= availableWidth)
        {
            return ShelfTitle.Full;
        }

        return shortWidth <= availableWidth ? ShelfTitle.Short : ShelfTitle.Hidden;
    }

    /// <summary>The width left for the title in a header row: the row less its sides, the icon, the flag button (0 when not shown), the pill and the gaps between the pieces (one per piece after the first).</summary>
    public static double AvailableForTitle(double rowWidth, double sides, double icon, double flagButton, double pill, double gap)
    {
        int pieces = flagButton > 0 ? 4 : 3; // icon, title, [flag], pill
        return Math.Max(0, rowWidth - sides - icon - flagButton - pill - ((pieces - 1) * gap));
    }

    /// <summary>The columns of the shelf (decision 3): phone portrait 2, phone landscape up to 4, tablet portrait 3, tablet landscape 4, Windows 4 and more on a wide window.</summary>
    public static int Columns(ChromeShape shape, double width)
    {
        double room = Math.Max(0, width - shape.RailWidth - SizesOf(shape).Content.Left - SizesOf(shape).Content.Right);
        double gap = SizesOf(shape).GapX;
        return shape.Family switch
        {
            ChromeFamily.Phone => PhonePortraitColumns,
            ChromeFamily.PhoneLandscape => CornerLayout.Fit(room, gap, PhoneLandscapeShelfPitch, CornerLayout.MinColumns, PhoneLandscapeMaxColumns),
            ChromeFamily.TabletPortrait => TabletPortraitColumns,
            ChromeFamily.TabletLandscape => TabletLandscapeColumns,
            _ => CornerLayout.Fit(room, gap, WindowsShelfPitch, WindowsMinColumns, MaxColumns),
        };
    }

    /// <summary>The shelf list's margins, columns and gaps inside its own column (the rail is not part of it): the sides and top of the content padding, no top under a bar.</summary>
    public static WindowShapeSeam.ShelfGeometry Geometry(ChromeShape shape, int columns)
    {
        ShelfSizes sizes = SizesOf(shape);
        return new WindowShapeSeam.ShelfGeometry(sizes.Content.Left, sizes.Content.Top, sizes.Content.Right, columns, sizes.GapX, sizes.GapY);
    }

    public static ShelfSizes SizesOf(ChromeShape shape) => shape.Family switch
    {
        ChromeFamily.Phone => BarSizes(shape.Width),
        ChromeFamily.PhoneLandscape => PhoneLandscapeSizes(),
        ChromeFamily.TabletPortrait => TabletSizes(CornerLayout.TabletPortraitRail, new Edges(26, 40, 18, 32), new Edges(28, 40, 32, 0)),
        ChromeFamily.TabletLandscape => TabletSizes(CornerLayout.TabletLandscapeRail, new Edges(32, 36, 20, 32), new Edges(32, 36, 40, 0)),
        _ => WindowsSizes(),
    };

    // Frames K1 to K4: the cream header (icon, title, flag, pill) and the 2-column grid.
    private static ShelfSizes BarSizes(PhoneWidth width) => width switch
    {
        PhoneWidth.Compact => Bar(new Edges(16, 10, 12, 14), 10, 40, 11, 22, 44, 26, 44, 11, 14, new Edges(14, 0, 14, 0), 12, 14, 14),
        PhoneWidth.Regular => Bar(new Edges(18, 10, 12, 16), 10, 44, 12, 22, 44, 26, 44, 11, 14, new Edges(16, 0, 16, 0), 14, 16, 14.5),
        PhoneWidth.Medium => Bar(new Edges(18, 10, 12, 16), 10, 44, 12, 22, 48, 30, 48, 14, 18, new Edges(16, 0, 16, 0), 14, 16, 14.5),
        _ => Bar(new Edges(22, 10, 16, 18), 10, 48, 13, 24, 48, 30, 48, 14, 18, new Edges(20, 0, 20, 0), 16, 18, 15),
    };

    private static ShelfSizes Bar(
        Edges header, double headerGap, double icon, double iconRadius, double title, double flagButton, double flag, double pillHeight, double pillLeft, double pillRight,
        Edges content, double gapX, double gapY, double tileTitle) =>
        new()
        {
            Header = header, HeaderGap = headerGap, Icon = icon, IconRadius = iconRadius, Title = title, FlagButton = flagButton, Flag = flag,
            PillHeight = pillHeight, PillLabel = 15, PillPadLeft = pillLeft, PillPadRight = pillRight, PillGap = 6, PillIcon = 20, PillRadius = pillHeight / 2,
            Content = content, GapX = gapX, GapY = gapY, CoverRadius = 16, ShadowOffset = 4, TileTitle = tileTitle, TileGap = 7,
        };

    // Frame KL1: the cream rail 250 with the name row and a full-width Grown-ups pill, four columns.
    private static ShelfSizes PhoneLandscapeSizes() => new()
    {
        RailWidth = CornerLayout.PhoneLandscapeRail, RailPadding = new Edges(14, 18, 14, 18), RailGap = 10, Icon = 28, IconRadius = 8, Title = 16, FlagButton = 44, Flag = 26,
        PillHeight = 44, PillLabel = 15, PillIcon = 20, PillGap = 6, PillRadius = 22,
        Content = new Edges(22, 18, 50, 0), GapX = 14, GapY = 16, CoverRadius = 16, ShadowOffset = 4, TileTitle = 14, TileGap = 7,
    };

    // Frames KT1 and KT2: the rail 260 / 290, covers of radius 18 with a 5 dp shadow.
    private static ShelfSizes TabletSizes(double rail, Edges padding, Edges content) => new()
    {
        RailWidth = rail, RailPadding = padding, RailGap = 12, Icon = 40, IconRadius = 12, Title = 20, FlagButton = 48, Flag = 30,
        PillHeight = 56, PillLabel = 16, PillIcon = 22, PillGap = 8, PillRadius = 28,
        Content = content, GapX = 18, GapY = 28, CoverRadius = 18, ShadowOffset = 5, TileTitle = 17, TileGap = 7,
    };

    // Frame KW1: the rail 280, covers 16 at 4 columns.
    private static ShelfSizes WindowsSizes() => new()
    {
        RailWidth = CornerLayout.WindowsRail, RailPadding = new Edges(28, 14, 20, 24), RailGap = 10, Icon = 40, IconRadius = 12, Title = 21, FlagButton = 48, Flag = 30,
        PillHeight = 52, PillLabel = 15.5, PillIcon = 22, PillGap = 8, PillRadius = 26,
        Content = new Edges(32, 20, 40, 0), GapX = 18, GapY = 24, CoverRadius = 16, ShadowOffset = 4, TileTitle = 16, TileGap = 7,
    };
}

/// <summary>The sizes of the kids' shelf's chrome and grid for one <see cref="ChromeShape"/> (the fields of the other chrome kind are 0). Dp.</summary>
public sealed record ShelfSizes
{
    public Edges Header { get; init; }
    public double HeaderGap { get; init; }
    public double Icon { get; init; }
    public double IconRadius { get; init; }
    public double Title { get; init; }
    public double FlagButton { get; init; }
    public double Flag { get; init; }

    public double PillHeight { get; init; }
    public double PillLabel { get; init; }
    public double PillPadLeft { get; init; }
    public double PillPadRight { get; init; }
    public double PillGap { get; init; }
    public double PillIcon { get; init; }
    public double PillRadius { get; init; }

    public double RailWidth { get; init; }
    public Edges RailPadding { get; init; }
    public double RailGap { get; init; }

    public Edges Content { get; init; }
    public double GapX { get; init; }
    public double GapY { get; init; }
    public double CoverRadius { get; init; }
    public double ShadowOffset { get; init; }
    public double TileTitle { get; init; }
    public double TileGap { get; init; }
}
