using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Application.Translation;

/// <summary>
/// Splits a text into the blocks a streamed translated read translates one after another (Slice 75): a
/// short first block (one or two sentences) so the listener hears the start soon, then larger blocks that
/// each translate well ahead of the speech. Pure.
/// </summary>
public static class TranslationBlocks
{
    /// <summary>The first block holds a second sentence only while it stays within this many characters.</summary>
    public const int HeadMaxChars = 200;

    /// <summary>Later blocks grow to about this many characters, ending on a sentence.</summary>
    public const int BlockChars = 600;

    private const int HeadMaxSentences = 2;

    public static IReadOnlyList<string> Plan(string text)
    {
        List<string> sentences = SentencesOf(text);
        var blocks = new List<string>();
        int index = TakeHead(sentences, blocks);
        var block = new System.Text.StringBuilder();
        for (; index < sentences.Count; index++)
        {
            if (block.Length > 0 && block.Length + sentences[index].Length > BlockChars)
            {
                blocks.Add(block.ToString());
                block.Clear();
            }

            block.Append(block.Length > 0 ? " " : string.Empty).Append(sentences[index]);
        }

        if (block.Length > 0)
        {
            blocks.Add(block.ToString());
        }

        return blocks;
    }

    // One sentence, plus the second when the pair stays short. Returns the index of the next sentence.
    private static int TakeHead(List<string> sentences, List<string> blocks)
    {
        if (sentences.Count == 0)
        {
            return 0;
        }

        int take = 1;
        int length = sentences[0].Length;
        while (take < HeadMaxSentences && take < sentences.Count && length + 1 + sentences[take].Length <= HeadMaxChars)
        {
            length += 1 + sentences[take].Length;
            take++;
        }

        blocks.Add(string.Join(" ", sentences.Take(take)));
        return take;
    }

    private static List<string> SentencesOf(string text) =>
        SpeechTextChunker.SplitParagraphs(text)
            .SelectMany(p => TranslationFlow.SplitSentences(p))
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
}
