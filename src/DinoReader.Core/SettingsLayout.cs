namespace DinoReader.Core;

/// <summary>The size of a store card's cover, in dp.</summary>
public readonly record struct CoverSize(double Width, double Height);

/// <summary>
/// The pure size rules of Settings, the Store and Backup when the window is held sideways (Slice 232 part D, mock Batch 44 frames 117, 118, 124, 128).
/// Phone or tablet and sideways or upright come from <see cref="LandscapeLayout"/>; this file only adds what these three screens decide on top.
/// </summary>
public static class SettingsLayout
{
    /// <summary>A tablet held upright keeps Settings in two panes from this width (the same 744 dp as the grown-ups' corner rail).</summary>
    public const double TabletPortraitTwoPaneMinWidth = LandscapeLayout.TabletPortraitRailMinWidth;

    /// <summary>The widest a settings page's content (a sub-page on its own, or the right pane on a tablet) gets, in dp.</summary>
    public const double MaxContentWidth = 640;

    public const int UprightStoreColumns = 1;
    public const int PhoneSidewaysStoreColumns = 2;
    public const int TabletSidewaysStoreColumns = 3;

    /// <summary>The widest a slim card's credit line gets, in dp, so a long credit cannot squeeze the title (about 150 dp is left beside the cover and pill on a phone held sideways).</summary>
    public const double SlimCreditsMaxWidth = 84;

    public static readonly CoverSize UprightStoreCover = new(66, 88);
    public static readonly CoverSize SlimStoreCover = new(52, 69);

    /// <summary>The store's cards: one column upright, two on a phone held sideways, three on a tablet held sideways.</summary>
    public static int StoreColumns(double width, double height)
    {
        if (!LandscapeLayout.IsLandscape(width, height))
        {
            return UprightStoreColumns;
        }

        return LandscapeLayout.SizeOf(width, height) == DeviceSize.Phone ? PhoneSidewaysStoreColumns : TabletSidewaysStoreColumns;
    }

    /// <summary>More than one column means slimmer cards: the cover is 52 dp wide and the sub-line moves up beside the title.</summary>
    public static bool SlimStoreCards(int columns) => columns > UprightStoreColumns;

    public static CoverSize StoreCover(int columns) => SlimStoreCards(columns) ? SlimStoreCover : UprightStoreCover;

    /// <summary>Settings shows the list and the open page side by side when held sideways, or on a tablet held upright that is at least 744 dp wide. A phone upright pushes each page.</summary>
    public static bool TwoPanes(double width, double height)
    {
        if (LandscapeLayout.IsLandscape(width, height))
        {
            return true;
        }

        return LandscapeLayout.SizeOf(width, height) == DeviceSize.Tablet && width >= TabletPortraitTwoPaneMinWidth;
    }

    /// <summary>On a tablet held upright the 300 dp list shows each row's value under its name, so nothing is cut short (mock Batch 45 frame 140); elsewhere the value sits at the row's end.</summary>
    public static bool ValueUnderName(double width, double height) => TwoPanes(width, height) && LandscapeLayout.IsTabletPortrait(width, height);

    /// <summary>The right pane's widest size: 640 dp on a tablet, no cap (it takes what the list leaves) on a phone.</summary>
    public static double PaneMaxWidth(double width, double height) =>
        LandscapeLayout.SizeOf(width, height) == DeviceSize.Tablet ? MaxContentWidth : double.PositiveInfinity;

    /// <summary>The width the right pane gets: the window less the list, capped at <see cref="PaneMaxWidth"/>. 0 with a single pane.</summary>
    public static double PaneWidth(double width, double height)
    {
        if (!TwoPanes(width, height))
        {
            return 0;
        }

        double room = Math.Max(0, width - LandscapeLayout.SideListWidth(width, height));
        return Math.Min(room, PaneMaxWidth(width, height));
    }

    /// <summary>Backup's two cards (save and restore) sit side by side when the window is held sideways, else one above the other.</summary>
    public static bool BackupSideBySide(double width, double height) => LandscapeLayout.IsLandscape(width, height);
}
