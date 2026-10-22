using ReadTheStupidText.Application.Reading;

namespace DinoReader.Core;

/// <summary>
/// Splits story text into the sentences the reading screen highlights (Decision 79: the sentence
/// is the unit, never a word). A sentence ends at its terminator (a title like "Mr." does not end
/// one) plus any closing quote marks, at a line break that <see cref="LineReflow"/> reads as a real
/// break (a blank line, a heading, a list item; never a soft wrap, Slice 76), and between two quotes that follow each
/// other ("Go!" "No!"). A quote and its attribution stay together: "Hi," said Peter. is one
/// sentence, so the box never stops mid-sentence. After a closing quote the sentence ends unless the
/// next word is an attribution: it starts lowercase, or in a script without case (Farsi, Arabic),
/// where an attribution cannot be told from a new sentence and the safe side is to keep them in one
/// box. A quote that ends with a comma, or that an attribution follows, is never split inside.
/// The Arabic question mark and the guillemets are understood here for every language; the
/// attributor's own tables for Farsi arrive with Slice 66.
/// </summary>
public static class StorySentences
{
    private const int MaxWordsBeforeVerb = 3;
    private const char ArabicQuestionMark = '\u061F';
    private const char GuillemetOpen = '\u00AB';
    private const char GuillemetClose = '\u00BB';

    public static IReadOnlyList<SentenceSpan> Build(string text, LanguageRules rules)
    {
        LineBreakMap lineBreaks = LineReflow.BreakMap(text);
        return Build(text, rules, lineBreaks);
    }

    /// <summary>As <see cref="Build(string, LanguageRules)"/>, with the line breaks decided by the caller
    /// (for example from the page's OCR line boxes).</summary>
    public static IReadOnlyList<SentenceSpan> Build(string text, LanguageRules rules, LineBreakMap lineBreaks)
    {
        var spans = new List<SentenceSpan>();
        int start = SkipWhitespace(text, 0);
        int i = start;
        var quote = new QuoteState(lineBreaks.SentenceBreaks);
        while (i < text.Length)
        {
            quote.Advance(text, i, rules);
            int end = EndAt(text, i, rules, quote, lineBreaks.SentenceBreaks);
            if (end < 0)
            {
                i++;
                continue;
            }

            spans.Add(new SentenceSpan(start, end));
            start = i = SkipWhitespace(text, end);
        }

        AddTail(spans, text, start);
        return spans;
    }

