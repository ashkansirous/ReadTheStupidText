namespace DinoReader.Core;

/// <summary>Where a book on the shelf came from (Slice 189): bundled with the app, given by the Store, or added by a grown-up.</summary>
public enum BookOrigin
{
    Own,
    Starter,
    Store,
}

/// <summary>One row of a book's options sheet (Slice 228).</summary>
public enum BookSheetRow
{
    ReadNow,
    ShowOriginalText,
    ReadWithMyVoice,
    RecordAndCast,
    EditPages,
    Export,
    Remove,
}

/// <summary>Which rows a book's options sheet (the "⋯" on its cover, Slice 189, mock Batch 37 frame 85b) shows. Show original text and Read with my voice
/// depend on the book's state, Edit pages only on an own book, Export only with the developer switch, and a Store book's Remove says it can come back.</summary>
public sealed record BookSheetPlan(bool ShowOriginalText, bool ShowVoice, bool ShowEditPages, bool ShowExport, bool CanReAddFromStore)
{
    /// <summary>The rows in the order the sheet shows them (Slice 228, mock Batch 43 frame 100): <see cref="BookSheetRow.ReadNow"/> is always first, then the book's switches and the Record and Cast pills, then the rows that depend on the book, and Remove last.</summary>
    public IReadOnlyList<BookSheetRow> Rows
    {
        get
        {
            List<BookSheetRow> rows = [BookSheetRow.ReadNow];
            AddIf(rows, ShowOriginalText, BookSheetRow.ShowOriginalText);
            AddIf(rows, ShowVoice, BookSheetRow.ReadWithMyVoice);
            rows.Add(BookSheetRow.RecordAndCast);
            AddIf(rows, ShowEditPages, BookSheetRow.EditPages);
            AddIf(rows, ShowExport, BookSheetRow.Export);
            rows.Add(BookSheetRow.Remove);
            return rows;
        }
    }

    private static void AddIf(List<BookSheetRow> rows, bool condition, BookSheetRow row)
    {
        if (condition)
        {
            rows.Add(row);
        }
    }

    public static BookSheetPlan For(BookOrigin origin, bool translated, bool hasRecording, bool developerExport) =>
        new(translated, hasRecording, origin == BookOrigin.Own, developerExport, origin == BookOrigin.Store);

    /// <summary>The origin of a book: a catalogue book (<paramref name="fromCatalogue"/>) is a starter when the app installed it itself, else a Store book; anything else is the grown-up's own.</summary>
    public static BookOrigin OriginOf(bool fromCatalogue, bool installedAsStarter) =>
        !fromCatalogue ? BookOrigin.Own : installedAsStarter ? BookOrigin.Starter : BookOrigin.Store;

    /// <summary>A card is at most this wide, in dp (Slice 232, mock Batch 44 frame 112).</summary>
    public const double CardWidth = 580;

    /// <summary>The cover on the card's left, in dp.</summary>
    public const double CardCoverWidth = 150;

    /// <summary>Room the card leaves round it, in dp.</summary>
    public const double CardMargin = 16;

    /// <summary>Held sideways, a bottom sheet would cover the whole screen, so the sheet is one centred card (cover left, actions right), the same on tablets; held upright it is the bottom sheet.</summary>
    public static BookSheetShape ShapeFor(double width, double height)
    {
        if (!LandscapeLayout.IsLandscape(width, height))
        {
            return BookSheetShape.BottomSheet;
        }

        double cardWidth = Math.Max(0, Math.Min(CardWidth, width - (2 * CardMargin)));
        return new BookSheetShape(true, cardWidth, Math.Max(0, height - (2 * CardMargin)));
    }
}

/// <summary>How the book sheet is laid out: a bottom sheet, or a centred card of this width and at most this height (it scrolls inside).</summary>
public readonly record struct BookSheetShape(bool Card, double CardWidth, double CardMaxHeight)
{
    public static readonly BookSheetShape BottomSheet = new(false, 0, 0);
}
