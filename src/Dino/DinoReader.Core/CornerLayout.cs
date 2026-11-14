namespace DinoReader.Core;

/// <summary>The three tabs of the grown-ups' corner (Slice 325 part A). Never persisted.</summary>
public enum CornerTab
{
    Books,
    Store,
    Settings,
}

/// <summary>The corner's and the shelf's chrome: a bar (header on top, tabs along the bottom for the corner) under 600 dp wide, a left rail from 600.</summary>
public enum ChromeKind
{
    Bar,
    Rail,
}

/// <summary>Which of the 2026-11-08 mock's size families a window belongs to; the rail width, columns and sizes follow it.</summary>
public enum ChromeFamily
{
    Phone,
    PhoneLandscape,
    TabletPortrait,
    TabletLandscape,
    Windows,
}

/// <summary>The width class of a bar (phone portrait): iPhone 8 (375), iPhone 15 Pro (393), Pixel 10 Pro (412) and iPhone 17 Pro Max (440) have their own sizes in the mock.</summary>
public enum PhoneWidth
{
    Compact,
    Regular,
    Medium,
    Large,
}

/// <summary>Four sides in dp, in the order of a Thickness.</summary>
public readonly record struct Edges(double Left, double Top, double Right, double Bottom);

/// <summary>What chrome a window gets: the kind, the family, the phone width class (bars) and the rail width in dp (0 for a bar).</summary>
public readonly record struct ChromeShape(ChromeKind Kind, ChromeFamily Family, PhoneWidth Width, double RailWidth)
{
    public bool IsRail => Kind == ChromeKind.Rail;

    /// <summary>The rail's coral button says "Back to my child's shelf" (full words fit) on tablets and Windows, "Child's shelf" on a phone held sideways.</summary>
    public bool FullWords => IsRail && Family != ChromeFamily.PhoneLandscape;

    public bool IsTabletPortrait => Family == ChromeFamily.TabletPortrait;
}

/// <summary>
/// The grown-ups' corner of the 2026-11-08 design (Slice 325 part A, grown-up mock frames P1-P5, L1, T1, T2, W1), as pure rules: which chrome a window gets, the sizes of its header, rail and tabs,
/// the Books grid and the cover. The shelf (<see cref="ShelfTitleRules"/>) uses the same <see cref="ChromeShape"/>. Sizes are in dp; the status area and the home-bar inset belong to the platform (safe areas).
/// </summary>
public static class CornerLayout
{
    /// <summary>From this width the chrome is a left rail (decision 2 of the slice); a window held sideways always gets one.</summary>
    public const double RailFromWidth = LandscapeLayout.TabletShortSide;

    public const double PhoneLandscapeRail = 250;
    public const double TabletPortraitRail = 260;
    public const double TabletLandscapeRail = 290;
    public const double WindowsRail = 280;

    private const double CompactBelow = 380;
    private const double RegularBelow = 400;
    private const double MediumBelow = 430;

    public const int PhonePortraitBooks = 3;
    public const int TabletPortraitMaxColumns = 3;
    public const int TabletLandscapeColumns = 4;
    public const int WindowsMinColumns = 4;
    public const int MaxColumns = 6;
    public const int MinColumns = 2;
    public const int PhoneLandscapeMaxBooks = 5;

    /// <summary>The cover width a column needs at least, by family: it picks how many columns a narrow window keeps.</summary>
    private const double PhoneLandscapeBookPitch = 90;
    private const double TabletPortraitBookPitch = 120;
    private const double WindowsBookPitch = 190;

    /// <summary>
    /// The chrome for a window of this size. <paramref name="width"/> and <paramref name="height"/> are the window's, already passed through <see cref="WindowShapeSeam.Effective"/> on Windows.
    /// A rail from 600 dp wide, or whenever the window is held sideways (a 568 x 320 phone has no room for a bottom bar); Windows is always a rail.
    /// </summary>
    public static ChromeShape ChromeOf(double width, double height, bool windows)
    {
        PhoneWidth phone = PhoneWidthOf(width);
        if (windows)
        {
            return new ChromeShape(ChromeKind.Rail, ChromeFamily.Windows, phone, WindowsRail);
        }

        if (width < RailFromWidth && !LandscapeLayout.IsLandscape(width, height))
        {
            return new ChromeShape(ChromeKind.Bar, ChromeFamily.Phone, phone, 0);
        }

        return WindowShapeSeam.DeviceShapeOf(width, height) switch
        {
            WindowShape.TabletUpright => new ChromeShape(ChromeKind.Rail, ChromeFamily.TabletPortrait, phone, TabletPortraitRail),
            WindowShape.TabletLandscape => new ChromeShape(ChromeKind.Rail, ChromeFamily.TabletLandscape, phone, TabletLandscapeRail),
            _ => new ChromeShape(ChromeKind.Rail, ChromeFamily.PhoneLandscape, phone, PhoneLandscapeRail),
        };
    }

