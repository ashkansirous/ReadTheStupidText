namespace DinoReader.Core;

/// <summary>The four shapes of the reading screen (Slice 98, Slice 174): upright, picture only held sideways, picture + text held sideways
/// (words over the picture) and text held sideways.</summary>
public enum ReadingLayout
{
    Upright,
    PictureSide,
    PictureOverlay,
    TextRow,
}

/// <summary>Which shape the reading screen takes, and which mode a book can offer (Slice 164 follow-up).</summary>
public static class ReadingLayoutRules
{
    public static ReadingLayout For(double width, double height, ReadingMode mode, bool isPictureTextBook)
    {
        if (width <= 0 || width <= height)
        {
            return ReadingLayout.Upright;
        }

        if (mode == ReadingMode.Text)
        {
            return ReadingLayout.TextRow;
        }

        if (!isPictureTextBook)
        {
            return ReadingLayout.Upright;
        }

        return mode == ReadingMode.PictureAndText ? ReadingLayout.PictureOverlay : ReadingLayout.PictureSide;
    }

    /// <summary>A mode the switch cannot offer (picture + text on a book whose pages do not change with the words) is folded to picture only.</summary>
    public static ReadingMode Offered(ReadingMode mode, bool offersPictureAndText) =>
        !offersPictureAndText && mode == ReadingMode.PictureAndText ? ReadingMode.PictureOnly : mode;

    /// <summary>The reading surface's margins in dp, and the left inset the floating chip and the tablet controls row start from.</summary>
    public readonly record struct SurfaceInsets(double Left, double Top, double Right, double Bottom);

    /// <summary>Mock Batch 44 frames 113 and 114: a phone held sideways keeps 34 dp clear at the left (camera cut-out).</summary>
    public const double PhoneSidewaysLeft = 34;

    /// <summary>Frames 113 and 114: the phone's right edge keeps 26 dp (the rail itself carries it, so the surface takes none beside a rail).</summary>
    public const double PhoneSidewaysRight = 26;

    public const double TabletSidewaysSide = 14;
    public const double SidewaysTop = 6;
    public const double SidewaysBottom = 8;

    /// <summary>Frame 127: the book title above the tablet's text card, 24 px, in a 36 dp line (6 above, 30 for the text), then a 20 dp gap before the card.</summary>
    public const double TabletTitleSize = 24;
    public const double TabletTitleLine = 36;
    public const double TabletTitleGap = 20;

    /// <summary>The surface's margins when the window is held sideways, by device size and by rail or row.</summary>
    public static SurfaceInsets SidewaysSurface(double width, double height, ControlsPlacement controls)
    {
        bool rail = controls == ControlsPlacement.Rail;
        if (LandscapeLayout.SizeOf(width, height) == DeviceSize.Tablet)
        {
            return new SurfaceInsets(TabletSidewaysSide, SidewaysTop, rail ? 0 : TabletSidewaysSide, SidewaysBottom);
        }

        return new SurfaceInsets(PhoneSidewaysLeft, SidewaysTop, rail ? 0 : PhoneSidewaysRight, SidewaysBottom);
    }

    /// <summary>Frame 127: the title returns above the card when the controls are a row (room for it) and the page has a picture beside the card.
    /// The one decision for both the title and the card's lowering (the page renderer and ReadingPage both ask it): a text card must be drawn on the page as well, or the title would stand alone.</summary>
    public static bool ShowsTabletTitle(ReadingLayout layout, ControlsPlacement controls, bool pageHasPictureBesideCard, bool pageDrawsTextCard, bool titleHasText) =>
        layout == ReadingLayout.PictureOverlay && controls == ControlsPlacement.Row && pageHasPictureBesideCard && pageDrawsTextCard && titleHasText;

    /// <summary>The title's line in dp: the mock's 36, or the label's measured height when the system font scale makes it taller.</summary>
    public static double TitleLine(double measuredHeight) => Math.Max(TabletTitleLine, measuredHeight);

    /// <summary>How far the card is lowered: the title's line and the gap when the title is shown, else nothing.</summary>
    public static double CardTop(bool titleShown, double titleLine) => titleShown ? titleLine + TabletTitleGap : 0;
}
