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

/// <summary>The five required steps of the first run (Slice 362; Slice 418 removed the corner tour): the step bar's segments, in order.</summary>
public enum OnboardingStep
{
    Language,
    StoryLanguages,
    Books,
    Listen,
    Voice,
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
        _ => IntroSteps.Voice,
    };

    /// <summary>The step after <paramref name="step"/>, or null after the last.</summary>
    public static OnboardingStep? After(OnboardingStep step) => step == OnboardingStep.Voice ? null : step + 1;

    /// <summary>
    /// A saved step made valid. Steps 1 to 3 are held by the language and book gates (<see cref="OnboardingStep.Listen"/> is the first one saved); a value outside the steps, or one before the listen step, means onboarding is not
    /// running (null), so an old install never lands in the middle of a path it did not start. The old corner step (stored 5, removed by Slice 418) was the last one: it resumes at the next existing step, which
    /// is none, so setup is over (<see cref="OldCornerStep"/>).
    /// </summary>
    public static OnboardingStep? Saved(int stored) =>
        stored >= (int)OnboardingStep.Listen && stored <= (int)OnboardingStep.Voice ? (OnboardingStep)stored : null;

    /// <summary>The stored value the removed corner tour step had; an install saved on it has finished the path.</summary>
    public const int OldCornerStep = 5;

    /// <summary>True when a saved value is the removed corner step: the install was killed on it, so setup ends normally at the next start (the stored value is cleared and the intro's end is counted).</summary>
    public static bool IsOldCornerStep(int stored) => stored == OldCornerStep;
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
