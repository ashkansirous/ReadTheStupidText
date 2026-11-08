namespace DinoReader.Core;

/// <summary>
/// The one seam between the Windows window and the page layout rules (Slice 235 part C, mock Batch 46). Every page asks its existing rule (<see cref="LandscapeLayout"/>, <see cref="OnboardingLayout"/>,
/// <see cref="TabletPageLayout"/>, <see cref="ReadingLayoutRules"/>, ...) with a width and a height; on phones and tablets those are the window's own, and the seam returns them unchanged. On the Windows head
/// the seam first makes the sizes agree with <see cref="WindowLayout"/> (the mock's rule on the content area), so the existing rules give the shape the window should have. No existing rule changes.
/// </summary>
public static class WindowShapeSeam
{
    /// <summary>Mock frames 151 and 154: a desktop shelf keeps 40 dp at each side (the phone's is 14).</summary>
    public const double DesktopShelfSidePadding = 40;

    /// <summary>A short side under this is a phone to the existing rules (<see cref="LandscapeLayout.TabletShortSide"/>); a window sized down to just under it reads as phone landscape.</summary>
    private const double JustUnderTablet = LandscapeLayout.TabletShortSide - 1;

    /// <summary>The shape on a phone or tablet: what the existing rules say about a window of this size.</summary>
    public static WindowShape DeviceShapeOf(double width, double height)
    {
        if (LandscapeLayout.IsTabletPortrait(width, height))
        {
            return WindowShape.TabletUpright;
        }

        if (!LandscapeLayout.IsLandscape(width, height))
        {
            return WindowShape.PhonePortrait;
        }

        return LandscapeLayout.SizeOf(width, height) == DeviceSize.Tablet ? WindowShape.TabletLandscape : WindowShape.PhoneLandscape;
    }

    /// <summary>The shape a page uses: the device's own everywhere except the Windows head, where the window's rule decides.</summary>
    public static WindowShape For(bool isWindowsHead, WindowShape windowLayout, WindowShape deviceShape) =>
        isWindowsHead ? windowLayout : deviceShape;

    /// <summary>
    /// The size to hand to the existing rules. Off Windows: exactly (<paramref name="width"/>, <paramref name="height"/>). On Windows: the same size, made smaller in height only when that is what it takes for
    /// the rules to see the shape <see cref="WindowLayout.For"/> gives (a square window is landscape; a window under 960 wide is phone landscape even when 600 or more tall). A window is never
    /// made larger, so nothing laid out from the result can overflow the real window; a short window (under 600 tall) is phone landscape in <see cref="WindowLayout.For"/> too, so the two always agree.
    /// </summary>
    public static (double Width, double Height) Effective(bool isWindowsHead, double width, double height)
    {
        if (!isWindowsHead || width <= 0 || height <= 0)
        {
            return (width, height);
        }

        WindowShape wanted = WindowLayout.For(width, height);
        double shaped = width == height ? height - 1 : height;
        if (wanted == WindowShape.PhoneLandscape)
        {
            shaped = Math.Min(shaped, JustUnderTablet);
        }

        return (width, shaped);
    }

    /// <summary>
    /// The shelf for a window. Off Windows: <see cref="LandscapeLayout.ShelfLayout"/>. On Windows a window that is phone landscape gets a grid like every other shape, never the phone's sideways row
    /// (mock frames 151-154: a mouse has a wheel and no swipe); the other shapes keep the existing rule.
    /// </summary>
    public static ShelfShape ShelfLayout(bool isWindowsHead, double width, double height)
    {
        if (!isWindowsHead)
        {
            return LandscapeLayout.ShelfLayout(width, height);
        }

        (double w, double h) = Effective(true, width, height);
        ShelfShape shape = LandscapeLayout.ShelfLayout(w, h);
        if (shape.TabletUpright)
        {
            return shape;
        }

        // Keep the rule's own tablet-landscape cover height (a sideways phone row has none to keep); the columns count the capped content width, not the whole window.
        double coverHeight = shape.Sideways ? 0 : shape.CoverHeight;
        return shape with { Sideways = false, CoverHeight = coverHeight, Columns = ShelfColumns(width), SidePadding = DesktopShelfSidePadding };
    }

