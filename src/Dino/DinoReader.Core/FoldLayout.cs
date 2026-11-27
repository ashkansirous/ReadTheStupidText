namespace DinoReader.Core;

/// <summary>The foldables the mocks draw (Slice 403). <see cref="None"/> is every other device: phones, tablets, Windows windows.</summary>
public enum FoldDevice
{
    None,
    Duo,
    Fold8,
    Fold8Ultra,
}

/// <summary>Phone layouts (under 600 wide) or the wide layouts (rail and two panes, from 600).</summary>
public enum FoldWidthClass
{
    Phone,
    Wide,
}

/// <summary>Which screen of a foldable a window is on. A device of <see cref="FoldDevice.None"/> has no fold screens, so every rule keeps its existing answer.</summary>
public readonly record struct FoldScreen(FoldDevice Device, bool Inner)
{
    public static readonly FoldScreen None = new(FoldDevice.None, false);

    public bool IsFoldable => Device != FoldDevice.None;

    public bool IsInner => IsFoldable && Inner;

    public bool IsUltra => Device == FoldDevice.Fold8Ultra;
}

/// <summary>A vertical fold: the gutter's centre x and its width, in dp.</summary>
public readonly record struct FoldGutter(double X, double Width);

/// <summary>A rectangle in dp.</summary>
public readonly record struct PageRect(double X, double Y, double Width, double Height);

/// <summary>The two pages of an open book: the left and right sides of the gutter.</summary>
public readonly record struct BookPages(PageRect Left, PageRect Right);

/// <summary>
/// Foldable rules (Slice 403, mocks iPhone Duo, Galaxy Z Fold8 and Fold8 Ultra), pure: the 600 split, the open book's two pages, and the column counts and rail widths drawn
/// for the foldable screens. Every rule takes a <see cref="FoldScreen"/>; with <see cref="FoldScreen.None"/> (a plain phone, tablet, Windows, a split-screen half) the existing
/// rules of <see cref="LandscapeLayout"/>, <see cref="CornerLayout"/> and <see cref="SettingsLayout"/> answer unchanged. Sizes are in dp.
/// </summary>
public static class FoldLayout
{
    /// <summary>From this width the layout is the wide one (the existing 600 rule).</summary>
    public const double WideFromWidth = LandscapeLayout.TabletShortSide;

    public const double InnerCornerRail = 230;
    public const double InnerCornerTabHeight = 44;
    public const double InnerShelfRail = 220;
    public const double InnerStorePlanColumn = 250;

    public const int OuterShelfColumns = 3;
    public const int OuterShelfColumnsUltra = 2;
    public const int OuterCornerBooks = 4;
    public const int OuterCornerBooksUltra = 3;
    public const int InnerCornerBooks = 5;
    public const int InnerCornerBooksUltra = 4;
    public const int InnerShelfColumns = 4;
    public const int InnerShelfColumnsUltra = 3;
    public const int InnerSettingsColumns = 2;
    public const int InnerSettingsColumnsUltra = 1;
    public const int InnerStoreBookColumns = 1;

    public const double InnerCornerGapDuo = 14;
    public const double InnerCornerGapFold = 12;

    public static FoldWidthClass ClassOf(double width) => width < WideFromWidth ? FoldWidthClass.Phone : FoldWidthClass.Wide;

    /// <summary>
    /// The screen the rules apply to for a window this wide (Decision 3): the 600 split comes first, so an inner screen's window under 600 (a half-width split) gets the outer / phone result.
    /// Every fold overload goes through this, so no caller has to derive <see cref="FoldScreen.Inner"/> from the width itself.
    /// </summary>
    public static FoldScreen EffectiveScreen(FoldScreen screen, double width) =>
        screen.IsInner && ClassOf(width) == FoldWidthClass.Phone ? screen with { Inner = false } : screen;

    /// <summary>
    /// The two pages when a fold lies inside the window, else null (no fold, or a gutter that does not sit strictly inside the window: the rules do not change). The pages meet the gutter's edges and run the window's full height.
    /// </summary>
    public static BookPages? PagesOf(double width, double height, FoldGutter? fold)
    {
        if (fold is null || !IsPositiveFinite(width) || !IsPositiveFinite(height) || !double.IsFinite(fold.Value.X) || !double.IsFinite(fold.Value.Width))
        {
            return null;
        }

        double half = Math.Max(0, fold.Value.Width) / 2;
        double left = fold.Value.X - half;
        double right = fold.Value.X + half;
        if (left <= 0 || right >= width)
        {
            return null;
        }

        return new BookPages(new PageRect(0, 0, left, height), new PageRect(right, 0, width - right, height));
    }

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0;

    /// <summary>The shelf's columns on a foldable's screen (mock DO1, DI4 and siblings); null off a foldable, so the caller keeps its own rule.</summary>
    public static int? ShelfColumns(FoldScreen screen)
    {
        if (!screen.IsFoldable)
        {
            return null;
        }

        return (screen.IsInner, screen.IsUltra) switch
        {
            (false, false) => OuterShelfColumns,
            (false, true) => OuterShelfColumnsUltra,
            (true, false) => InnerShelfColumns,
            (true, true) => InnerShelfColumnsUltra,
        };
    }

    /// <summary>The corner's Books columns on a foldable's screen (DO2, DI1 and siblings); null off a foldable.</summary>
    public static int? CornerBookColumns(FoldScreen screen)
    {
        if (!screen.IsFoldable)
        {
            return null;
        }

        return (screen.IsInner, screen.IsUltra) switch
        {
            (false, false) => OuterCornerBooks,
            (false, true) => OuterCornerBooksUltra,
            (true, false) => InnerCornerBooks,
            (true, true) => InnerCornerBooksUltra,
        };
    }

    /// <summary>The Settings groups' columns on a foldable's inner screen (DI3): two (Duo, Fold8), one on the Ultra; null elsewhere, so the caller keeps its own rule.</summary>
    public static int? SettingsColumns(FoldScreen screen)
    {
        if (!screen.IsInner)
        {
            return null;
        }

        return screen.IsUltra ? InnerSettingsColumnsUltra : InnerSettingsColumns;
    }

    /// <summary>The inner corner's Books gap: 14 on the Duo, 12 on the Folds (frame notes); null off the inner screen.</summary>
    public static double? CornerBookGap(FoldScreen screen)
    {
        if (!screen.IsInner)
        {
            return null;
        }

        return screen.Device == FoldDevice.Duo ? InnerCornerGapDuo : InnerCornerGapFold;
    }

    /// <summary>The kids' shelf's cream rail on a foldable's inner screen: 220; null elsewhere.</summary>
    public static double? ShelfRailWidth(double width, FoldScreen screen) =>
        EffectiveScreen(screen, width).IsInner ? InnerShelfRail : null;
}
