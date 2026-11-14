namespace DinoReader.Core;

/// <summary>How a grown-up records a book (Slice 279): a line at a time for the characters they tick, or a whole page (cut into parts when it is long).</summary>
public enum RecordMode
{
    PerCharacter,
    WholePage,
}

/// <summary>One sentence of a page as the Record screen sees it: <see cref="Index"/> counts from 0 on the <see cref="Page"/>, <see cref="Speaker"/> is the character who says it
/// (<see cref="RecordSplit.Storyteller"/> for the narration), and <see cref="EndsParagraph"/> says a paragraph ends after it.</summary>
public sealed record RecordSentence(int Page, int Index, string Text, string Speaker, bool EndsParagraph);

/// <summary>A character the recording can be done for; <see cref="Id"/> is <see cref="RecordSplit.Storyteller"/> for the narration.</summary>
public sealed record RecordSpeaker(string Id, string Name);

/// <summary>One thing to record: the sentences of one page it covers (<see cref="Key"/>), who says it and its words as the book shows them.</summary>
public sealed record RecordPiece(StoryClipKey Key, string Speaker, string Text);

/// <summary>
/// The pure split of a book into the pieces a grown-up records (Slice 279, work items 1 and 2). Per character: the lines of the ticked speakers in story order, where a line is a run of
/// neighbouring sentences on one page said by the same speaker (so a stretch of narration between two spoken lines is one line). Whole page: a page's paragraphs packed up to a height limit,
/// or its sentences when one paragraph alone is too tall. The height comes from the caller (the app measures the words in the card; a test passes fake heights).
/// </summary>
public static class RecordSplit
{
    /// <summary>The speaker id of the narration.</summary>
    public const string Storyteller = "";

    private const string SentenceJoin = " ";
    private const string ParagraphJoin = "\n\n";

    /// <summary>The lines of the speakers in <paramref name="ticked"/>, in story order. With <paramref name="heightOf"/> a line taller than <paramref name="maxHeight"/> is cut at the end of a sentence
    /// (the card never covers more than half the screen); without it every line is whole.</summary>
    public static IReadOnlyList<RecordPiece> ByCharacter(IReadOnlyList<RecordSentence> sentences, IReadOnlySet<string> ticked, Func<string, double>? heightOf = null, double maxHeight = double.MaxValue) =>
        RunLists(sentences).Where(run => ticked.Contains(run[0].Speaker))
            .SelectMany(run => Fit(run, heightOf, maxHeight)).Where(HasWords)
            .ToList();

    /// <summary>How many lines each speaker has in the book, whoever is ticked.</summary>
    public static IReadOnlyDictionary<string, int> LineCounts(IReadOnlyList<RecordSentence> sentences) =>
        RunLists(sentences).GroupBy(run => run[0].Speaker).ToDictionary(g => g.Key, g => g.Count());

    /// <summary>The pieces of a whole-page recording: every page in order, each cut into parts that stay within <paramref name="maxHeight"/> by <paramref name="heightOf"/>.
    /// A part ends at the end of a paragraph; a paragraph alone taller than the limit ends at a sentence. A sentence alone taller than the limit is a part of its own.</summary>
    public static IReadOnlyList<RecordPiece> WholePages(IReadOnlyList<RecordSentence> sentences, Func<string, double> heightOf, double maxHeight)
    {
        var pieces = new List<RecordPiece>();
        foreach (IGrouping<int, RecordSentence> page in sentences.GroupBy(s => s.Page).OrderBy(g => g.Key))
        {
            pieces.AddRange(PackPage(page.OrderBy(s => s.Index).ToList(), heightOf, maxHeight));
        }

        return pieces.Where(HasWords).ToList();
    }

    // A translated book can hold sentences with no words (shown with an earlier one): a piece made only of them has nothing to read out, so it is never a card, a checklist entry or a silent clip (Slice 286).
    private static bool HasWords(RecordPiece piece) => !string.IsNullOrWhiteSpace(piece.Text);

    /// <summary>How many pieces cover page <paramref name="page"/>.</summary>
    public static int PartsOf(IReadOnlyList<RecordPiece> pieces, int page) => pieces.Count(p => p.Key.Page == page);

    /// <summary>The 1-based number of <paramref name="piece"/> among the pieces of its page.</summary>
    public static int PartNumber(IReadOnlyList<RecordPiece> pieces, RecordPiece piece) =>
        pieces.Where(p => p.Key.Page == piece.Key.Page).ToList().IndexOf(piece) + 1;

