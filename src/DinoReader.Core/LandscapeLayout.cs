namespace DinoReader.Core;

/// <summary>Phone or tablet, told by the short side of the window (Slice 232).</summary>
public enum DeviceSize
{
    Phone,
    Tablet,
}

/// <summary>Where the reading controls (previous, play, next) go: a row under the text, or a rail beside it.</summary>
public enum ControlsPlacement
{
    Row,
    Rail,
}

/// <summary>Where a page's pinned button goes: the bottom of the page, or the bottom of the side rail.</summary>
public enum PinnedPlacement
{
    BottomBar,
    SideRailBottom,
}

/// <summary>The shelf: one row of covers that scrolls sideways (phone held sideways) or a grid with this many columns.</summary>
public readonly record struct ShelfShape(bool Sideways, double CoverHeight, int Columns);

/// <summary>The grown-ups' corner: a side rail (width in dp, 0 without one) with this many columns of books; Add and Store are full rows on a tablet's rail, a pair of buttons on a phone's.</summary>
public readonly record struct CornerShape(bool Rail, double RailWidth, int Columns, bool FullRowButtons);

/// <summary>Diameters of the three reading buttons, in dp.</summary>
public readonly record struct ControlSizes(double Play, double Skip);

/// <summary>
/// The pure layout rules every landscape screen shares (Slice 232, mock Batch 44): one function per rule, so a page asks instead of
/// carrying its own numbers. Sizes are in dp.
/// </summary>
public static class LandscapeLayout
{
    /// <summary>A window whose short side is under this is a phone.</summary>
    public const double TabletShortSide = 600;

    /// <summary>Below this height the reading controls leave the text for a rail.</summary>
    public const double RailHeightBelow = 480;

    public const double MaxPictureShare = 0.45;
    public const double PhoneListPane = 300;
    public const double TabletListPane = 360;
    public const double ShelfHeaderAndTitles = 120;
    public const double ShelfColumnWidth = 190;
    public const int MinShelfColumns = 2;
    public const int MaxShelfColumns = 6;
    public const double DefaultPictureAspect = 3.0 / 4.0;

    /// <summary>The gap between the picture pane and the text card.</summary>
    public const double SidewaysGap = 6;

    public static readonly ControlSizes RailControls = new(68, 52);
    public static readonly ControlSizes RowControls = new(84, 64);

    public static bool IsLandscape(double width, double height) => width > height;

    public static DeviceSize SizeOf(double width, double height) =>
        Math.Min(width, height) < TabletShortSide ? DeviceSize.Phone : DeviceSize.Tablet;

    /// <summary>Phone held sideways: one sideways row, a cover as tall as the window less the header and the title. Anything else: a grid of 2 to 6 columns.</summary>
    public static ShelfShape ShelfLayout(double width, double height)
    {
        if (IsLandscape(width, height) && SizeOf(width, height) == DeviceSize.Phone)
        {
            return new ShelfShape(true, Math.Max(0, height - ShelfHeaderAndTitles), 0);
        }

        int columns = (int)Math.Clamp(Math.Floor(Math.Max(0, width) / ShelfColumnWidth), MinShelfColumns, MaxShelfColumns);
        return new ShelfShape(false, 0, columns);
    }

    /// <summary>The picture's pane: the picture fitted to the full height (width = height x its width-to-height ratio), never over 45% of the window. A ratio of 0 or less means unknown and uses 3:4.</summary>
    public static double PicturePaneWidth(double width, double height, double pictureAspect)
    {
        double aspect = pictureAspect > 0 ? pictureAspect : DefaultPictureAspect;
        return Math.Max(0, Math.Min(height * aspect, width * MaxPictureShare));
    }

    /// <summary>
    /// Where the text card's left edge is inside the reading surface on the page held: the picture pane plus the gap. 0 when the page has no picture (null aspect) or is picture only.
    /// A ratio of 0 or less means a picture of unknown shape. The page's own layout and the tablet controls row both use this one function.
    /// </summary>
    public static double TextCardLeft(double width, double height, double? pictureAspect, bool isPictureOnly) =>
        pictureAspect is null || isPictureOnly ? 0 : PicturePaneWidth(width, height, pictureAspect.Value) + SidewaysGap;

    /// <summary>A list's pane beside its working part: 300 on a phone, 360 on a tablet.</summary>
    public static double ListPaneWidth(double width, double height) =>
        SizeOf(width, height) == DeviceSize.Phone ? PhoneListPane : TabletListPane;

    /// <summary>The reading controls sit in a right-hand rail when the window is under 480 dp tall, else in a row under the text.</summary>
    public static ControlsPlacement ReadingControls(double height) =>
        height < RailHeightBelow ? ControlsPlacement.Rail : ControlsPlacement.Row;

    public static ControlSizes ControlSizesFor(ControlsPlacement placement) =>
        placement == ControlsPlacement.Rail ? RailControls : RowControls;

    /// <summary>In landscape a page's pinned bottom button moves to the bottom of its side rail.</summary>
    public static PinnedPlacement PinnedButton(double width, double height) =>
        IsLandscape(width, height) ? PinnedPlacement.SideRailBottom : PinnedPlacement.BottomBar;

    /// <summary>The cover width that goes with a cover height: covers are 3 wide for 4 tall.</summary>
    public static double CoverWidth(double coverHeight) => Math.Max(0, coverHeight) * DefaultPictureAspect;

    /// <summary>
    /// The grown-ups' corner (mock frames 111, 126): an ink side rail when the window is held sideways, or is a tablet wide enough (744 dp) for the rail plus three columns in portrait;
    /// a phone in portrait keeps the header bar and three columns. The rail is <see cref="ListPaneWidth"/> (300 on a phone, 360 on a tablet); the books take the width left over,
    /// as many columns of at least <see cref="CornerColumnWidth"/> as fit (2 to 5).
    /// </summary>
    public static CornerShape CornerLayout(double width, double height)
    {
        bool tablet = SizeOf(width, height) == DeviceSize.Tablet;
        bool landscape = IsLandscape(width, height);
        if (!landscape && !(tablet && width >= TabletPortraitRailMinWidth))
        {
            return new CornerShape(false, 0, PortraitCornerColumns, false);
        }

        double rail = ListPaneWidth(width, height);
        return new CornerShape(true, rail, CornerColumnsFor(width - rail), tablet);
    }

    /// <summary>How many columns of books fit in the width left of the rail (after the list's side margins and the gaps), 2 to 5.</summary>
    public static int CornerColumnsFor(double widthLeft)
    {
        double room = Math.Max(0, widthLeft - CornerBooksMargin) + CornerColumnGap;
        return (int)Math.Clamp(Math.Floor(room / (CornerColumnWidth + CornerColumnGap)), MinCornerColumns, MaxCornerColumns);
    }

    /// <summary>The Cast screen's rows: two columns held sideways (mock frame 119), one column upright.</summary>
    public static int CastColumns(double width, double height) => IsLandscape(width, height) ? 2 : 1;

    /// <summary>Room the sideways row keeps clear at its start for a camera cut-out, in dp.</summary>
    public const double SidewaysInset = 34;

    public const double TabletPortraitRailMinWidth = 744;
    public const int PortraitCornerColumns = 3;
    public const double CornerColumnWidth = 100;
    public const double CornerColumnGap = 12;
    public const double CornerBooksMargin = 42;
    public const int MinCornerColumns = 2;
    public const int MaxCornerColumns = 5;
}
