namespace DinoReader.Core;

/// <summary>How the first-run and intro pages are arranged: one column, two panes, or (waits, offline, ready) one centred column.</summary>
public enum OnboardingMode
{
    Stack,
    Split,
    Centred,
}

/// <summary>Which of the mock's size families a window belongs to; the pane widths, columns and button sizes follow it (Slice 325 part D, onboarding mock frames O1-O9).</summary>
public enum OnboardingFamily
{
    Phone,
    PhoneLandscape,
    TabletPortrait,
    TabletLandscape,
    Windows,
}

/// <summary>
/// The first-run and intro skeleton for one window (Slice 325 part D, onboarding mock): <see cref="Mode"/> is <see cref="OnboardingMode.Stack"/> under 600 dp wide and
/// <see cref="OnboardingMode.Split"/> from 600; <see cref="LeftPane"/> is the left pane's width in dp (0 when stacked); <see cref="Compact"/> is a stacked page under 700 dp tall
/// (the iPhone 8 frames), which moves the icon beside the title and shrinks the buttons.
/// </summary>
public readonly record struct OnboardingShape(OnboardingMode Mode, OnboardingFamily Family, double LeftPane, bool Compact)
{
    public bool Split => Mode == OnboardingMode.Split;
}

/// <summary>The grown-up gate's arrangement (Slice 363, mock Missing States G1-G3): the phone's bottom sheet, or one centred cream card.</summary>
public enum GateMode
{
    /// <summary>A window under 600 wide: the bottom sheet, as built.</summary>
    Sheet,

    /// <summary>600 or wider and tall enough: the upright card (G1, G2), 440 wide.</summary>
    Card,

    /// <summary>600 or wider but short (a phone held sideways): the short card (G3), 470 wide, the ring or keys beside the heading.</summary>
    ShortCard,
}

/// <summary>Which of the three arrangements the window gets and how wide the card is.</summary>
/// <remarks><see cref="CardWidth"/> is 0 for the sheet.</remarks>
public readonly record struct GateShape(GateMode Mode, double CardWidth = 0)
{
    public bool Card => Mode != GateMode.Sheet;

    public bool Short => Mode == GateMode.ShortCard;
}

/// <summary>One cell of a grid, counted from 0.</summary>
public readonly record struct GridCell(int Row, int Column);

/// <summary>
/// The pure size rules of the first run, the intro and the grown-up gate (Slice 232 part E for the gate; Slice 325 part D for the first run and the intro, onboarding mock frames O1-O9).
/// Phone or tablet and sideways or upright come from <see cref="LandscapeLayout"/> and <see cref="WindowLayout"/>; this file only adds what these screens decide on top.
/// Each page asks once per size change and only places the views it already has: the wizard step, the gate's sum and the first-run pick are never in a layout.
/// </summary>
public static class OnboardingLayout
{
    /// <summary>The gate's answer keys: 52 dp upright, up to 64 dp held sideways so a covering hand never hides the sum (frame 122).</summary>
    public const double UprightKeyHeight = 52;

    public const double SidewaysKeyHeight = 64;

    /// <summary>The hold ring on a phone, in dp.</summary>
    public const double PhoneHoldRing = 120;
    public const double MinSidewaysKeyHeight = 40;

    /// <summary>The number pad has four rows.</summary>
    public const int KeyRows = 4;

    /// <summary>From this width the pages are two panes (frames OL1, OT2, OTL1, OW2).</summary>
    public const double SplitFromWidth = 600;

    /// <summary>Below this height a stacked page is the iPhone 8 layout (frames O1-8, O2-8, O6-8).</summary>
    public const double CompactBelowHeight = 700;

    /// <summary>Left pane widths: phone landscape 300, tablet portrait 320, tablet landscape 460 (560 for the intro, which carries a picture), Windows 380 (500 for the intro).</summary>
    public const double PhoneLandscapePane = 300;

    public const double TabletPortraitPane = 320;
    public const double TabletLandscapePane = 460;
    public const double TabletLandscapeIntroPane = 560;
    public const double WindowsPane = 380;
    public const double WindowsIntroPane = 500;

    /// <summary>The centred column of the waits, offline and ready pages, in dp (frames O3-O5).</summary>
    public const double CentredColumn = 440;

    /// <summary>The picture of an intro step: 100% on a phone, 80% beside the words on a phone held sideways or a short stacked page, 115% on a tablet held upright, 160% on a tablet held sideways.</summary>
    public const double ArtScaleDefault = 1.0;

    public const double ArtScaleReduced = 0.8;
    public const double ArtScaleTabletPortrait = 1.15;
    public const double ArtScaleTabletLandscape = 1.6;
    public const double ArtScaleWindows = 1.4;

    /// <summary>The shape of the first-run and intro pages in a window of this size. <paramref name="width"/> and <paramref name="height"/> are the window's, already passed through <see cref="WindowShapeSeam.Effective"/> on Windows.</summary>
    public static OnboardingShape Shape(double width, double height, bool windows, bool intro)
    {
        OnboardingFamily family = FamilyOf(width, height, windows);
        if (width < SplitFromWidth)
        {
            return new OnboardingShape(OnboardingMode.Stack, family, 0, height < CompactBelowHeight);
        }

        return new OnboardingShape(OnboardingMode.Split, family, PaneOf(family, intro), false);
    }

    private static OnboardingFamily FamilyOf(double width, double height, bool windows)
    {
        if (windows)
        {
            return OnboardingFamily.Windows;
        }

        return WindowShapeSeam.DeviceShapeOf(width, height) switch
        {
            WindowShape.TabletUpright => OnboardingFamily.TabletPortrait,
            WindowShape.TabletLandscape => OnboardingFamily.TabletLandscape,
            WindowShape.PhoneLandscape => OnboardingFamily.PhoneLandscape,
            _ => OnboardingFamily.Phone,
        };
    }