    public static PhoneWidth PhoneWidthOf(double width) => width switch
    {
        < CompactBelow => PhoneWidth.Compact,
        < RegularBelow => PhoneWidth.Regular,
        < MediumBelow => PhoneWidth.Medium,
        _ => PhoneWidth.Large,
    };

    /// <summary>Whether the coral button keeps its words: only when the title (as wide as it needs) and the button with its label fit one row. The title never gives way; the label does (frame P1 note).</summary>
    public static bool PillKeepsLabel(double headerWidth, double titleWidth, double pillWithLabelWidth, double gap) =>
        titleWidth + gap + pillWithLabelWidth <= headerWidth;

    /// <summary>Line height multiple of a bar tab label: room for descenders ("g") and for Arabic and Farsi marks above and below the line (Slice 349).</summary>
    public const double TabLabelLineHeight = 1.4;

    /// <summary>The least height of one bar tab (Slice 349). As in the mock (frame P1: the bar's 4 pt padding around a 52 pt row), the padding is the bar's and the row is <see cref="CornerSizes.TabRow"/> (52 or 56) holding the pill, the 2 gap and one label line at <see cref="TabLabelLineHeight"/>, which always fit; only a content taller than the row (a larger system font scale) grows it.</summary>
    public static double TabBarMinHeight(CornerSizes sizes, double labelGap)
    {
        double content = sizes.TabPillHeight + labelGap + Math.Ceiling(sizes.TabLabel * TabLabelLineHeight);
        return sizes.TabBar.Top + sizes.TabBar.Bottom + Math.Max(sizes.TabRow, content);
    }

    /// <summary>The columns of the Books grid (decision 3): phone portrait 3, phone landscape up to 5, tablet portrait up to 3, tablet landscape 4, Windows 4 and more on a wide window.</summary>
    public static int BookColumns(ChromeShape shape, double width)
    {
        double room = ContentWidth(shape, width);
        CornerSizes sizes = SizesOf(shape);
        return shape.Family switch
        {
            ChromeFamily.Phone => PhonePortraitBooks,
            ChromeFamily.PhoneLandscape => Fit(room, sizes.GapX, PhoneLandscapeBookPitch, MinColumns, PhoneLandscapeMaxBooks),
            ChromeFamily.TabletPortrait => Fit(room, sizes.GapX, TabletPortraitBookPitch, MinColumns, TabletPortraitMaxColumns),
            ChromeFamily.TabletLandscape => TabletLandscapeColumns,
            _ => Fit(room, sizes.GapX, WindowsBookPitch, WindowsMinColumns, MaxColumns),
        };
    }

    /// <summary>The width of the content column (everything right of the rail, less its own sides).</summary>
    public static double ContentWidth(ChromeShape shape, double width)
    {
        Edges content = SizesOf(shape).Content;
        return Math.Max(0, width - shape.RailWidth - content.Left - content.Right);
    }

    /// <summary>How many columns of at least <paramref name="pitch"/> wide (the gap included) fit in <paramref name="room"/>, between <paramref name="min"/> and <paramref name="max"/>.</summary>
    public static int Fit(double room, double gap, double pitch, int min, int max) =>
        (int)Math.Clamp(Math.Floor((Math.Max(0, room) + gap) / (pitch + gap)), min, max);

    /// <summary>The shelf list's margins, columns and gaps for the corner's Books tab, inside its own column (the rail is not part of it).</summary>
    public static WindowShapeSeam.ShelfGeometry BooksGeometry(ChromeShape shape, int columns)
    {
        CornerSizes sizes = SizesOf(shape);
        return new WindowShapeSeam.ShelfGeometry(sizes.Content.Left, 0, sizes.Content.Right, columns, sizes.GapX, sizes.GapY);
    }

