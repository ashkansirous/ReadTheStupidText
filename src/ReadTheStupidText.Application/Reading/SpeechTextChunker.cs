using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Splits text for the neural engine so long reads synthesize as several small
/// chunks (generated concurrently, played in order) instead of one slow call.
/// Text at or under <see cref="SingleChunkMax"/> stays whole; longer text is split
/// on paragraph boundaries, then on sentences, then — only for a single oversized
/// sentence — on word boundaries, so no chunk greatly exceeds <see cref="TargetChunkMax"/>.
/// The very first chunk is biased to a short head (a <see cref="HeadPolicy"/>: by default two
/// sentences and at most 120 characters) so playback starts after a short first synthesis
/// rather than a whole paragraph (time-to-first-audio).
/// </summary>
internal static partial class SpeechTextChunker
{
    private const int SingleChunkMax = 200;
    private const int TargetChunkMax = 240;

    // Slice 214: the first piece of a read is about 8 spoken words (about 2 s of audio). Cost counts one per spaced word and
    // CjkCharCost per unspaced Chinese/Japanese character; a clause cut must leave at least FirstPieceMinClauseCost.
    private const double FirstPieceMaxCost = 8;
    private const double FirstPieceMinClauseCost = 3;
    // The second piece is only what is left of the cut sentence, bounded so it is synthesised while piece 1 plays, even at 2x.
    private const double SecondPieceMaxCost = 16;
    private const double CjkCharCost = 0.5;
    private const string ClauseMarks = ".!?,;:–—،؛؟۔、。，！：；？";
    private const string ClosingMarks = "\"'”’)]»」』）";
    private const char IdeographicSpace = '\u3000';
    private const char KanaStart = '\u3040';
    private const char KanaEnd = '\u30FF';
    private const char CjkIdeographStart = '\u3400';
    private const char CjkIdeographEnd = '\u9FFF';
    private const char CjkCompatStart = '\uF900';
    private const char CjkCompatEnd = '\uFAFF';
    private const char FullwidthStart = '\uFF00';
    private const char FullwidthEnd = '\uFF60';
    private const char HalfwidthKatakanaStart = '\uFF66';
    private const char HalfwidthKatakanaEnd = '\uFF9F';
    // Scripts written without spaces between words (Thai, Lao, Burmese, Khmer): a cut falls between characters, so about four characters a word.
    private const char BurmeseStart = '\u1000';
    private const char BurmeseEnd = '\u109F';
    private const char KhmerStart = '\u1780';
    private const char KhmerEnd = '\u17FF';
    private const char ThaiStart = '\u0E00';
    private const char LaoEnd = '\u0EFF';
    private const double SeaCharCost = 0.25;
    private const char ThaiLeadingVowelStart = '\u0E40';
    private const char ThaiLeadingVowelEnd = '\u0E44';
    private const char LaoLeadingVowelStart = '\u0EC0';
    private const char LaoLeadingVowelEnd = '\u0EC4';
    private const char BurmeseVirama = '\u1039';
    private const char KhmerCoeng = '\u17D2';
    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "mr.", "mrs.", "ms.", "dr.", "prof.", "st.", "e.g.", "i.e.", "etc.", "vs.",
    };

    /// <summary>How long the first chunk may be: at most <see cref="MaxSentences"/> sentences
    /// and <see cref="MaxChars"/> characters. The first chunk is what the listener waits on, so
    /// the default stays short; a reader that has the sentence cache (Slice 47) is prepared
    /// ahead and passes <see cref="Cached"/>.</summary>
    public readonly record struct HeadPolicy(int MaxSentences, int MaxChars, bool ShortFirstPiece = false)
    {
        /// <summary>Two sentences, at most 120 characters (the reader without a cache): Decision 147 settles
        /// the earlier one-versus-two mismatch with Decision 66 at two.</summary>
        public static HeadPolicy Default { get; } = new(2, 120);

        /// <summary>Two sentences, at most 200 characters: the head is split back into
        /// sentences for the cache, which is prepared ahead.</summary>
        public static HeadPolicy Cached { get; } = new(2, 200);

        /// <summary>The same policy for the read itself (Slice 214): the first piece is also cut to about 8 spoken words, so a long
        /// first sentence does not make the first synthesis slow. RTST text reads only (Dino keeps its policy); preparing a head ahead uses the same policy,
        /// so the prepared units are the read's first units.</summary>
        public HeadPolicy WithShortFirstPiece() => this with { ShortFirstPiece = true };
    }

    /// <summary>
    /// Splits <paramref name="text"/> into chunks. A <paramref name="firstChunkHint"/> (the early start's prepared head, Slice 139)
    /// becomes the first chunk only when <see cref="AcceptsHint"/> allows it; without one, or when it is not accepted, the
    /// result is exactly the plain split.
    /// </summary>
    public static IReadOnlyList<string> Split(
        string text, bool biasFirstChunk = true, HeadPolicy? head = null, string? firstChunkHint = null)
    {
        string trimmed = LineReflow.ReflowText(text).Trim();
        if (BlankText.IsBlank(trimmed))
        {
            // Nothing to speak: an empty chunk would reach the engine and fail there (Slice 176).
            return Array.Empty<string>();
        }

        if (firstChunkHint is not null && AcceptsHint(trimmed, firstChunkHint, head ?? HeadPolicy.Default))
        {
            return SplitAfterFirst(firstChunkHint, trimmed, head);
        }

        return SplitPlain(trimmed, biasFirstChunk, head);
    }

    /// <summary>
    /// Whether <paramref name="hint"/> may be the first chunk of <paramref name="trimmed"/> (reflowed and trimmed): a prefix of it
    /// that ends exactly at a sentence boundary, within the policy's sentence count and character cap.
    /// </summary>
    internal static bool AcceptsHint(string trimmed, string hint, HeadPolicy policy)
    {
        if (hint.Length == 0 || hint.Length > policy.MaxChars || !trimmed.StartsWith(hint, StringComparison.Ordinal))
        {
            return false;
        }

        int stop = hint.Length;
        while (stop < trimmed.Length && char.IsWhiteSpace(trimmed[stop]))
        {
            stop++;
        }

        // Look one visible character past the hint, so a hint cut inside a sentence is not mistaken for a sentence end.
        List<int> ends = SentenceEnds(trimmed[..Math.Min(trimmed.Length, stop + 1)]);
        int index = ends.IndexOf(hint.Length);
        return index >= 0 && index < policy.MaxSentences;
    }

    // The early start prepared this head (Slice 139): it is the first chunk, so the first units are the prepared ones; the rest
    // is chunked on its own and is not biased (the listener no longer waits on it).
    private static IReadOnlyList<string> SplitAfterFirst(string first, string trimmed, HeadPolicy? head)
    {
        // The hint is cut like any first chunk (Slice 214), through the same function the early start used to prepare it.
        var chunks = new List<string>(head?.ShortFirstPiece == true ? ShortenFirstPiece(new[] { first }) : new[] { first });
        string rest = trimmed[first.Length..].Trim();
        if (rest.Length > 0)
        {
            chunks.AddRange(SplitPlain(rest, biasFirstChunk: false, head));
        }

        return chunks;
    }

    private static IReadOnlyList<string> SplitPlain(string trimmed, bool biasFirstChunk, HeadPolicy? head)
    {
        IReadOnlyList<string> chunks = SplitUnbiasedTail(trimmed, biasFirstChunk, head);
        return biasFirstChunk && head?.ShortFirstPiece == true ? ShortenFirstPiece(chunks) : chunks;
    }

    private static IReadOnlyList<string> SplitUnbiasedTail(string trimmed, bool biasFirstChunk, HeadPolicy? head)
    {
        if (trimmed.Length <= SingleChunkMax)
        {
            return new[] { trimmed };
        }

        var chunks = new List<string>();
        foreach (string paragraph in SplitParagraphs(trimmed))
        {
            AppendParagraph(paragraph, chunks);
        }

        if (chunks.Count == 0)
        {
            return new[] { trimmed };
        }

        if (biasFirstChunk)
        {
            BiasFirstChunkToShortHead(chunks, head ?? HeadPolicy.Default);
        }

        return chunks;
    }

    // Slice 214: first audio scales with the first piece's audio length (about 0.5 s + 0.26 s per audio second on the S21), so the
    // first piece is cut to about FirstPieceMaxCost words (about 2 s of speech) when it is longer.
    private static IReadOnlyList<string> ShortenFirstPiece(IReadOnlyList<string> chunks)
    {
        string first = chunks[0];
        int cut = FirstPieceCut(first, FirstPieceMaxCost);
        if (cut <= 0 || cut >= first.Length || first[cut..].Trim().Length == 0)
        {
            return chunks;
        }

        var result = new List<string>(chunks.Count + 2) { first[..cut].Trim() };
        string rest = first[cut..].Trim();
        int second = FirstPieceCut(rest, SecondPieceMaxCost);
        if (second > 0 && second < rest.Length && rest[second..].Trim().Length > 0)
        {
            result.Add(rest[..second].Trim());
            rest = rest[second..].Trim();
        }

        result.Add(rest);
        result.AddRange(chunks.Skip(1));
        return result;
    }

    /// <summary>Where the first piece of <paramref name="text"/> ends: the whole text (or -1) when it is within about
    /// <paramref name="maxCost"/> spoken words; otherwise just after the last clause end (sentence end, comma, semicolon,
    /// colon, dash) that leaves at least <see cref="FirstPieceMinClauseCost"/> words, else at the last word boundary within the cap.
    /// Unspaced Chinese and Japanese cost <see cref="CjkCharCost"/> per character.</summary>
    internal static int FirstPieceCut(string text, double maxCost = FirstPieceMaxCost)
    {
        double cost = 0;
        int lastWord = -1;
        int lastClause = -1;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool startsWord = !char.IsWhiteSpace(c) && (i == 0 || char.IsWhiteSpace(text[i - 1]));
            double unitCost = IsSoutheastAsianScript(c) ? SeaCharCost : IsUnspacedScript(c) ? CjkCharCost : (startsWord ? 1 : 0);
            if (cost + unitCost > maxCost && lastWord > 0)
            {
                return lastClause > 0 ? lastClause : lastWord;
            }

            cost += unitCost;
            if (IsWordEnd(text, i))
            {
                lastWord = i + 1;
            }

            int clauseEnd = cost >= FirstPieceMinClauseCost ? ClauseEndAfter(text, i) : -1;
            if (clauseEnd > 0)
            {
                lastClause = clauseEnd;
            }
        }

        return -1;
    }

    private static bool IsSoutheastAsianScript(char c) =>
        c is >= ThaiStart and <= LaoEnd or >= BurmeseStart and <= BurmeseEnd or >= KhmerStart and <= KhmerEnd;

    private static bool IsUnspacedScript(char c) =>
        c is >= KanaStart and <= KanaEnd or >= CjkIdeographStart and <= CjkIdeographEnd or >= CjkCompatStart and <= CjkCompatEnd
            or >= HalfwidthKatakanaStart and <= HalfwidthKatakanaEnd
        || IsSoutheastAsianScript(c)
        || (c >= FullwidthStart && c <= FullwidthEnd && !ClauseMarks.Contains(c));

    private static bool IsWordEnd(string text, int i) =>
        !char.IsHighSurrogate(text[i]) && !char.IsWhiteSpace(text[i])
        && (i + 1 == text.Length || char.IsWhiteSpace(text[i + 1]) || IsUnspacedScript(text[i + 1]) || IsUnspacedScript(text[i]))
        && !CannotEndPiece(text, i);

    // A cut never falls inside a syllable: before a combining mark (vowel signs, tone marks), after a leading vowel (Thai, Lao) or a stacking sign.
    private static bool CannotEndPiece(string text, int i)
    {
        char c = text[i];
        bool leading = c is >= ThaiLeadingVowelStart and <= ThaiLeadingVowelEnd or >= LaoLeadingVowelStart and <= LaoLeadingVowelEnd;
        bool stacking = c is BurmeseVirama or KhmerCoeng;
        bool markFollows = i + 1 < text.Length && char.GetUnicodeCategory(text[i + 1]) is
            System.Globalization.UnicodeCategory.NonSpacingMark or System.Globalization.UnicodeCategory.SpacingCombiningMark;
        return leading || stacking || markFollows;
    }

    // The offset just after a clause end at i (a clause mark, plus any closing quote or bracket after it), or -1. A mark must be
    // followed by whitespace or the end (so "1,000" is no clause end) and a full stop after an abbreviation or initial is not one.
    private static int ClauseEndAfter(string text, int i)
    {
        char c = text[i];
        if (!ClauseMarks.Contains(c))
        {
            return -1;
        }

        int end = i + 1;
        while (end < text.Length && ClosingMarks.Contains(text[end]))
        {
            end++;
        }

        bool cjkMark = c >= IdeographicSpace;
        bool boundary = cjkMark || end == text.Length || char.IsWhiteSpace(text[end]);
        return boundary && !(c == '.' && EndsAbbreviation(text, i)) ? end : -1;
    }

    // "Mr.", "e.g.", "U.S." or a single-letter initial such as "J.": the word that ends in the full stop at i.
    private static bool EndsAbbreviation(string text, int i)
    {
        int start = i;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
        {
            start--;
        }

        string word = text[start..(i + 1)].TrimStart('(', '[', '"', '\'', '“', '‘');
        bool initial = word.Length == 2 && char.IsLetter(word[0]) && word[0] != 'I'; // "I." ends a sentence ("So do I.")
        return initial || Abbreviations.Contains(word) || DottedLetters().IsMatch(word); // "U.S.", "a.m."; not "Wait..." or "file.txt." 
    }

    /// <summary>One chunk plus the span of the original (untrimmed) text it came
    /// from (Decision 50), for the reading text box's whole-chunk highlight
    /// (Decision 46).</summary>
    public readonly record struct TextChunk(string Text, int SourceStart, int SourceEnd)
    {
        public int SourceLength => SourceEnd - SourceStart;
    }

    /// <summary>
    /// <see cref="Split"/>, plus each chunk's source range. Split only ever decides
    /// *where* to break between chunks — it never drops, duplicates, or reorders a
    /// word — so every chunk is some number of whole words taken, in order, off the
    /// front of the original text's own word positions. That invariant means the
    /// mapping can be reconstructed by word count after the fact, without touching
    /// (and risking) Split's own, already-tuned boundary logic.
    /// </summary>
    public static IReadOnlyList<TextChunk> SplitWithRanges(
        string text, bool biasFirstChunk = true, HeadPolicy? head = null, string? firstChunkHint = null)
    {
        IReadOnlyList<string> chunks = Split(text, biasFirstChunk, head, firstChunkHint);
        var result = new List<TextChunk>(chunks.Count);
        IReadOnlySet<int> hyphenDrops = LineReflow.BreakMap(text).DroppedHyphenBreaks;
        int cursor = 0;
        foreach (string chunk in chunks)
        {
            (int start, int end) = LocateChunk(text, chunk, cursor, hyphenDrops);
            result.Add(new TextChunk(chunk, start, end));
            cursor = Math.Max(cursor, end);
        }

        return result;
    }

    // Chunks differ from the source only in whitespace (the joining above normalizes it,
    // and unspaced CJK text has none to count), so the source range is found by matching
    // the chunk's non-whitespace characters, in order, from the end of the previous chunk. Each
    // character is verified, so a chunk that is not a whitespace-only rewrite of the source
    // fails loudly rather than shifting every later range.
    private static (int Start, int End) LocateChunk(string text, string chunk, int from, IReadOnlySet<int> hyphenDrops)
    {
        int i = from;
        int start = -1;
        int end = from;
        foreach (char c in chunk.Where(c => !char.IsWhiteSpace(c)))
        {
            i = SkipDroppedInvisible(text, SkipSourceGap(text, i, hyphenDrops), c);
            if (i >= text.Length)
            {
                throw new InvalidOperationException("Chunk text runs past the end of the source.");
            }

            if (text[i] != c)
            {
                throw new InvalidOperationException(
                    $"Chunk text diverges from the source at index {i}: expected '{c}', found '{text[i]}'.");
            }

            start = start < 0 ? i : start;
            i++;
            end = i;
        }

        return start < 0 ? (0, 0) : (start, end);
    }

    // Whitespace between chunk characters is skipped, and so is the hyphen of a hyphen-wrapped word
    // ("foot-" line break "ball") exactly where LineReflow dropped it from the chunk text (Slice 76).
    private static int SkipSourceGap(string text, int i, IReadOnlySet<int> hyphenDrops)
    {
        i = SkipWhitespace(text, i);
        int newline = WrappedHyphenNewline(text, i);
        if (newline < 0 || !hyphenDrops.Contains(newline))
        {
            return i;
        }

        return SkipWhitespace(text, newline);
    }

    // Invisible-only paragraphs and sentences are dropped from the chunks (Slice 176), so the source still holds them
    // between chunks: skip them here, but never a character the chunk itself expects.
    private static int SkipDroppedInvisible(string text, int i, char expected)
    {
        while (i < text.Length && text[i] != expected && BlankText.IsInvisible(text[i]))
        {
            i++;
        }

        return i;
    }

    private static int SkipWhitespace(string text, int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        return i;
    }

    // The index of the line break after a hyphen at i (and optional trailing blanks), or -1.
    private static int WrappedHyphenNewline(string text, int i)
    {
        if (i >= text.Length || (text[i] != '-' && text[i] != '‐'))
        {
            return -1;
        }

        int k = i + 1;
        while (k < text.Length && text[k] != '\n' && char.IsWhiteSpace(text[k]))
        {
            k++;
        }

        return k < text.Length ? k : -1;
    }

    // Time-to-first-audio is dominated by the first chunk's synthesis, so make that
    // chunk a short head: at most the policy's sentences, and never more than its character
    // cap. A chunk already within the cap is left untouched. This is robust to
    // punctuation-sparse text (e.g. a copied log line) where the leading "sentence"
    // runs past the cap with no early ".!?" boundary: it is word-wrapped instead.
    private static void BiasFirstChunkToShortHead(List<string> chunks, HeadPolicy policy)
    {
        if (chunks[0].Length <= policy.MaxChars)
        {
            return;
        }

        (string head, string remainder) = SplitLeadingHead(chunks[0], policy);
        if (remainder.Length == 0)
        {
            return; // nothing to peel (e.g. a single token longer than the cap)
        }

        chunks[0] = head;
        chunks.Insert(1, remainder);
    }

    // Splits a short leading head off the front: as many leading sentences as fit the cap
    // (up to the policy's count), otherwise a word-wrapped head of at most MaxChars chars.
    // The remainder stays within TargetChunkMax (the source chunk already was), so it is
    // one chunk.
    private static (string head, string remainder) SplitLeadingHead(string text, HeadPolicy policy)
    {
        List<string> sentences = SplitSentences(text).ToList();
        int take = CountHeadSentences(sentences, policy);
        if (take == 0 || take == sentences.Count)
        {
            return PeelWordHead(text, policy.MaxChars);
        }

        return (string.Join(' ', sentences.Take(take)), string.Join(' ', sentences.Skip(take)));
    }

    private static int CountHeadSentences(List<string> sentences, HeadPolicy policy)
    {
        int take = 0;
        int length = 0;
        while (take < Math.Min(policy.MaxSentences, sentences.Count))
        {
            int next = length + (take > 0 ? 1 : 0) + sentences[take].Length;
            if (next > policy.MaxChars)
            {
                break;
            }

            length = next;
            take++;
        }

        return take;
    }

    // Peels a leading head of at most maxChars on a word boundary, returning
    // (head, remainder). A lone token longer than the cap becomes the whole head.
    private static (string head, string remainder) PeelWordHead(string text, int maxChars)
    {
        var head = new StringBuilder();
        var rest = new StringBuilder();
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries).SelectMany(w => BreakLongWord(w, maxChars)))
        {
            bool headFull = head.Length > 0 && head.Length + 1 + word.Length > maxChars;
            Append(rest.Length > 0 || headFull ? rest : head, word);
        }

        return (head.ToString(), rest.ToString());
    }

    internal static IEnumerable<string> SplitParagraphs(string text)
    {
        foreach (string block in ParagraphBreak().Split(text))
        {
            string paragraph = block.Trim();
            if (!BlankText.IsBlank(paragraph)) // an invisible-only paragraph would be an unspeakable chunk (Slice 176)
            {
                yield return paragraph;
            }
        }
    }

    private static void AppendParagraph(string paragraph, List<string> chunks)
    {
        if (paragraph.Length <= TargetChunkMax)
        {
            chunks.Add(paragraph);
            return;
        }

        var current = new StringBuilder();
        foreach (string sentence in SplitSentences(paragraph))
        {
            if (sentence.Length > TargetChunkMax)
            {
                FlushInto(chunks, current);
                foreach (string piece in HardWrap(sentence))
                {
                    chunks.Add(piece);
                }

                continue;
            }

            if (current.Length > 0 && current.Length + 1 + sentence.Length > TargetChunkMax)
            {
                FlushInto(chunks, current);
            }

            Append(current, sentence);
        }

        FlushInto(chunks, current);
    }

    internal static IEnumerable<string> SplitSentences(string paragraph)
    {
        foreach (string part in SentenceBreak().Split(paragraph))
        {
            string sentence = part.Trim();
            if (!BlankText.IsBlank(sentence))
            {
                yield return sentence;
            }
        }
    }

    /// <summary>Where each sentence of <paramref name="text"/> ends (an offset into it, after the last non-blank character), cut exactly as <see cref="SplitSentences"/> does; the early start (Slice 139) uses it so its head matches the first chunk.</summary>
    internal static List<int> SentenceEnds(string text)
    {
        var ends = new List<int>();
        int start = 0;
        foreach (Match match in SentenceBreak().Matches(text))
        {
            AddSentenceEnd(text, start, match.Index, ends);
            start = match.Index + match.Length;
        }

        AddSentenceEnd(text, start, text.Length, ends);
        return ends;
    }

    private static void AddSentenceEnd(string text, int start, int end, List<int> ends)
    {
        int last = end;
        while (last > start && char.IsWhiteSpace(text[last - 1]))
        {
            last--;
        }

        if (last > start)
        {
            ends.Add(last);
        }
    }

    // A single sentence longer than the target: wrap on word boundaries.
    private static IEnumerable<string> HardWrap(string sentence)
    {
        var current = new StringBuilder();
        foreach (string word in sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).SelectMany(w => BreakLongWord(w, TargetChunkMax)))
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > TargetChunkMax)
            {
                yield return current.ToString();
                current.Clear();
            }

            Append(current, word);
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    // A token with no spaces (unspaced Chinese or Japanese text) that is longer than a
    // chunk is wrapped by character count, never inside a surrogate pair.
    private static IEnumerable<string> BreakLongWord(string word, int max)
    {
        int start = 0;
        while (word.Length - start > max)
        {
            int length = char.IsHighSurrogate(word[start + max - 1]) ? max - 1 : max;
            yield return word.Substring(start, length);
            start += length;
        }

        yield return word.Substring(start);
    }

    private static void Append(StringBuilder builder, string text)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(text);
    }

    private static void FlushInto(List<string> chunks, StringBuilder builder)
    {
        if (builder.Length > 0)
        {
            chunks.Add(builder.ToString());
            builder.Clear();
        }
    }

    [GeneratedRegex(@"\r?\n\s*\r?\n")]
    private static partial Regex ParagraphBreak();

    [GeneratedRegex(@"^(\p{L}\.){2,}$")]
    private static partial Regex DottedLetters();

    // CJK terminators split without a space, but closing quotes and brackets after one stay
    // with the sentence they end, and a run of terminators stays together.
    [GeneratedRegex(@"(?<=[\u3002\uFF01\uFF1F][\u300D\u300F\uFF09\u3011\u3009\u300B\u201D\u2019""')\]]*)(?![\u3002\uFF01\uFF1F\u300D\u300F\uFF09\u3011\u3009\u300B\u201D\u2019""')\]])|(?<=[.!?\u061F\u06D4])\s+|\r?\n")]
    private static partial Regex SentenceBreak();
}

