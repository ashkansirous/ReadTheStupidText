namespace DinoReader.Core;

/// <summary>The four shapes of the reading screen (Slice 98, Slice 174): upright, picture only held sideways, picture + text held sideways
/// (words over the picture) and text held sideways.</summary>
public enum ReadingLayout
{
    Upright,
    PictureSide,
    PictureOverlay,
    TextRow,

    /// <summary>A tablet held upright (Slice 234, mock Batch 45 frames 134, 135, 150): the picture panel on top, the text card below, the controls row under both.</summary>
    Stacked,
}

/// <summary>Which shape the reading screen takes, and which mode a book can offer (Slice 164 follow-up).</summary>
public static class ReadingLayoutRules
{
    public static ReadingLayout For(double width, double height, ReadingMode mode, bool isPictureTextBook)
    {
        if (LandscapeLayout.IsTabletPortrait(width, height))
        {
            if (!StackedFits(height))
            {
                return ReadingLayout.Upright;
            }

            return mode == ReadingMode.Text || isPictureTextBook ? ReadingLayout.Stacked : ReadingLayout.Upright;
        }

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

    /// <summary>Slice 234, mock Batch 45 frames 134 and 135: the picture panel is 48% of the height, the text 32 sp (36 sp in text only), the surface keeps 40 dp at each side.</summary>
    public const double StackedPictureShare = 0.48;
    public const double StackedTextSize = 32;
    public const double StackedTextOnlySize = 36;
    public const double StackedTextOnlyTopInset = 84;
    public const double StackedSide = 40;
    public const double StackedTop = 6;
    public const double StackedGap = 20;
    public const double StackedChipInset = 12;
    public const double StackedMinCard = 120;

    /// <summary>The controls row and the gap above it (play button 84 + 20), which the frame's 48% counts as part of the height.</summary>
    public const double StackedControlsBand = 104;

    /// <summary>The stacked surface's margins: 40 dp at each side, no bottom (the controls row sits right under it).</summary>
    public static SurfaceInsets StackedSurface => new(StackedSide, StackedTop, StackedSide, 0);

    /// <summary>The floating chip's margin: 12 dp in from the panel's (or the card's) top left corner, inside the surface margins.</summary>
    public static SurfaceInsets StackedChip => new(StackedSide + StackedChipInset, StackedTop + StackedChipInset, 0, 0);

    /// <summary>Whether a window this tall has room for the stacked shape: the surface (the window less the top margin and the controls band) must hold the gap and the 120 dp card with a picture panel above them.
    /// Otherwise the screen falls back to upright rather than draw a picture panel 0 high.</summary>
    public static bool StackedFits(double windowHeight) =>
        windowHeight - StackedTop - StackedControlsBand - StackedGap - StackedMinCard > 0;

    /// <summary>The picture panel's height: 48% of the surface plus the controls band, never leaving the text card under 120 dp (a short window).</summary>
    public static double StackedPictureHeight(double surfaceHeight)
    {
        double wanted = StackedPictureShare * (surfaceHeight + StackedControlsBand);
        return Math.Max(0, Math.Min(wanted, surfaceHeight - StackedGap - StackedMinCard));
    }

    /// <summary>The text size when stacked: 36 sp in text only, 32 sp beside a picture.</summary>
    public static double StackedTextSizeFor(ReadingMode mode) => mode == ReadingMode.Text ? StackedTextOnlySize : StackedTextSize;

    /// <summary>Frame 127: the title returns above the card when the controls are a row (room for it) and the page has a picture beside the card.
    /// The one decision for both the title and the card's lowering (the page renderer and ReadingPage both ask it): a text card must be drawn on the page as well, or the title would stand alone.</summary>
    public static bool ShowsTabletTitle(ReadingLayout layout, ControlsPlacement controls, bool pageHasPictureBesideCard, bool pageDrawsTextCard, bool titleHasText) =>
        layout == ReadingLayout.PictureOverlay && controls == ControlsPlacement.Row && pageHasPictureBesideCard && pageDrawsTextCard && titleHasText;

    /// <summary>The title's line in dp: the mock's 36, or the label's measured height when the system font scale makes it taller.</summary>
    public static double TitleLine(double measuredHeight) => Math.Max(TabletTitleLine, measuredHeight);

    /// <summary>How far the card is lowered: the title's line and the gap when the title is shown, else nothing.</summary>
    public static double CardTop(bool titleShown, double titleLine) => titleShown ? titleLine + TabletTitleGap : 0;
}