    public static CornerSizes SizesOf(ChromeShape shape) => shape.Family switch
    {
        ChromeFamily.Phone => BarSizes(shape.Width),
        ChromeFamily.PhoneLandscape => PhoneLandscapeSizes(),
        ChromeFamily.TabletPortrait => TabletSizes(TabletPortraitRail, new Edges(26, 40, 18, 32), new Edges(28, 40, 32, 0), 16, 22),
        ChromeFamily.TabletLandscape => TabletSizes(TabletLandscapeRail, new Edges(32, 36, 20, 32), new Edges(32, 36, 40, 0), 16, 22),
        _ => WindowsSizes(),
    };

    // Frames P1 to P3 and P4: the bar. Header, note and Books grid per width class; the tab bar is 52 high up to 393 (a 52 x 28 pill) and 56 from 412 (56 x 30). Padding: 4 above and below at 375 (no home bar); from 393 6 above and 0 below: the mock's bottom padding is the home-bar inset itself (P2 34, P4 24), which the bar's safe area adds; a phone with no inset matches the mock's own layout.
    private static CornerSizes BarSizes(PhoneWidth width) => width switch
    {
        PhoneWidth.Compact => Bar(width, new Edges(16, 8, 12, 12), 11.5, 22, 44, 15, 12, 16, 7, 13, new Edges(16, 10, 16, 8), 52, 52, 28, 12, 20, new Edges(8, 4, 8, 4), 12, 10, 10, 12, 12, 5, 30, 4),
        PhoneWidth.Regular => Bar(width, new Edges(18, 6, 12, 14), 11.5, 22, 44, 15, 12, 16, 7, 13.5, new Edges(18, 12, 18, 10), 52, 52, 28, 12, 20, new Edges(10, 6, 10, 0), 14, 10, 10, 12, 12.5, 5, 30, 4),
        PhoneWidth.Medium => Bar(width, new Edges(20, 6, 14, 14), 11.5, 22, 48, 15, 14, 18, 7, 13.5, new Edges(20, 12, 20, 10), 56, 56, 30, 12, 20, new Edges(10, 6, 10, 0), 16, 10, 12, 12, 12.5, 5, 30, 4),
        _ => Bar(width, new Edges(22, 6, 16, 16), 12, 24, 48, 16, 14, 18, 8, 14, new Edges(22, 14, 22, 12), 56, 56, 30, 13, 22, new Edges(16, 6, 16, 0), 18, 12, 14, 14, 13.5, 6, 32, 5),
    };

    private static CornerSizes Bar(
        PhoneWidth width, Edges header, double eyebrow, double title, double pillHeight, double pillLabel, double pillLeft, double pillRight, double pillGap,
        double noteSize, Edges note, double tabRow, double tabPillWidth, double tabPillHeight, double tabLabel, double tabIcon, Edges tabBar,
        double side, double gapX, double gapY, double coverRadius, double tileTitle, double tileGap, double dot, double dotInset) =>
        new()
        {
            Header = header, Eyebrow = eyebrow, Title = title, PillHeight = pillHeight, PillLabel = pillLabel, PillPadLeft = pillLeft, PillPadRight = pillRight, PillGap = pillGap,
            NoteSize = noteSize, Note = note, TabRow = tabRow, TabPillWidth = tabPillWidth, TabPillHeight = tabPillHeight, TabLabel = tabLabel, TabIcon = tabIcon, TabBar = tabBar,
            Content = new Edges(side, 0, side, 0), GapX = gapX, GapY = gapY, CoverRadius = coverRadius, TileTitle = tileTitle, TileGap = tileGap, DotSize = dot, DotInset = dotInset,
            PillIcon = 14,
        };

    // Frame L1: the 250 rail with a short coral button, the title row (with "Only grown-ups see this." beside it) above 5 columns.
    private static CornerSizes PhoneLandscapeSizes() => new()
    {
        RailWidth = PhoneLandscapeRail, RailPadding = new Edges(14, 18, 14, 18), RailGap = 8, NameIcon = 18, NameSize = 16, NamePadding = new Edges(4, 0, 4, 4),
        PillHeight = 44, PillLabel = 15, PillIcon = 14, PillGap = 7, DividerMargin = new Edges(0, 6, 0, 2), NoteInRail = false, NoteSize = 12.5,
        TabHeight = 46, TabRadius = 14, TabPadding = 12, TabLabelSize = 15, TabIcon = 20, TabIconGap = 10,
        Content = new Edges(20, 16, 50, 0), ContentTitle = 20, ContentGap = 10, GapX = 10, GapY = 12, CoverRadius = 12, TileTitle = 12, TileGap = 5, DotSize = 30, DotInset = 4,
    };

