namespace DinoReader.Core;

/// <summary>Where a book on the shelf came from (Slice 189): bundled with the app, given by the Store, or added by a grown-up.</summary>
public enum BookOrigin
{
    Own,
    Starter,
    Store,
}

/// <summary>Which rows a book's options sheet (the "⋯" on its cover, Slice 189, mock Batch 37 frame 85b) shows. Show original text and Read with my voice
/// depend on the book's state, Edit pages only on an own book, Export only with the developer switch, and a Store book's Remove says it can come back.</summary>
public sealed record BookSheetPlan(bool ShowOriginalText, bool ShowVoice, bool ShowEditPages, bool ShowExport, bool CanReAddFromStore)
{
    public static BookSheetPlan For(BookOrigin origin, bool translated, bool hasRecording, bool developerExport) =>
        new(translated, hasRecording, origin == BookOrigin.Own, developerExport, origin == BookOrigin.Store);

    /// <summary>The origin of a book: a catalogue book (<paramref name="fromCatalogue"/>) is a starter when the app installed it itself, else a Store book; anything else is the grown-up's own.</summary>
    public static BookOrigin OriginOf(bool fromCatalogue, bool installedAsStarter) =>
        !fromCatalogue ? BookOrigin.Own : installedAsStarter ? BookOrigin.Starter : BookOrigin.Store;
}
