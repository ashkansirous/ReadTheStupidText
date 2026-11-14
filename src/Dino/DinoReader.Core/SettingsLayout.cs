namespace DinoReader.Core;

/// <summary>The size of a store card's cover, in dp.</summary>
public readonly record struct CoverSize(double Width, double Height);

/// <summary>
/// The pure size rules of the Store and Settings tabs of the grown-ups' corner (Slice 325 part B, grown-up mock frames T2 and W1). The tab's content sits right of the rail (or above the tab bar), so
/// these rules take the content column's width, not the window's. They replace the rules of Slice 232 part D (two-pane Settings, slim store cards).
/// </summary>
public static class SettingsLayout
{
    /// <summary>Frame W1: the Settings list is one column at most this wide, in dp, at every size.</summary>
    public const double MaxContentWidth = 620;

    /// <summary>Decision 4: the store shows two columns of cards from this content width (frame T2's content is about 890 dp); below it, one.</summary>
    public const double StoreTwoColumnMinWidth = 800;

    public const int OneStoreColumn = 1;
    public const int TwoStoreColumns = 2;

    /// <summary>Frame T2: a store card's cover is 76 dp wide, 3:4.</summary>
    public static readonly CoverSize StoreCover = new(76, 76 * 4.0 / 3.0);

    /// <summary>Frame T2: the gap between store cards, in dp, both ways.</summary>
    public const double StoreCardGap = 14;

    /// <summary>The store's cards: two columns when the content column is at least 800 dp wide, else one.</summary>
    public static int StoreColumns(double contentWidth) => contentWidth >= StoreTwoColumnMinWidth ? TwoStoreColumns : OneStoreColumn;

    /// <summary>The width of the column right of the rail (the whole window under a tab bar).</summary>
    public static double ContentWidth(double windowWidth, double railWidth) => Math.Max(0, windowWidth - railWidth);

    /// <summary>Backup's two cards (save and restore) sit side by side when the window is held sideways, else one above the other.</summary>
    public static bool BackupSideBySide(double width, double height) => LandscapeLayout.IsLandscape(width, height);

    /// <summary>Two backup cards each need about 270 dp for the longer languages' labels, so the content column (rail and margins already taken off) must be at least this wide.</summary>
    public const double BackupTwoCardMinWidth = 560;

    /// <summary>Whether the content column is wide enough to hold two backup cards side by side.</summary>
    public static bool BackupCardsFit(double contentWidth) => contentWidth >= BackupTwoCardMinWidth;
}