    /// <summary>
    /// The shelf list's side margin on a desktop window: the shelf's own side padding, or half of what the page is wider than the 1600 dp cap, whichever is more. A pure function of the PAGE width, so the
    /// list never sizes itself from its own measured size (a layout cycle on the VM, Slice 235 part C).
    /// </summary>
    public static double ShelfSideMargin(double pageWidth, double sidePadding) =>
        Math.Max(sidePadding, (pageWidth - WindowLayout.MaximumContentWidth) / 2);

    /// <summary>The shelf list's left margin beside the grown-ups' side rail, inside the list's own column.</summary>
    public const double RailListInset = 16;

    /// <summary>The shelf list's top margin beside the grown-ups' side rail.</summary>
    public const double RailListTop = 14;

    /// <summary>The shelf list's right margin beside the grown-ups' side rail.</summary>
    public const double RailListRight = 26;

    /// <summary>
    /// The shelf list's real geometry (Slice 319): its left, top and right margins inside its own column, the columns of its grid and the gaps between tiles. The shelf applies it and the Reorder layer copies it,
    /// so the two grids cannot drift apart. <see cref="BesideRail"/> moves it onto the whole page when the list sits right of a rail.
    /// </summary>
    public readonly record struct ShelfGeometry(double Left, double Top, double Right, int Columns, double GapX, double GapY)
    {
        /// <summary>The same geometry measured from the page's left edge, for a layer that spans the page, rail included.</summary>
        public ShelfGeometry BesideRail(double railWidth) => this with { Left = Left + railWidth };
    }

    /// <summary>
    /// The one rule for the shelf list's margins and columns inside its column: beside the rail 16 / 14 / 26; held sideways <see cref="LandscapeLayout.SidewaysInset"/> on the left only; otherwise equal sides,
    /// the roomy side margin (the shelf's own padding, centred by <see cref="ShelfSideMargin"/> on a desktop window) for a tablet shelf or a desktop window, else the phone's.
    /// </summary>
    public static ShelfGeometry ShelfListGeometry(bool isWindowsHead, bool sideways, bool rail, bool tabletShelf, double pageWidth, double sidePadding, int columns, double gapX, double gapY)
    {
        if (rail)
        {
            return new ShelfGeometry(RailListInset, RailListTop, RailListRight, columns, gapX, gapY);
        }

        if (sideways)
        {
            return new ShelfGeometry(LandscapeLayout.SidewaysInset, 0, 0, columns, gapX, gapY);
        }

        double side = SideMargin(isWindowsHead, tabletShelf, pageWidth, sidePadding);
        return new ShelfGeometry(side, 0, side, columns, gapX, gapY);
    }

    /// <summary>Mock Batch 47 frames 173/174 (phone): the Reorder grid has 14 dp sides, 3 across, 10 across and 27 down (mock row pitch `RH=180` less the mock tile 153); no top padding under the hint.</summary>
    public const double ReorderPhoneSide = 14;
    public const int ReorderPhoneColumns = 3;
    public const double ReorderPhoneGapX = 10;
    public const double ReorderPhoneGapY = 27;

    /// <summary>Mock frame 176 (Windows, a 1100 dp window): 40 dp sides, 6 across, an 18 dp gap both ways, no top padding.</summary>
    public const double ReorderDesktopSide = 40;
    public const int ReorderDesktopMockColumns = 6;
    public const double ReorderDesktopGap = 18;
    public const double ReorderDesktopMockWidth = 1100;
    public const int ReorderDesktopMinColumns = 3;

    /// <summary>The cover-to-title spacing of a Reorder tile (<see cref="ShelfOrderGrid.Fit(double,int,double,double,double,double,double,double,double)"/>'s titleSpacing).</summary>
    public const double ReorderTitleSpacing = 7;

    /// <summary>The mock's Windows tile width: the 1100 dp frame less 2 x 40 sides and 5 x 18 gaps, over 6 columns (155 dp).</summary>
    public static double ReorderDesktopTileWidth =>
        (ReorderDesktopMockWidth - (2 * ReorderDesktopSide) - ((ReorderDesktopMockColumns - 1) * ReorderDesktopGap)) / ReorderDesktopMockColumns;

    /// <summary>The Windows Reorder columns for a window: how many of the mock's 155 dp tiles fit between 40 dp sides with 18 dp gaps, at least 3. 6 at 1100 dp; the tile size stays the mock's at any width, the column count follows the width.</summary>
    public static int ReorderDesktopColumns(double windowWidth) =>
        Math.Max(ReorderDesktopMinColumns, (int)Math.Floor((windowWidth - (2 * ReorderDesktopSide) + ReorderDesktopGap) / (ReorderDesktopTileWidth + ReorderDesktopGap)));