    /// <summary>The index of the sentence holding <paramref name="position"/>, the next one when the
    /// position is in the gap between two, the last one past the end; -1 when there are none.</summary>
    public static int IndexAt(IReadOnlyList<SentenceSpan> sentences, int position)
    {
        int low = 0;
        int high = sentences.Count - 1;
        int found = sentences.Count - 1;
        while (low <= high)
        {
            int mid = (low + high) / 2;
            if (position < sentences[mid].End)
            {
                found = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        return found;
    }

    // The exclusive end of a sentence that finishes at index i, or -1 when none does.
    private static int EndAt(string text, int i, LanguageRules rules, QuoteState quote, IReadOnlySet<int> lineBreaks)
    {
        if (text[i] == '\n')
        {
            return lineBreaks.Contains(i) ? TrimEnd(text, i) : -1;
        }

        if (!IsTerminator(text[i], rules) || (i + 1 < text.Length && IsTerminator(text[i + 1], rules)))
        {
            return -1;
        }

        int end = i + 1;
        while (end < text.Length && IsClosingQuote(text[end], rules))
        {
            end++;
        }

        bool closedQuote = end > i + 1;
        if (!AtEdge(text, end, rules) || (!closedQuote && StopsHere(text, i, rules, quote)))
        {
            return -1;
        }

        bool attribution = (closedQuote && AttributionFollows(text, end, rules)) || DashTagFollows(text, end, rules);
        return attribution ? -1 : end;
    }

    // Dash dialogue: `— Hello! — said Peter.` keeps the tag, a lowercase word after the second dash, in the sentence.
    private static bool DashTagFollows(string text, int end, LanguageRules rules)
    {
        int dash = SkipBlanks(text, end);
        if (dash >= text.Length || !rules.IsDialogueDash(text, dash)) return false;
        int word = SkipBlanks(text, dash + 1);
        return word < text.Length && char.IsLower(text[word]);
    }

    private static int SkipBlanks(string text, int i)
    {
        while (i < text.Length && text[i] != '\n' && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        return i;
    }

    // A full stop after a title ("Mr."), or a terminator inside a quote that goes on into its
    // attribution, ends nothing.
    private static bool StopsHere(string text, int i, LanguageRules rules, QuoteState quote) =>
        quote.HoldsTogether || (text[i] == '.' && SentenceBoundaries.FollowsTitle(text, i, rules));

    private static bool AtEdge(string text, int end, LanguageRules rules) =>
        rules.Script == ScriptStyle.Unspaced || end >= text.Length || char.IsWhiteSpace(text[end]);

    // After a closing quote the next words are an attribution when the first word starts lowercase (or
    // in an uncased script), or a speech verb comes within the first three words ("Peter said.").
    private static bool AttributionFollows(string text, int end, LanguageRules rules)
    {
        int next = SkipWhitespace(text, end);
        if (next >= text.Length)
        {
            return false;
        }

        return (char.IsLetter(text[next]) && !char.IsUpper(text[next])) || SpeechVerbSoon(text, next, rules);
    }

    private static bool SpeechVerbSoon(string text, int from, LanguageRules rules)
    {
        int i = from;
        for (int words = 0; words < MaxWordsBeforeVerb && i < text.Length; words++)
        {
            int start = i;
            while (i < text.Length && (char.IsLetter(text[i]) || text[i] == '\u0027'))
            {
                i++;
            }

            if (i == start || rules.SpeechVerbs.Contains(text[start..i]))
            {
                return i > start;
            }

            i = SkipWhitespace(text, i);
        }

        return false;
    }

    private static bool IsTerminator(char c, LanguageRules rules) =>
        rules.SentenceTerminators.Contains(c) || c == ArabicQuestionMark;

    private static bool IsOpeningQuote(char c, LanguageRules rules) => rules.IsOpeningQuote(c) || c == GuillemetOpen;

    private static bool IsClosingQuote(char c, LanguageRules rules) => rules.IsClosingQuote(c) || c == GuillemetClose;

    // Which quote the scan is in, found by looking ahead to its close on the same line.
    private sealed class QuoteState
    {
        private readonly IReadOnlySet<int> _lineBreaks;
        private int _close = -1;

        public QuoteState(IReadOnlySet<int> lineBreaks)
        {
            _lineBreaks = lineBreaks;
        }

        /// <summary>True inside a quote that ends with a comma or runs on into an attribution: one sentence.</summary>
        public bool HoldsTogether { get; private set; }

        public void Advance(string text, int i, LanguageRules rules)
        {
            if (i > _close)
            {
                _close = -1;
                HoldsTogether = false;
            }

            if (_close < 0 && IsOpeningQuote(text[i], rules))
            {
                _close = FindClose(text, i, rules, _lineBreaks);
                HoldsTogether = _close >= 0 && RunsOn(text, _close, rules);
            }
        }

        private static int FindClose(string text, int open, LanguageRules rules, IReadOnlySet<int> lineBreaks)
        {
            for (int k = open + 1; k < text.Length; k++)
            {
                if (text[k] == '\n' && lineBreaks.Contains(k))
                {
                    return -1;
                }

                if (IsClosingQuote(text[k], rules))
                {
                    return k;
                }

                if (IsOpeningQuote(text[k], rules))
                {
                    return -1;
                }
            }

            return -1;
        }

        private static bool RunsOn(string text, int close, LanguageRules rules)
        {
            char last = text[close - 1];
            return last == ',' || (IsTerminator(last, rules) && AttributionFollows(text, close + 1, rules));
        }
    }

    private static void AddTail(List<SentenceSpan> spans, string text, int start)
    {
        int end = TrimEnd(text, text.Length);
        if (end > start)
        {
            spans.Add(new SentenceSpan(start, end));
        }
    }

    private static int SkipWhitespace(string text, int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        return i;
    }

    private static int TrimEnd(string text, int end)
    {
        while (end > 0 && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return end;
    }
}
