namespace DinoReader.Core;

/// <summary>Which language a book or the shelf shows when the parent set more than one story language (Slice 164 follow-up).</summary>
public static class LanguagePick
{
    public const int MinimumChoices = 2;

    /// <summary>A book offers a language switch whenever there is a choice: recordings no longer matter, each plays in the language it was made in (Slice 286).</summary>
    public static bool BookSwitchable(bool hasChoice) => hasChoice;

    /// <summary>The child's pick when the book can be read in it; else the app language, else the list's first language. With no choice to offer, the app language.</summary>
    public static string Effective(IReadOnlyList<string> readable, string main, string pick)
    {
        if (readable.Count < MinimumChoices)
        {
            return main;
        }

        if (readable.Contains(pick))
        {
            return pick;
        }

        return readable.Contains(main) ? main : readable[0];
    }

    /// <summary>The language already on the book's button, kept while it is still on the list; else <see cref="Effective"/>.</summary>
    public static string Shown(IReadOnlyList<string> readable, string current, string main, string pick) =>
        readable.Count >= MinimumChoices && readable.Contains(current) ? current : Effective(readable, main, pick);

    /// <summary>The shelf button's language: the pick when it is on the list, else the app language.</summary>
    public static string ShelfShown(IReadOnlyList<string> choosable, string pick, string main) =>
        choosable.Contains(pick) ? pick : main;

    /// <summary>The language of the kids' shelf's tile titles (Slice 323, Decision 173): the language on the shelf button while the button is shown (two or more choices, kids' shelf);
    /// the app language when it is hidden or in grown-up mode.</summary>
    public static string ShelfTitles(bool grownUp, IReadOnlyList<string> choosable, string pick, string main) =>
        ShelfButtonShown(grownUp, choosable) ? ShelfShown(choosable, pick, main) : main;

    /// <summary>The one rule for the shelf's language button (kids' shelf, two or more choosable languages); the button and the tile titles both use it.</summary>
    public static bool ShelfButtonShown(bool grownUp, IReadOnlyList<string> choosable) =>
        !grownUp && choosable.Count >= MinimumChoices;
}
