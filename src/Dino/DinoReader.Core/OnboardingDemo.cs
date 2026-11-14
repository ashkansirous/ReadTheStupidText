namespace DinoReader.Core;

/// <summary>
/// The page of a free book the onboarding plays and records on (Slice 362, Decision 2): <see cref="StoreId"/> is the book's package id (<c>Story.SourceId</c>), <see cref="Page"/> counts from 1 as the book's
/// <c>story.md</c> does, and <see cref="Character"/> is the speaker id (the character's name in the book's cast) whose lines the grown-up records. The copy names the character through <c>{0}</c>, so no string is book-specific.
/// </summary>
public sealed record OnboardingDemoPage(string StoreId, int Page, string Character)
{
    /// <summary>The page's 0-based index in <c>Story.Pages</c>.</summary>
    public int PageIndex => Page - 1;
}

/// <summary>
/// Which page the listen and voice steps use (Slice 362, Decision 2). The parent may not pick The Three Little Pigs, so every free book has one set page with two speakers and two feelings; the first of these that is on the
/// shelf wins, in this order. Pure: the pages were chosen from each book's <c>story.md</c>.
/// </summary>
public static class OnboardingDemo
{
    public const string ThreeLittlePigsId = "966400017c494fb5b045786f53852c9e";
    public const string LionAndMouseId = "1ca50c6936cc44788b992726784e8132";
    public const string GingerbreadManId = "bee62232085d4c1ab50cdf574530f473";
    public const string XMarksTheTreasureId = "a02c0731b7ac475a896e9184f3575e7b";

    /// <summary>Priority order: Pop scared and Pim calm (record Pim); Lion doubtful and Mouse sure (record Mouse); Granny pleading and the Gingerbread Man laughing (record Granny); Seagull worried and Pancake Polly cheerful (record Pancake Polly).</summary>
    public static readonly IReadOnlyList<OnboardingDemoPage> Pages =
    [
        new(ThreeLittlePigsId, 13, "Pim"),
        new(LionAndMouseId, 11, "Mouse"),
        new(GingerbreadManId, 3, "Granny"),
        new(XMarksTheTreasureId, 10, "Pancake Polly"),
    ];

    /// <summary>The demo page for the books on the shelf (<paramref name="shelfIds"/> are their package ids), or null when none of the four is there (the steps then have nothing to play and carry on).</summary>
    public static OnboardingDemoPage? Pick(IEnumerable<string?> shelfIds)
    {
        HashSet<string> shelf = new(shelfIds.OfType<string>(), StringComparer.OrdinalIgnoreCase);
        return Pages.FirstOrDefault(page => shelf.Contains(page.StoreId));
    }

    /// <summary>The demo character's lines on the demo page, in story order: the pieces the record page walks in step 5 (no other line of the book is offered).</summary>
    public static IReadOnlyList<RecordPiece> LinesOf(IEnumerable<RecordPiece> pieces, OnboardingDemoPage demo) =>
        pieces.Where(p => p.Key.Page == demo.PageIndex && p.Speaker == demo.Character).ToList();

    /// <summary>The first and last sentence numbers (0-based, whole book) of the demo page in <paramref name="sentences"/>; null when the page has none.</summary>
    public static (int First, int Last)? SentenceRange(IReadOnlyList<RecordSentence> sentences, OnboardingDemoPage demo)
    {
        int first = -1;
        int last = -1;
        for (int i = 0; i < sentences.Count; i++)
        {
            if (sentences[i].Page != demo.PageIndex)
            {
                continue;
            }

            first = first < 0 ? i : first;
            last = i;
        }

        return first < 0 ? null : (first, last);
    }

}

/// <summary>The six required steps of the first run (Slice 362, mock 2026-11-13): the step bar's segments, in order.</summary>
public enum OnboardingStep
{
    Language,
    StoryLanguages,
    Books,
    Listen,
    Voice,
    Corner,
}

/// <summary>Pure rules of the onboarding path: the step bar, the usage names and what comes after which step.</summary>
public static class OnboardingFlow
{
    /// <summary>The bar has one segment per step.</summary>
    public static int Count => Enum.GetValues<OnboardingStep>().Length;

    /// <summary>"Step {0}" of the label: counted from 1.</summary>
    public static int NumberOf(OnboardingStep step) => (int)step + 1;

    /// <summary>How many segments are filled when <paramref name="current"/> shows: the current one and every one before it.</summary>
    public static int FilledAt(OnboardingStep current) => NumberOf(current);

    /// <summary>The closed name an <c>intro_step</c> count carries (the Worker keeps the same list, <see cref="IntroSteps"/>).</summary>
    public static string NameOf(OnboardingStep step) => step switch
    {
        OnboardingStep.Language => IntroSteps.Language,
        OnboardingStep.StoryLanguages => IntroSteps.StoryLanguages,
        OnboardingStep.Books => IntroSteps.Books,
        OnboardingStep.Listen => IntroSteps.Listen,
        OnboardingStep.Voice => IntroSteps.Voice,
        _ => IntroSteps.Corner,
    };

    /// <summary>The step after <paramref name="step"/>, or null after the last.</summary>
    public static OnboardingStep? After(OnboardingStep step) => step == OnboardingStep.Corner ? null : step + 1;

