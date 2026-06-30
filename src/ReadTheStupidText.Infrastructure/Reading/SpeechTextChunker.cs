using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Splits text for the neural engine so long reads synthesize as several small
/// chunks (generated concurrently, played in order) instead of one slow call.
/// Text at or under <see cref="SingleChunkMax"/> stays whole; longer text is split
/// on paragraph boundaries, then on sentences, then — only for a single oversized
/// sentence — on word boundaries, so no chunk greatly exceeds <see cref="TargetChunkMax"/>.
/// The very first chunk is biased to a short head (<see cref="FirstChunkMax"/>) so
/// playback starts after a short first synthesis rather than a whole paragraph
/// (time-to-first-audio).
/// </summary>
internal static partial class SpeechTextChunker
{
    private const int SingleChunkMax = 200;
    private const int TargetChunkMax = 240;

    // The first chunk to play is what the user waits on (time-to-first-audio), so cap
    // it well below the target: a short head synthesizes fast. Kept large enough to
    // still land on a natural boundary (usually a whole sentence) rather than sounding
    // clipped.
    private const int FirstChunkMax = 120;

    public static IReadOnlyList<string> Split(string text)
    {
        string trimmed = text.Trim();
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

        BiasFirstChunkToShortHead(chunks);
        return chunks;
    }

    // Time-to-first-audio is dominated by the first chunk's synthesis, so make that
    // chunk a short head: at most one sentence, and never more than FirstChunkMax
    // characters. A chunk already within the cap is left untouched. This is robust to
    // punctuation-sparse text (e.g. a copied log line) where the leading "sentence"
    // runs past the target with no early ".!?" boundary — peeling only a *whole*
    // sentence would leave a ~240-char first chunk and a slow first synthesis.
    private static void BiasFirstChunkToShortHead(List<string> chunks)
    {
        if (chunks[0].Length <= FirstChunkMax)
        {
            return;
        }

        (string head, string remainder) = SplitLeadingHead(chunks[0]);
        if (remainder.Length == 0)
        {
            return; // nothing to peel (e.g. a single token longer than the cap)
        }

        chunks[0] = head;
        chunks.Insert(1, remainder);
    }

    // Splits a short leading head off the front: the leading sentence when it fits the
    // cap, otherwise a word-wrapped head of at most FirstChunkMax chars. The remainder
    // stays within TargetChunkMax (the source chunk already was), so it is one chunk.
    private static (string head, string remainder) SplitLeadingHead(string text)
    {
        List<string> sentences = SplitSentences(text).ToList();
        if (sentences.Count > 1 && sentences[0].Length <= FirstChunkMax)
        {
            return (sentences[0], string.Join(' ', sentences.Skip(1)));
        }

        return PeelWordHead(text);
    }

    // Peels a leading head of at most FirstChunkMax chars on a word boundary, returning
    // (head, remainder). A lone token longer than the cap becomes the whole head.
    private static (string head, string remainder) PeelWordHead(string text)
    {
        var head = new StringBuilder();
        var rest = new StringBuilder();
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            bool headFull = head.Length > 0 && head.Length + 1 + word.Length > FirstChunkMax;
            Append(rest.Length > 0 || headFull ? rest : head, word);
        }

        return (head.ToString(), rest.ToString());
    }

    private static IEnumerable<string> SplitParagraphs(string text)
    {
        foreach (string block in ParagraphBreak().Split(text))
        {
            string paragraph = block.Trim();
            if (paragraph.Length > 0)
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

    private static IEnumerable<string> SplitSentences(string paragraph)
    {
        foreach (string part in SentenceBreak().Split(paragraph))
        {
            string sentence = part.Trim();
            if (sentence.Length > 0)
            {
                yield return sentence;
            }
        }
    }

    // A single sentence longer than the target: wrap on word boundaries.
    private static IEnumerable<string> HardWrap(string sentence)
    {
        var current = new StringBuilder();
        foreach (string word in sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries))
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

    [GeneratedRegex(@"(?<=[.!?])\s+|\r?\n")]
    private static partial Regex SentenceBreak();
}
