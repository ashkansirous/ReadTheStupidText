using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Application.Translation;

/// <summary>
/// Splits a text into the blocks a streamed translated read translates one after another (Slice 75): a
/// short first block (one or two sentences) so the listener hears the start soon, then larger blocks that
/// each translate well ahead of the speech. When the early start already translated the text's head (Slice 142), that
/// span is the first block instead, so its translation is reused. Pure.
/// </summary>
public static class TranslationBlocks
{
    /// <summary>The first block holds a second sentence only while it stays within this many characters.</summary>
    public const int HeadMaxChars = 200;

    /// <summary>Later blocks grow to about this many characters, ending on a sentence.</summary>
    public const int BlockChars = 600;

    private const int HeadMaxSentences = 2;

    public static IReadOnlyList<string> Plan(string text) => Plan(text, null);

    /// <summary>As <see cref="Plan(string)"/>; a head translated early for exactly this text is the first block.</summary>
    public static IReadOnlyList<string> Plan(string text, EarlyTranslation? early)
    {
        var blocks = new List<string>();
        if (early is not null && early.FullText == text && early.Split.Head.Length > 0)
        {
            blocks.Add(early.Split.Head);
            AddGroupedBlocks(SentencesOf(early.Split.Remainder), 0, blocks);
            return blocks;
        }

        List<string> sentences = SentencesOf(text);
        AddGroupedBlocks(sentences, TakeHead(sentences, blocks), blocks);
        return blocks;
    }

    // Whole sentences from startIndex packed into blocks of about BlockChars.
    private static void AddGroupedBlocks(List<string> sentences, int startIndex, List<string> blocks)
    {
        var block = new System.Text.StringBuilder();
        for (int index = startIndex; index < sentences.Count; index++)
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