    /// <summary>
    /// A saved step made valid. Steps 1 to 3 are held by the language and book gates (<see cref="OnboardingStep.Listen"/> is the first one saved); a value outside the steps, or one before the listen step, means onboarding is not
    /// running (null), so an old install never lands in the middle of a path it did not start.
    /// </summary>
    public static OnboardingStep? Saved(int stored) =>
        stored >= (int)OnboardingStep.Listen && stored <= (int)OnboardingStep.Corner ? (OnboardingStep)stored : null;
}

/// <summary>The stages of the listen step (Slice 362 work item B): the first language plays, the bubble points at the second, the second plays.</summary>
public enum ListenStage
{
    /// <summary>The first language plays. Continue is dim while two languages are on offer, live at the page's end with one.</summary>
    First,

    /// <summary>The first language ended: the bubble and the amber pulse point at the second one and Continue stays dim.</summary>
    PointAtSecond,

    /// <summary>The second (or another) language plays: Continue is live.</summary>
    Other,
}

/// <summary>
/// The listen step's small state machine (Slice 362 work item B, mock N4a-N4d): pure, so every language count is tested. With one family language the page plays once and Continue wakes when it ends (N4d); with two or three the
/// first language plays, the bubble points at the second, and Continue wakes as the second one starts (N4c). The state only moves on <see cref="PageEnded"/> and <see cref="LanguageStarted"/>.
/// </summary>
public sealed class ListenDemo
{
    private readonly IReadOnlyList<string> _languages;
    private bool _ended;

    /// <param name="languages">The family's story languages, the app language first.</param>
    public ListenDemo(IReadOnlyList<string> languages)
    {
        _languages = languages;
    }

    public ListenStage Stage { get; private set; } = ListenStage.First;

    /// <summary>More than one language on offer: the bar shows and the bubble points at the second.</summary>
    public bool HasOther => _languages.Count > 1;

    /// <summary>The language the bubble points at (the second); null with one language.</summary>
    public string? Pointed => HasOther ? _languages[1] : null;

    /// <summary>True once the page has ended in the first language (N4d), or a second language has started (N4c).</summary>
    public bool ContinueLive => Stage == ListenStage.Other || (!HasOther && _ended);

    /// <summary>The bubble and the pulse show between the first language ending and the second one starting.</summary>
    public bool BubbleShown => Stage == ListenStage.PointAtSecond;

    /// <summary>The page was read to its end in the language that was playing.</summary>
    public void PageEnded()
    {
        _ended = true;
        if (Stage == ListenStage.First && HasOther)
        {
            Stage = ListenStage.PointAtSecond;
        }
    }

    /// <summary>A language began to play; the first one at the start changes nothing, any other wakes Continue.</summary>
    public void LanguageStarted(string language)
    {
        if (HasOther && language != _languages[0] && Stage != ListenStage.Other)
        {
            Stage = ListenStage.Other;
        }
    }
}

/// <summary>The grown-ups' corner tour (Slice 362 work item D, mock N8a-N10): welcome card, the three tabs one at a time, the child's shelf pointer, the shelf card.</summary>
public enum TourStop
{
    Welcome,
    Books,
    Store,
    Settings,
    ShelfPointer,
    ChildShelf,
}

/// <summary>Pure rules of the tour: its order, the "{0} of {1}" counter and where it ends.</summary>
public static class CornerTourFlow
{
    /// <summary>The three tab tips, in the order the tour shows them (the mock's tip list: Books, Store, Settings).</summary>
    public static readonly IReadOnlyList<TourStop> Tabs = [TourStop.Books, TourStop.Store, TourStop.Settings];

    /// <summary>The stop after <paramref name="stop"/>; null after the shelf card (Start reading ends the tour).</summary>
    public static TourStop? Next(TourStop stop) => stop == TourStop.ChildShelf ? null : stop + 1;

    /// <summary>The 1-based number a tab tip shows in "{0} of {1}"; 0 for a stop that has no counter.</summary>
    public static int CounterOf(TourStop stop) => Tabs.ToList().IndexOf(stop) + 1;

    /// <summary>How many tab tips there are, the {1} of the counter.</summary>
    public static int CounterTotal => Tabs.Count;

    /// <summary>True for the stops where the real tab bar is under the scrim and one tab is lifted out of it.</summary>
    public static bool LiftsATab(TourStop stop) => Tabs.Contains(stop);
}

/// <summary>One run of text of a string that marks bold words with <c>**double stars**</c> (the copy of the onboarding, Slice 362).</summary>
public readonly record struct MarkupRun(string Text, bool Bold);

/// <summary>Splits the onboarding copy's <c>**bold**</c> markup into runs; the label that shows it builds one span per run. An unmatched marker is shown as it is.</summary>
public static class OnboardingMarkup
{
    private const string Marker = "**";

    public static IReadOnlyList<MarkupRun> Split(string text)
    {
        string[] parts = text.Split(Marker);
        bool balanced = parts.Length % 2 == 1; // an even count means a marker is left open: nothing is bold then and the text is shown as it is
        if (!balanced)
        {
            return text.Length == 0 ? [] : [new MarkupRun(text, false)];
        }

        List<MarkupRun> runs = [];
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
            {
                runs.Add(new MarkupRun(parts[i], i % 2 == 1));
            }
        }

        return runs;
    }
}
