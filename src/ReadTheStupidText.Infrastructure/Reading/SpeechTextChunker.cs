using System.Text;
using System.Text.RegularExpressions;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Splits text for the neural engine so long reads synthesize as several small
/// chunks (generated concurrently, played in order) instead of one slow call.
/// Text at or under <see cref="SingleChunkMax"/> stays whole; longer text is split
/// on paragraph boundaries, then on sentences, then — only for a single oversized
/// sentence — on word boundaries, so no chunk greatly exceeds <see cref="TargetChunkMax"/>.
/// </summary>
internal static partial class SpeechTextChunker
{
    private const int SingleChunkMax = 200;
    private const int TargetChunkMax = 240;

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

        return chunks.Count > 0 ? chunks : new[] { trimmed };
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
