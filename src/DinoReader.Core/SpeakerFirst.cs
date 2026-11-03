namespace DinoReader.Core;

/// <summary>
/// Speaker first (Slice 254). Persian and Arabic say who speaks before what is said ("پیم گفت: ..."), so a translated line
/// written in the source's order, quote then dialogue tag (then the rest of the quote), is regrouped after translation to tag, quote, quote.
/// Only the order of the pieces changes: every piece keeps its text, speaker and source range, and a tag moved in front of its quote ends with a colon
/// instead of a full stop. A tag never moves in front of a quote piece that lies in an earlier sentence than the tag, so the highlight sentence never goes backwards.
/// </summary>
public static class SpeakerFirst
{
    /// <summary>Persian: «X گفت:» comes first (Slice 254, checked against its translations).</summary>
    public const string Persian = "fa";

    /// <summary>Arabic: «قال X:» comes first (Slice 254).</summary>
    public const string Arabic = "ar";

    private static readonly HashSet<string> Languages = [Persian, Arabic];

    private const char Colon = ':';
    private const string TagStops = ".،,؛;。۔";
    private const string OpenEnded = "!?؟…";
    private const string ContinuingMarks = ",،";
    private const int MaxTagWords = 6;
    private static readonly char[] WordEdges = [.. ".,;:!?\"'“”‘’"];
    private static readonly char[] TagEndings = [.. ".,;:!?…\"'“”‘’،؛؟"];
    private static readonly char[] ClauseBreaks = [',', ';', ':', '—', '–', '(', ')', '،', '؛'];
    private static readonly HashSet<string> Conjunctions = ["and", "but", "then", "while", "so", "as", "when", "until", "because", "og", "men", "und", "aber"];

    /// <summary>One piece of the regrouped list: the index it had, and whether it is a tag that now introduces its quote (so its stop becomes a colon).</summary>
    public sealed record Move(int Index, bool EndsWithColon = false);

    /// <summary>Whether a read in <paramref name="language"/> puts the speaker first.</summary>
    public static bool Applies(string? language) => language is not null && Languages.Contains(UiLanguage.Resolve(language));

    /// <summary>
    /// Whether a read of a story written in <paramref name="storyLanguage"/> is regrouped speaker first when spoken in <paramref name="spokenLanguage"/>:
    /// the language puts the speaker first, the read really is a translation, and <paramref name="translated"/> (a failed or skipped translation leaves the story as it was).
    /// The one decision the app and the sentence dump both use.
    /// </summary>
    public static bool Regroups(string? spokenLanguage, string? storyLanguage, bool translated) =>
        translated && Applies(spokenLanguage) && UiLanguage.Resolve(spokenLanguage) != UiLanguage.Resolve(storyLanguage);

    /// <summary>The pieces of <paramref name="pieces"/> (a read of the story whose text and sentences are given) regrouped speaker first; the same list for any other language.</summary>
    public static IReadOnlyList<SplitSegment> Regroup(IReadOnlyList<SplitSegment> pieces, string? language, string sourceText, IReadOnlyList<SentenceSpan> sentences)
    {
        if (!Applies(language))
        {
            return pieces;
        }

        return Apply(pieces, Plan(pieces, language, sourceText, sentences), withColon: true, sourceText);
    }

    /// <summary>The story's own pieces in the order they are read in <paramref name="language"/> (texts untouched), so the animal sounds count a line the way the read hears it.</summary>
    public static IReadOnlyList<SplitSegment> SourceOrder(IReadOnlyList<SplitSegment> pieces, string? language, string sourceText, IReadOnlyList<SentenceSpan> sentences) =>
        Applies(language) ? Apply(pieces, Plan(pieces, language, sourceText, sentences), withColon: false) : pieces;