    /// <summary>The Reorder layer has no mock frame on a tablet or a phone held sideways (the mock draws portrait phones and one Windows window): both keep the shelf's geometry (a phone rotated while the mode is open therefore relayouts between the two, by decision).</summary>
    public static bool ReorderUsesShelfGeometry(bool tablet, double windowWidth, double windowHeight) => tablet || windowWidth > windowHeight;

    /// <summary>The one tablet test for the Reorder layer: the shelf's own classification, or the corner's when the grown-ups' corner is showing.</summary>
    public static bool IsTabletUpright(bool grownUp, bool shelfTabletUpright, bool cornerTabletUpright) => grownUp ? cornerTabletUpright : shelfTabletUpright;

    /// <summary>The hint above the grid (frames 173/174: padding 12 / 18 / 12, 13.5 pt; frame 176: 16 / 40 / 14, 14 pt). The tablet keeps Slice 319's 12 / 18 / 8, 13.5 pt (no tablet frame).</summary>
    public readonly record struct ReorderHint(double Left, double Top, double Right, double Bottom, double FontSize);

    /// <summary>The Reorder layer's grid and hint (Slice 321).</summary>
    public readonly record struct ReorderLayerShape(ShelfGeometry Grid, ReorderHint Hint);

    /// <summary>
    /// The Windows head's Reorder layer, the mock's frame 176 (Slice 321). The layer covers the whole page including the grown-ups' rail (it spans every column of the gallery grid), so the 40 dp sides are
    /// measured from the page's own edges and the columns come from the full page width: no rail offset.
    /// </summary>
    public static ReorderLayerShape ReorderLayerDesktop(double pageWidth)
    {
        ShelfGeometry desktop = new(ReorderDesktopSide, 0, ReorderDesktopSide, ReorderDesktopColumns(pageWidth), ReorderDesktopGap, ReorderDesktopGap);
        return new ReorderLayerShape(desktop, new ReorderHint(ReorderDesktopSide, 16, ReorderDesktopSide, 14, 14));
    }

    /// <summary>
    /// A tablet, or a phone held sideways, has no mock frame, so the layer keeps the shelf's geometry (<paramref name="shelfOnPage"/>, already measured from the page's left edge, which is the layer's: the layer spans the page)
    /// as Slice 319 did, with the old top inset (the shelf's own, else one gap). The hint starts at the grid's left edge. A phone rotated while the mode is open switches between this and <see cref="ReorderLayerPhone"/> (slice decision).
    /// </summary>
    public static ReorderLayerShape ReorderLayerShelf(ShelfGeometry shelfOnPage)
    {
        double top = shelfOnPage.Top > 0 ? shelfOnPage.Top : shelfOnPage.GapY;
        return new ReorderLayerShape(shelfOnPage with { Top = top }, new ReorderHint(shelfOnPage.Left, 12, shelfOnPage.Right, 8, 13.5));
    }

    /// <summary>A phone held upright, mock frames 173/174: 14 dp sides, 3 across, no top padding under the hint.</summary>
    public static ReorderLayerShape ReorderLayerPhone()
    {
        ShelfGeometry phone = new(ReorderPhoneSide, 0, ReorderPhoneSide, ReorderPhoneColumns, ReorderPhoneGapX, ReorderPhoneGapY);
        return new ReorderLayerShape(phone, new ReorderHint(18, 12, 18, 12, 13.5));
    }

    private static double SideMargin(bool isWindowsHead, bool tabletShelf, double pageWidth, double sidePadding)
    {
        if (!tabletShelf && !isWindowsHead)
        {
            return LandscapeLayout.PhoneShelfSidePadding;
        }

        return isWindowsHead ? ShelfSideMargin(pageWidth, sidePadding) : sidePadding;
    }

    /// <summary>The shelf's columns on a desktop window: as many 190 dp columns as fit the content (capped at 1600 dp, centred), 2 to 6. 800 gives 4, 1100 gives 5, 1600 and more give 6 (mock frames 151, 153, 154).</summary>
    public static int ShelfColumns(double windowWidth) =>
        (int)Math.Clamp(Math.Floor(WindowLayout.ContentWidth(Math.Max(0, windowWidth)) / LandscapeLayout.ShelfColumnWidth), LandscapeLayout.MinShelfColumns, LandscapeLayout.MaxShelfColumns);
}