/// <summary>
/// The pure choices of the bounded synthesis window (Slice 244): chunk <c>k</c> of a run starts only once chunk
/// <c>k - window</c> has finished, so at most the window of generations run at once (an iPhone 8 with under 3 GB died
/// with ~110 sentences queued; finished clips waiting for playback are not bounded by this). Both phone readers (iOS and Android) use the same two rules.
/// </summary>
internal static class GenerationWindowPlan
{
    /// <summary>Chunks in flight at once on a normal phone.</summary>
    public const int DefaultWindow = 8;

    /// <summary>Chunks in flight at once on a phone with little memory.</summary>
    public const int SmallMemoryWindow = 3;

    /// <summary>Physical memory below which the small window applies (3 GB).</summary>
    public const long SmallMemoryBytes = 3L * 1024 * 1024 * 1024;

    /// <summary>
    /// The window for a phone with <paramref name="physicalMemoryBytes"/> of memory; 0 or less (the probe failed,
    /// memory unknown) takes the full window, the same as before the window was sized.
    /// </summary>
    public static int WindowFor(long physicalMemoryBytes) =>
        physicalMemoryBytes > 0 && physicalMemoryBytes < SmallMemoryBytes ? SmallMemoryWindow : DefaultWindow;

    /// <summary>
    /// The offset (from the run's first chunk) of the chunk that chunk <paramref name="offset"/> must wait to finish
    /// before it starts, or null when it may start at once. At most <paramref name="window"/> chunks are in flight
    /// at a time (the same count as Android's earlier <c>GenerationWindow</c>: chunk k waits for chunk k - window),
    /// and generation keeps running ahead of playback, so every chunk's duration is known early and a skip can
    /// land anywhere. A skip or voice swap starts a new run, so its window begins at the new position.
    /// </summary>
    public static int? WaitsFor(int offset, int window)
    {
        int behind = offset - Math.Max(1, window);
        return behind >= 0 ? behind : null;
    }
}