    private static IEnumerable<List<RecordSentence>> RunLists(IReadOnlyList<RecordSentence> sentences)
    {
        var run = new List<RecordSentence>();
        foreach (RecordSentence sentence in sentences)
        {
            if (run.Count > 0 && !Continues(run[^1], sentence))
            {
                yield return run;
                run = [];
            }

            run.Add(sentence);
        }

        if (run.Count > 0)
        {
            yield return run;
        }
    }

    private static bool Continues(RecordSentence last, RecordSentence next) =>
        last.Page == next.Page && last.Speaker == next.Speaker && next.Index == last.Index + 1;

    private static RecordPiece PieceOf(IReadOnlyList<RecordSentence> run) =>
        new(new StoryClipKey(run[0].Page, run[0].Index, run[^1].Index, StoryClipKey.NoLanguage), run[0].Speaker, TextOf(run));

    /// <summary>The words of <paramref name="run"/> as the book shows them: sentences side by side, a blank line where a paragraph ends.</summary>
    public static string TextOf(IReadOnlyList<RecordSentence> run)
    {
        var text = new System.Text.StringBuilder();
        List<RecordSentence> shown = run.Where(s => s.Text.Length > 0).ToList(); // a translated sentence whose words were shown with an earlier one has none (Slice 286)
        for (int i = 0; i < shown.Count; i++)
        {
            text.Append(shown[i].Text);
            if (i < shown.Count - 1)
            {
                text.Append(shown[i].EndsParagraph ? ParagraphJoin : SentenceJoin);
            }
        }

        return text.ToString();
    }

    private static List<RecordPiece> PackPage(List<RecordSentence> page, Func<string, double> heightOf, double maxHeight)
    {
        var pieces = new List<RecordPiece>();
        var current = new List<RecordSentence>();
        foreach (List<RecordSentence> paragraph in Paragraphs(page))
        {
            if (current.Count > 0 && !Fits(current.Concat(paragraph), heightOf, maxHeight))
            {
                Flush(pieces, current);
            }

            if (Fits(paragraph, heightOf, maxHeight))
            {
                current.AddRange(paragraph);
                continue;
            }

            Flush(pieces, current);
            pieces.AddRange(PackSentences(paragraph, heightOf, maxHeight));
        }

        Flush(pieces, current);
        return pieces;
    }

    private static bool Fits(IEnumerable<RecordSentence> candidate, Func<string, double> heightOf, double maxHeight)
    {
        double height = heightOf(TextOf(candidate.ToList()));
        return height <= maxHeight;
    }

    // The sentences gathered so far become one piece (its key and words are taken at once, so the list can be reused).
    private static void Flush(List<RecordPiece> pieces, List<RecordSentence> current)
    {
        if (current.Count > 0)
        {
            pieces.Add(PieceOf(current));
            current.Clear();
        }
    }

    private static List<RecordPiece> Fit(List<RecordSentence> run, Func<string, double>? heightOf, double maxHeight) =>
        heightOf is null || heightOf(TextOf(run)) <= maxHeight ? [PieceOf(run)] : PackSentences(run, heightOf, maxHeight);

    // Sentences packed greedily up to the limit; a sentence alone over it is a piece of its own.
    private static List<RecordPiece> PackSentences(List<RecordSentence> sentences, Func<string, double> heightOf, double maxHeight)
    {
        var pieces = new List<RecordPiece>();
        var current = new List<RecordSentence>();
        foreach (RecordSentence sentence in sentences)
        {
            if (current.Count > 0 && heightOf(TextOf(current.Append(sentence).ToList())) > maxHeight)
            {
                pieces.Add(PieceOf(current));
                current = [];
            }

            current.Add(sentence);
        }

        if (current.Count > 0)
        {
            pieces.Add(PieceOf(current));
        }

        return pieces;
    }

    private static IEnumerable<List<RecordSentence>> Paragraphs(List<RecordSentence> page)
    {
        var paragraph = new List<RecordSentence>();
        foreach (RecordSentence sentence in page)
        {
            paragraph.Add(sentence);
            if (sentence.EndsParagraph)
            {
                yield return paragraph;
                paragraph = [];
            }
        }

        if (paragraph.Count > 0)
        {
            yield return paragraph;
        }
    }
}
