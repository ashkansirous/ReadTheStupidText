namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The heads the early start has voiced (Slice 139), so a read that starts on the same text begins with exactly that text as
/// its first chunk. The early voice decides the head (a sentence-boundary prefix of the text, original or translated), and the
/// chunker, which every reader plans its chunks with, takes a registered head as the first chunk before it chunks the rest:
/// the first units the read asks the sentence cache for are the ones that were prepared, by construction. A head that matches
/// nothing does nothing. Process-wide because the voice and the readers are separate singletons; thread-safe.
/// </summary>
internal static class EarlyFirstChunks
{
    private const int Capacity = 8;

    private static readonly object Gate = new();
    private static readonly List<string> Heads = [];

    /// <summary>Remembers <paramref name="head"/> (the newest kept, the oldest dropped past a few).</summary>
    public static void Register(string head)
    {
        string reflowed = Normalize(head);
        if (reflowed.Length == 0)
        {
            return;
        }

        lock (Gate)
        {
            Heads.Remove(reflowed);
            Heads.Add(reflowed);
            if (Heads.Count > Capacity)
            {
                Heads.RemoveAt(0);
            }
        }
    }

    /// <summary>Forgets <paramref name="head"/>.</summary>
    public static void Remove(string head)
    {
        lock (Gate)
        {
            Heads.Remove(Normalize(head));
        }
    }

    /// <summary>Forgets every head (the text they were for has changed).</summary>
    public static void Clear()
    {
        lock (Gate)
        {
            Heads.Clear();
        }
    }

    /// <summary>The longest registered head that <paramref name="reflowedText"/> starts with, up to a word boundary, or null.</summary>
    public static string? Match(string reflowedText)
    {
        lock (Gate)
        {
            return Heads
                .Where(head => StartsWithWhole(reflowedText, head))
                .OrderByDescending(head => head.Length)
                .FirstOrDefault();
        }
    }

    private static bool StartsWithWhole(string text, string head) =>
        text.StartsWith(head, StringComparison.Ordinal)
        && (text.Length == head.Length || char.IsWhiteSpace(text[head.Length]));

    private static string Normalize(string text) => LineReflow.ReflowText(text ?? string.Empty).Trim();
}