    /// <summary>The new order of the pieces: every index once, a tag first before the quote it belongs to. Identity when the language does not put the speaker first.</summary>
    public static IReadOnlyList<Move> Plan(IReadOnlyList<SplitSegment> pieces, string? language, string sourceText, IReadOnlyList<SentenceSpan> sentences)
    {
        var moves = new List<Move>(pieces.Count);
        int i = 0;
        while (i < pieces.Count)
        {
            i = Applies(language) && sentences.Count > 0
                ? AddGroup(moves, pieces, i, sourceText, sentences)
                : AddPlain(moves, i, i);
        }

        return moves;
    }

    /// <summary>
    /// The pieces in the order of <paramref name="order"/>; with <paramref name="withColon"/> a moved tag ends with a colon (false keeps every text as it was).
    /// A tag whose text is still the story's own words (no translation came back for it) keeps its text when <paramref name="sourceText"/> is given: no colon on a fallback.
    /// </summary>
    public static IReadOnlyList<SplitSegment> Apply(IReadOnlyList<SplitSegment> pieces, IReadOnlyList<Move> order, bool withColon, string? sourceText = null)
    {
        return order.Select(m => withColon && m.EndsWithColon && !IsSourceText(pieces[m.Index], sourceText) ? WithColon(pieces[m.Index]) : pieces[m.Index]).ToList();
    }

    // A quote (its pieces, then a plain tag that finishes its sentence) is regrouped; any other piece stays where it is.
    private static int AddGroup(List<Move> moves, IReadOnlyList<SplitSegment> pieces, int first, string text, IReadOnlyList<SentenceSpan> sentences)
    {
        if (pieces[first].Segment.Kind != SegmentKind.Quote)
        {
            return AddPlain(moves, first, first);
        }

        int quoteEnd = SameSpeakerEnd(pieces, first);
        int tagEnd = TagEnd(pieces, quoteEnd, sentences);
        if (tagEnd == quoteEnd || !IsPlainTag(pieces, quoteEnd, tagEnd, text) || QuoteStartsEarlier(pieces, first, quoteEnd, sentences))
        {
            return AddPlain(moves, first, quoteEnd);
        }

        return AddRegrouped(moves, pieces, first, quoteEnd, tagEnd, text, sentences);
    }

    // The tags in front, the quotes after them: "A," said Pim, "B," said Pim reads «Pim said, Pim said: A B», every piece kept.
    private static int AddRegrouped(List<Move> moves, IReadOnlyList<SplitSegment> pieces, int first, int quoteEnd, int tagEnd, string text, IReadOnlyList<SentenceSpan> sentences)
    {
        int tagSentence = StorySentences.IndexAt(sentences, pieces[quoteEnd + 1].Segment.SourceStart);
        var tags = new List<int>(Enumerable.Range(quoteEnd + 1, tagEnd - quoteEnd));
        var quotes = new List<int>(Enumerable.Range(first, quoteEnd - first + 1));
        int last = tagEnd;
        while (EndsWithComma(pieces[last].Segment, text) && FollowingQuote(pieces, last, first) is { } next)
        {
            int nextEnd = SameSpeakerEnd(pieces, next);
            quotes.AddRange(Enumerable.Range(next, nextEnd - next + 1));
            last = nextEnd;
            int secondTag = TagEnd(pieces, nextEnd, sentences);
            if (secondTag == nextEnd || !IsPlainTag(pieces, nextEnd, secondTag, text) || StorySentences.IndexAt(sentences, pieces[nextEnd + 1].Segment.SourceStart) != tagSentence)
            {
                break;
            }

            tags.AddRange(Enumerable.Range(nextEnd + 1, secondTag - nextEnd));
            last = secondTag;
        }

        moves.AddRange(tags.Select(k => new Move(k, EndsWithColon: k == tags[^1])));
        moves.AddRange(quotes.Select(k => new Move(k)));
        return last + 1;
    }

    // The quote of the same speaker that follows the tag directly, or null.
    private static int? FollowingQuote(IReadOnlyList<SplitSegment> pieces, int tagEnd, int first)
    {
        int next = tagEnd + 1;
        bool follows = next < pieces.Count
            && pieces[next].Segment.Kind == SegmentKind.Quote
            && pieces[next].Segment.CharacterId == pieces[first].Segment.CharacterId;
        return follows ? next : null;
    }