    private static double PaneOf(OnboardingFamily family, bool intro) => family switch
    {
        OnboardingFamily.Windows => intro ? WindowsIntroPane : WindowsPane,
        OnboardingFamily.TabletLandscape => intro ? TabletLandscapeIntroPane : TabletLandscapePane,
        OnboardingFamily.TabletPortrait => TabletPortraitPane,
        _ => PhoneLandscapePane,
    };

    /// <summary>Language buttons per row (frames O1, OL1, OTL1): 2 stacked, 3 on a phone held sideways, 2 on the wide panes.</summary>
    public static int LanguageColumns(OnboardingShape shape) =>
        shape.Split && shape.Family == OnboardingFamily.PhoneLandscape ? 3 : 2;

    /// <summary>A language button's height: 52 (48 on the iPhone 8), 46 on a phone held sideways, 64 on the wide panes.</summary>
    public static double LanguageButtonHeight(OnboardingShape shape) => shape switch
    {
        { Split: false, Compact: true } => 48,
        { Split: false } => 52,
        { Family: OnboardingFamily.PhoneLandscape } => 46,
        _ => 64,
    };

    /// <summary>The flag in a language button: 28 round stacked, 24 on a phone held sideways, 34 on the wide panes.</summary>
    public static double FlagSize(OnboardingShape shape) => shape switch
    {
        { Split: false } => 28,
        { Family: OnboardingFamily.PhoneLandscape } => 24,
        _ => 34,
    };

    /// <summary>Book columns in the first-books picker (frames O2, OL2, OT2, OW2): 2 stacked, 4 on a phone held sideways, 2 on a tablet held upright, 3 on the wide panes.</summary>
    public static int BookColumns(OnboardingShape shape) => shape switch
    {
        { Split: false } => 2,
        { Family: OnboardingFamily.PhoneLandscape } => 4,
        { Family: OnboardingFamily.TabletPortrait } => 2,
        _ => 3,
    };

    /// <summary>The store tags show on every shape but a phone held sideways (frame OL2).</summary>
    public static bool TagsShown(OnboardingShape shape) => !(shape.Split && shape.Family == OnboardingFamily.PhoneLandscape);

    /// <summary>The picked count sits beside Continue on the iPhone 8 and in every two-pane shape; above it on a normal phone (frames O2, O2-8, OL2, OW2).</summary>
    public static bool CountBesideContinue(OnboardingShape shape) => shape.Split || shape.Compact;

    /// <summary>The heading and line of the book picker join the back row on the iPhone 8, and use the shorter line.</summary>
    public static bool ShortBooksLine(OnboardingShape shape) => shape.Compact;

    /// <summary>The main coral button's size: 56 high, full width stacked; 50 high and 220 wide beside the panes, 52 high and 260 wide on Windows.</summary>
    public static (double Width, double Height) ContinueSize(OnboardingShape shape) => shape switch
    {
        { Split: false } => (double.NaN, 56),
        { Family: OnboardingFamily.Windows } => (260, 52),
        _ => (220, 50),
    };

    /// <summary>How much of an intro step's picture to draw (frames O6, O6-8, OL7, OT6, OTL7, OW6).</summary>
    public static double ArtScale(OnboardingShape shape) => shape switch
    {
        { Family: OnboardingFamily.TabletLandscape } => ArtScaleTabletLandscape,
        { Family: OnboardingFamily.TabletPortrait } => ArtScaleTabletPortrait,
        { Family: OnboardingFamily.Windows, Split: true } => ArtScaleWindows, // a narrow stacked Windows window follows the phone rules
        { Split: true } => ArtScaleReduced,
        { Compact: true } => ArtScaleReduced,
        _ => ArtScaleDefault,
    };

    /// <summary>The centred column's width in a window this wide, never over <see cref="CentredColumn"/>.</summary>
    public static double CentredWidth(double width, double sidePadding) =>
        Math.Max(0, Math.Min(CentredColumn, width - (2 * sidePadding)));

    /// <summary>The upright gate card is 440 wide, the short one 470 (mock G1-G3), never wider than the window less 24 dp each side.</summary>
    public const double GateCardWidth = 440;
    public const double GateShortCardWidth = 470;
    public const double GateCardMargin = 24;

    /// <summary>The upright card is about 460 dp tall on its tallest step; under this height the short card is used so the card fits with room around it.</summary>
    public const double GateShortBelowHeight = 540;

    /// <summary>Size rule after review: under 540 high is the short card (G3) at any width; else 600 or wider is the card; else the bottom sheet (phone upright).</summary>
    public static GateShape Gate(double width, double height)
    {
        if (height < GateShortBelowHeight)
        {
            return new GateShape(GateMode.ShortCard, Math.Max(0, Math.Min(GateShortCardWidth, width - (2 * GateCardMargin))));
        }

        if (width >= SplitFromWidth)
        {
            return new GateShape(GateMode.Card, Math.Max(0, Math.Min(GateCardWidth, width - (2 * GateCardMargin))));
        }

        return new GateShape(GateMode.Sheet);
    }

    /// <summary>Where the item at <paramref name="index"/> goes in a grid of <paramref name="columns"/> columns, filled row by row.</summary>
    public static GridCell CellOf(int index, int columns)
    {
        int safe = Math.Max(1, columns);
        return new GridCell(Math.Max(0, index) / safe, Math.Max(0, index) % safe);
    }

    /// <summary>How many rows <paramref name="count"/> items need in <paramref name="columns"/> columns.</summary>
    public static int RowsFor(int count, int columns)
    {
        int safe = Math.Max(1, columns);
        return (Math.Max(0, count) + safe - 1) / safe;
    }
}