    // Frames T1 and T2: 260 / 290 rail with the full words, 28 pt title, covers of radius 14.
    private static CornerSizes TabletSizes(double rail, Edges padding, Edges content, double gapX, double gapY) => new()
    {
        RailWidth = rail, RailPadding = padding, RailGap = 12, NameIcon = 26, NameSize = 22, NamePadding = new Edges(4, 0, 4, 6),
        PillHeight = 56, PillLabel = 16, PillIcon = 16, PillGap = 8, DividerMargin = new Edges(0, 8, 0, 4), NoteInRail = true, NoteSize = 14, NotePadding = new Edges(4, 2, 4, 0),
        TabHeight = 56, TabRadius = 16, TabPadding = 16, TabLabelSize = 17, TabIcon = 22, TabIconGap = 12,
        Content = content, ContentTitle = 28, ContentGap = 20, GapX = gapX, GapY = gapY, CoverRadius = 14, TileTitle = 15, TileGap = 7, DotSize = 36, DotInset = 6,
    };

    // Frame W1: the 280 rail with the shortcut hints, 26 pt title.
    private static CornerSizes WindowsSizes() => new()
    {
        RailWidth = WindowsRail, RailPadding = new Edges(28, 14, 20, 24), RailGap = 10, NameIcon = 24, NameSize = 21, NamePadding = new Edges(4, 0, 4, 6),
        PillHeight = 52, PillLabel = 15.5, PillIcon = 16, PillGap = 8, DividerMargin = new Edges(0, 8, 0, 4), NoteInRail = true, NoteSize = 13.5, NotePadding = new Edges(4, 2, 4, 0),
        TabHeight = 48, TabRadius = 14, TabPadding = 14, TabLabelSize = 16, TabIcon = 20, TabIconGap = 12, TabHints = true,
        Content = new Edges(32, 20, 40, 0), ContentTitle = 26, ContentGap = 12, GapX = 18, GapY = 24, CoverRadius = 16, TileTitle = 16, TileGap = 7, DotSize = 36, DotInset = 6,
    };
}

/// <summary>All the sizes of the corner's chrome and Books grid for one <see cref="ChromeShape"/> (the fields of the other chrome kind are 0). Dp.</summary>
public sealed record CornerSizes
{
    // The coral button (both kinds)
    public double PillHeight { get; init; }
    public double PillLabel { get; init; }
    public double PillPadLeft { get; init; }
    public double PillPadRight { get; init; }
    public double PillGap { get; init; }
    public double PillIcon { get; init; }

    // Bar: the ink header, the note, the tab bar
    public Edges Header { get; init; }
    public double Eyebrow { get; init; }
    public double Title { get; init; }
    public double NoteSize { get; init; }
    public Edges Note { get; init; }
    public double TabRow { get; init; }
    public double TabPillWidth { get; init; }
    public double TabPillHeight { get; init; }
    public double TabLabel { get; init; }
    public double TabIcon { get; init; }
    public Edges TabBar { get; init; }

    // Rail
    public double RailWidth { get; init; }
    public Edges RailPadding { get; init; }
    public double RailGap { get; init; }
    public double NameIcon { get; init; }
    public double NameSize { get; init; }
    public Edges NamePadding { get; init; }
    public Edges DividerMargin { get; init; }
    public bool NoteInRail { get; init; }
    public Edges NotePadding { get; init; }
    public double TabHeight { get; init; }
    public double TabRadius { get; init; }
    public double TabPadding { get; init; }
    public double TabLabelSize { get; init; }
    public double TabIconGap { get; init; }
    public bool TabHints { get; init; }

    // Content: the column right of the rail (or the whole width under a bar), its title row, and the Books grid
    public Edges Content { get; init; }
    public double ContentTitle { get; init; }
    public double ContentGap { get; init; }
    public double GapX { get; init; }
    public double GapY { get; init; }
    public double CoverRadius { get; init; }
    public double TileTitle { get; init; }
    public double TileGap { get; init; }
    public double DotSize { get; init; }
    public double DotInset { get; init; }
}