    // A quote the splitter cut across sentences, with its tag in a later one, stays as written: a tag in the middle of the quote, or in front of its first half, would send the highlight back.
    private static bool QuoteStartsEarlier(IReadOnlyList<SplitSegment> pieces, int first, int quoteEnd, IReadOnlyList<SentenceSpan> sentences)
    {
        int tagSentence = StorySentences.IndexAt(sentences, pieces[quoteEnd + 1].Segment.SourceStart);
        return StorySentences.IndexAt(sentences, pieces[first].Segment.SourceStart) < tagSentence;
    }

    // Only "said Pim" moves: a tag with an action clause ("said the wolf, and he huffed and puffed", "and ran home") stays where it was written.
    private static bool IsPlainTag(IReadOnlyList<SplitSegment> pieces, int quoteEnd, int tagEnd, string text)
    {
        int start = Math.Min(pieces[quoteEnd + 1].Segment.SourceStart, text.Length);
        int end = Math.Clamp(pieces[tagEnd].Segment.SourceEnd, start, text.Length);
        string written = text[start..end].Trim().TrimEnd(TagEndings);
        string[] words = written.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return words.Length is > 0 and <= MaxTagWords
            && written.IndexOfAny(ClauseBreaks) < 0
            && !words.Any(w => Conjunctions.Contains(w.Trim(WordEdges).ToLowerInvariant()));
    }

    private static int AddPlain(List<Move> moves, int from, int to)
    {
        moves.AddRange(Enumerable.Range(from, to - from + 1).Select(k => new Move(k)));
        return to + 1;
    }

    // The last quote piece of the same speaker that follows without anything between.
    private static int SameSpeakerEnd(IReadOnlyList<SplitSegment> pieces, int first)
    {
        int end = first;
        while (end + 1 < pieces.Count && pieces[end + 1].Segment.Kind == SegmentKind.Quote && pieces[end + 1].Segment.CharacterId == pieces[first].Segment.CharacterId)
        {
            end++;
        }

        return end;
    }

    // The narration pieces that finish the quote's own sentence are its dialogue tag (the same rule the animal sounds use).
    private static int TagEnd(IReadOnlyList<SplitSegment> pieces, int quoteEnd, IReadOnlyList<SentenceSpan> sentences)
    {
        int sentence = StorySentences.IndexAt(sentences, pieces[quoteEnd].Segment.SourceEnd - 1);
        int last = quoteEnd;
        while (sentence >= 0 && last + 1 < pieces.Count && pieces[last + 1].Segment.Kind == SegmentKind.Narration && pieces[last + 1].Segment.SourceStart < sentences[sentence].End)
        {
            last++;
        }

        return last;
    }

    private static bool EndsWithComma(Segment tag, string text)
    {
        int end = Math.Min(tag.SourceEnd, text.Length);
        string written = text[Math.Min(tag.SourceStart, end)..end].TrimEnd();
        return written.Length > 0 && ContinuingMarks.Contains(written[^1]);
    }

    // No translation came back for the piece, so it still reads as the story's own words.
    private static bool IsSourceText(SplitSegment piece, string? sourceText)
    {
        if (sourceText is null)
        {
            return false;
        }

        int start = Math.Min(piece.Segment.SourceStart, sourceText.Length);
        int end = Math.Clamp(piece.Segment.SourceEnd, start, sourceText.Length);
        return Collapse(piece.Segment.Text) == Collapse(sourceText[start..end]);
    }

    private static string Collapse(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static SplitSegment WithColon(SplitSegment piece)
    {
        string text = piece.Segment.Text.TrimEnd();
        if (text.Length == 0 || OpenEnded.Contains(text[^1]) || text[^1] == Colon)
        {
            return piece;
        }

        string bare = TagStops.Contains(text[^1]) ? text[..^1].TrimEnd() : text;
        return piece with { Segment = piece.Segment with { Text = bare + Colon } };
    }
}
