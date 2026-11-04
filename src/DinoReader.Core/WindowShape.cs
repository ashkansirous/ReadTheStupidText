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
    /// made larger, so nothing laid out from the result can overflow the real window; a short window that the mock calls tablet landscape (960+ wide, under 600 tall) stays what the rules call it.
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

    /// <summary>The shelf's columns on a desktop window: as many 190 dp columns as fit the content (capped at 1600 dp, centred), 2 to 6. 800 gives 4, 1100 gives 5, 1600 and more give 6 (mock frames 151, 153, 154).</summary>
    public static int ShelfColumns(double windowWidth) =>
        (int)Math.Clamp(Math.Floor(WindowLayout.ContentWidth(Math.Max(0, windowWidth)) / LandscapeLayout.ShelfColumnWidth), LandscapeLayout.MinShelfColumns, LandscapeLayout.MaxShelfColumns);
}
