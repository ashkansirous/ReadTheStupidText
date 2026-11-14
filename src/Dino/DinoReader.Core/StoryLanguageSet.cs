namespace DinoReader.Core;

/// <summary>The outcome of changing the app language while story languages are set (Slice 173, Decision 168).</summary>
/// <param name="Set">The new set, or null when the parent has to say which language goes first.</param>
/// <param name="DropChoices">The languages the parent may drop (the old app language and both sides) when <see cref="Set"/> is null.</param>
public sealed record MainLanguageChange(StoryLanguageSet? Set, IReadOnlyList<string> DropChoices);

/// <summary>
/// The languages a child's stories can be in (Slice 173, Decision 168): the app language, always, plus up to two side languages the parent picked.
/// Pure rules, no storage: the main language is whatever the app language is, the sides are what was saved. Languages are the app's fifteen
/// (<see cref="UiLanguage"/>) in their short codes.
/// </summary>
public sealed record StoryLanguageSet
{
    /// <summary>How many side languages a set holds, so three languages at most.</summary>
    public const int MaxSides = 2;

    private const char Separator = ',';

    private StoryLanguageSet(string main, IReadOnlyList<string> sides)
    {
        Main = main;
        Sides = sides;
    }

    /// <summary>The app language: always a story language, never removable.</summary>
    public string Main { get; }

    /// <summary>The side languages, in the order they were picked.</summary>
    public IReadOnlyList<string> Sides { get; }

    /// <summary>The app language first, then the sides.</summary>
    public IReadOnlyList<string> All => [Main, .. Sides];

    /// <summary>True when the child has a choice to make: the button shows only then.</summary>
    public bool HasChoice => Sides.Count > 0;

    public bool IsFull => Sides.Count >= MaxSides;

    /// <summary>A set for <paramref name="main"/> with <paramref name="sides"/>: unsupported codes, repeats and the main language itself are dropped, and only the first two sides stay.</summary>
    public static StoryLanguageSet Create(string? main, IEnumerable<string>? sides)
    {
        string mainCode = UiLanguage.Resolve(main);
        var kept = new List<string>();
        foreach (string side in sides ?? [])
        {
            string code = UiLanguage.Resolve(side);
            if (UiLanguage.IsSupported(side) && code != mainCode && !kept.Contains(code) && kept.Count < MaxSides)
            {
                kept.Add(code);
            }
        }

        return new StoryLanguageSet(mainCode, kept);
    }

    public bool Contains(string? language) => All.Contains(UiLanguage.Resolve(language));

    /// <summary>Whether <paramref name="language"/> may still be ticked: not the app language, not already in, and a side is free.</summary>
    public bool CanAdd(string language) => UiLanguage.IsSupported(language) && !Contains(language) && !IsFull;

    public StoryLanguageSet WithSide(string language) => CanAdd(language) ? Create(Main, [.. Sides, UiLanguage.Resolve(language)]) : this;

    public StoryLanguageSet WithoutSide(string language) => Create(Main, Sides.Where(s => s != UiLanguage.Resolve(language)));

    /// <summary>The language the child reads in: <paramref name="pick"/> when it is in the set, else the app language.</summary>
    public string Reading(string? pick) => pick is not null && Contains(pick) ? UiLanguage.Resolve(pick) : Main;

    /// <summary>
    /// Changes the app language. A side language becoming the app language swaps places with the old one; a new language takes the app language and
    /// the old one becomes a side when there is room; with both sides taken the parent is asked which of the three goes (<see cref="MainLanguageChange.DropChoices"/>).
    /// </summary>
    public MainLanguageChange ChangeMain(string newMain)
    {
        string code = UiLanguage.Resolve(newMain);
        if (code == Main)
        {
            return new MainLanguageChange(this, []);
        }

        if (Sides.Contains(code))
        {
            return new MainLanguageChange(Create(code, Sides.Select(s => s == code ? Main : s)), []);
        }

        return IsFull ? new MainLanguageChange(null, All) : new MainLanguageChange(Create(code, [Main, .. Sides]), []);
    }

    /// <summary>The set after the parent chose to drop <paramref name="dropped"/> (one of <see cref="MainLanguageChange.DropChoices"/>) when <paramref name="newMain"/> became the app language.</summary>
    public StoryLanguageSet ChangeMainDropping(string newMain, string dropped)
    {
        string gone = UiLanguage.Resolve(dropped);
        return Create(newMain, All.Where(l => l != gone));
    }

    /// <summary>The side languages as saved text.</summary>
    public static string Join(IEnumerable<string> sides) => string.Join(Separator, sides);

    /// <summary>The side languages in saved text; empty or damaged text gives none.</summary>
    public static IReadOnlyList<string> Split(string? saved) =>
        (saved ?? string.Empty).Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
