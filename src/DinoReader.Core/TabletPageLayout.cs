namespace DinoReader.Core;

/// <summary>What a store card looks like for one window: the cover, whether the card is slim (sub-line beside the title) and whether the action is a full-width button under the card.</summary>
public readonly record struct StoreCardShape(int Columns, CoverSize Cover, bool Slim, bool ActionBelow);

/// <summary>The page editor's picture on a tablet held upright: its height in dp, with the Change picture button over it.</summary>
public readonly record struct EditorPictureBand(double Height, bool Strip);

/// <summary>
/// The pure rules of the grown-ups' pages on a tablet held upright (Slice 234 part C, mock Batch 45 frames 142-146): Record, Store, Cast, the page editor and Backup. Each rule takes the window size;
/// on a tablet held upright it gives the mock's value and on every other window it gives the existing rule's value unchanged, so a page asks only this class.
/// </summary>
public static class TabletPageLayout
{
    public const int StoreColumnsUpright = 2;
    public const int CastColumnsUpright = 2;

    /// <summary>Frame 143: the store's covers are 90 dp wide, 3:4.</summary>
    public static readonly CoverSize StoreCoverUpright = new(90, 120);

    public const double StoreActionHeight = 48;

    /// <summary>Frames 142 and 145: the rounded panel behind a page picture has 24 dp corners (its colour is <c>ReadingTheme.PicturePanel</c>).</summary>
    public const double PanelRadius = 24;

    /// <summary>Frames 142-146: a page's content sits 40 dp from the window's sides on a tablet held upright (the phone's is 14).</summary>
    public const double PageSideMargin = 40;

    /// <summary>Frame 142: the words are 30 sp on a tablet held upright.</summary>
    public const double RecordWordsSize = 30;

    /// <summary>Frame 142: the page picture above the words is this tall.</summary>
    public const double RecordPictureHeight = 400;

    /// <summary>Frame 145: the editor's picture is this tall, and a 120 dp strip while the keyboard is up.</summary>
    public const double EditorPictureHeight = 440;
    public const double EditorPictureStripHeight = 120;

    /// <summary>
    /// Frame 144: the cast picker opens as a centred card, min(width - 80, 560) wide. This is the book sheet's rule (frame 136, <see cref="BookSheetPlan"/>, part B of Slice 234); fold the two
    /// into one rule once both have merged.
    /// </summary>
    public const double PickerMaxWidth = 560;
    public const double PickerSideMargin = 80;

    public static bool Applies(double width, double height) => LandscapeLayout.IsTabletPortrait(width, height);

    /// <summary>Store columns: two on a tablet held upright, else <see cref="SettingsLayout.StoreColumns"/>.</summary>
    public static int StoreColumns(double width, double height) =>
        Applies(width, height) ? StoreColumnsUpright : SettingsLayout.StoreColumns(width, height);

    /// <summary>A store card for a window: tablet upright gets 90 dp covers, two columns of full cards and the action under them; anything else keeps Settings' shapes.</summary>
    public static StoreCardShape StoreCard(double width, double height)
    {
        int columns = StoreColumns(width, height);
        if (Applies(width, height))
        {
            return new StoreCardShape(columns, StoreCoverUpright, false, true);
        }

        return new StoreCardShape(columns, SettingsLayout.StoreCover(columns), SettingsLayout.SlimStoreCards(columns), false);
    }

    /// <summary>Cast columns: two on a tablet held upright, else <see cref="LandscapeLayout.CastColumns"/>.</summary>
    public static int CastColumns(double width, double height) =>
        Applies(width, height) ? CastColumnsUpright : LandscapeLayout.CastColumns(width, height);

    /// <summary>The cast picker's card width on a tablet held upright; 0 elsewhere (the picker keeps its own sheet).</summary>
    public static double PickerWidth(double width, double height) =>
        Applies(width, height) ? Math.Min(width - PickerSideMargin, PickerMaxWidth) : 0;

    /// <summary>Backup's cards side by side: sideways, or on a tablet held upright.</summary>
    public static bool BackupSideBySide(double width, double height) =>
        Applies(width, height) || SettingsLayout.BackupSideBySide(width, height);

    /// <summary>The page editor's picture band on a tablet held upright (null elsewhere): 440 dp, or a 120 dp strip while the field has focus (keyboard up) so the field keeps five lines.</summary>
    public static EditorPictureBand? EditorPicture(double width, double height, bool hasPicture, bool editorFocused)
    {
        if (!Applies(width, height) || !hasPicture)
        {
            return null;
        }

        return new EditorPictureBand(PictureHeight(EditorPictureHeight, height - EditorFixedRows, editorFocused), editorFocused);
    }

    /// <summary>The share of the body height a page picture may take, so a small or squarish tablet window keeps room for the words / the field.</summary>
    public const double PictureHeightShare = 0.5;

    /// <summary>
    /// Heights of what sits around the picture, taken off the window height to give the body height: Record's header, the lines and card under the picture and the footer; the editor's header and the
    /// five lines its field must keep. Estimates in dp (the pages read the window, never their own panel, so there is no loop).
    /// </summary>
    public const double RecordFixedRows = 360;
    public const double EditorFixedRows = 272;

    /// <summary>
    /// THE picture height rule for both pages: the 120 dp strip while the field has focus, else the mock's height capped at <see cref="PictureHeightShare"/> of <paramref name="bodyHeight"/> (the window
    /// less the header and the fixed rows under the picture; no cap when unknown, 0 or less).
    /// </summary>
    public static double PictureHeight(double mockHeight, double bodyHeight, bool fieldFocused)
    {
        if (fieldFocused)
        {
            return Math.Min(EditorPictureStripHeight, mockHeight);
        }

        return bodyHeight > 0 ? Math.Min(mockHeight, bodyHeight * PictureHeightShare) : mockHeight;
    }

    /// <summary>Record's picture panel on a tablet held upright: 400 dp, or less when the window is short.</summary>
    public static double RecordPicture(double windowHeight) => PictureHeight(RecordPictureHeight, windowHeight - RecordFixedRows, false);

    /// <summary>Room around the art inside the editor's panel (8 dp each side), and the helper line under it when it shows.</summary>
    public const double EditorPanelPadding = 8;
    public const double EditorHelperReserve = 40;

    /// <summary>The height the whole page picture is fitted to inside the editor's panel, so every region stays on screen and tappable.</summary>
    public static double EditorArtHeight(EditorPictureBand band) =>
        Math.Max(0, band.Height - 2 * EditorPanelPadding - (band.Strip ? 0 : EditorHelperReserve));
}
