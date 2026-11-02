namespace DinoReader.Core;

/// <summary>The centred cards a tablet held upright shows instead of a phone-width box or a bottom sheet (Slice 234 part B, mock Batch 45).</summary>
public enum TabletCardKind
{
    Gate,
    FirstRun,
    BookSheet,
}

/// <summary>
/// The pure size rules of the tablet-upright cards: the grown-up gate (frame 137), the first run (138), the book sheet (136) and the intro's picture panel (141).
/// Whether a window is a tablet held upright comes from <see cref="LandscapeLayout.IsTabletPortrait"/>; this file only holds the numbers drawn in the mock. Sizes are in dp.
/// </summary>
public static class TabletCardLayout
{
    /// <summary>The room a card leaves on each side of the window.</summary>
    public const double SideMargin = 40;

    /// <summary>The widest each card gets, as drawn in the frames (the mock's text says 560 for the sheet and 520 for the gate; the drawn widths win).</summary>
    public const double GateMaxWidth = 520;

    public const double FirstRunMaxWidth = 620;
    public const double BookSheetMaxWidth = 560;

    /// <summary>The gate's answer keys and hold ring on the card (frame 137).</summary>
    public const double GateKeyHeight = 80;

    public const double GateHoldRing = 160;

    /// <summary>The cover on the book sheet's card, 3 wide for 4 tall (frame 136).</summary>
    public const double BookSheetCoverWidth = 170;

    public const double BookSheetPadding = 28;
    public const double BookSheetReadNowHeight = 64;
    public const double BookSheetActionHeight = 52;

    /// <summary>The intro's picture panel above the copy (frame 141): 45% of the window height, between 240 and 520 dp, and the copy beside it never wider than 600.</summary>
    public const double IntroPanelMaxHeight = 520;

    public const double IntroPanelMinHeight = 240;
    public const double IntroPanelShare = 0.45;
    public const double IntroTextMaxWidth = 600;

    /// <summary>The card's width: the window less its side margins, never over the card's drawn width.</summary>
    public static double CardWidth(double width, TabletCardKind kind) =>
        Math.Max(0, Math.Min(width - (2 * SideMargin), MaxWidthOf(kind)));

    public static double MaxWidthOf(TabletCardKind kind) => kind switch
    {
        TabletCardKind.Gate => GateMaxWidth,
        TabletCardKind.FirstRun => FirstRunMaxWidth,
        _ => BookSheetMaxWidth,
    };

    /// <summary>How tall the intro's picture panel is on a window this tall.</summary>
    public static double IntroPanelHeight(double height) =>
        Math.Clamp(height * IntroPanelShare, IntroPanelMinHeight, IntroPanelMaxHeight);

    /// <summary>The tallest the book sheet's card gets: the window less the margin above and below (it scrolls inside).</summary>
    public static double BookSheetMaxHeight(double height) => CardMaxHeight(height);

    /// <summary>The tallest any tablet-upright card gets: the window less the margin above and below. A taller card scrolls inside, so nothing is clipped on a short window.</summary>
    public static double CardMaxHeight(double height) => Math.Max(0, height - (2 * SideMargin));

    /// <summary>
    /// The tallest the part of a card that scrolls may be: the card's cap less its padding and less what sits outside the scroll
    /// (the gate's hold ring and Back, the first run's action under the scroll). Whatever is outside never scrolls, so a hold is never taken as a drag.
    /// </summary>
    public static double ScrollAreaHeight(double cardMaxHeight, double chrome, double fixedBelow) =>
        Math.Max(0, cardMaxHeight - chrome - fixedBelow);

    /// <summary>What sits under the gate's scrolling part on the hold step: the ring (as laid out), its caption and Back, with the grid's row spacing between them.</summary>
    public static double GateFixedBelow(bool ringShown, double ringSize, double ringTopMargin, double captionHeight, double backHeight, double rowSpacing) =>
        ringShown ? ringSize + ringTopMargin + captionHeight + backHeight + (2 * rowSpacing) : backHeight + rowSpacing;
}
