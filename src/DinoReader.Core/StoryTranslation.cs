namespace DinoReader.Core;

/// <summary>Translates one piece of text; the app plugs the on-device translator in here.</summary>
public delegate Task<string> PieceTranslator(string text, CancellationToken cancellationToken);

/// <summary>
/// Translates a story after speakers have been attributed in its own language (Slice 66, Decision 74).
/// Each sentence piece keeps its kind, character and source range (<see cref="Segment.SourceStart"/> to
/// <see cref="Segment.SourceEnd"/>, which the reading screen highlights), while its <see cref="Segment.Text"/>
/// becomes the translation the voice speaks. A translation too long for one speech chunk is cut into
/// several pieces that all keep the same source range, so the highlight stays on the source sentence.
/// </summary>
public static class StoryTranslation
{
    /// <summary>The longest spoken piece: the speech chunker keeps text up to this length in one chunk.</summary>
    public const int MaxSpokenChars = 200;

    private const string SentenceEnds = ".!?…؟。！？";

    /// <summary>How many pieces are translated at once: the first audio waits for the whole story, so the wait is divided.</summary>
    public const int DefaultParallelism = 4;

    /// <summary>
    /// Translates every piece, up to <paramref name="maxParallel"/> at a time, and returns them in story order.
    /// Cancelling <paramref name="cancellationToken"/> (a stopped or superseded read) stops starting new pieces and throws
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    public static async Task<IReadOnlyList<SplitSegment>> TranslateAsync(
        IReadOnlyList<SplitSegment> pieces, PieceTranslator translate, CancellationToken cancellationToken, int maxParallel = DefaultParallelism)
    {
        using var gate = new SemaphoreSlim(Math.Max(1, maxParallel));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<IEnumerable<SplitSegment>>[] work = pieces.Select(p => GatedAsync(p, translate, gate, linked)).ToArray();
        try
        {
            IEnumerable<SplitSegment>[] done = await Task.WhenAll(work);
            return done.SelectMany(d => d).ToList();
        }
        catch
        {
            await linked.CancelAsync();
            ObserveAll(work);
            throw;
        }
    }

    // Lets the pieces that were cancelled by the first fault finish, so none of their exceptions goes unobserved.
    private static void ObserveAll(Task[] work)
    {
        foreach (Task task in work) _ = task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }

    private static async Task<IEnumerable<SplitSegment>> GatedAsync(
        SplitSegment piece, PieceTranslator translate, SemaphoreSlim gate, CancellationTokenSource linked)
    {
        CancellationToken cancellationToken = linked.Token;
        await gate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await TranslatePieceAsync(piece, translate, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await linked.CancelAsync();
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Cuts spoken text into runs of whole sentences of at most <see cref="MaxSpokenChars"/> characters; a longer sentence is cut at a space (or anywhere, with none).</summary>
    public static IReadOnlyList<string> SplitSpoken(string text)
    {
        var runs = new List<string>();
        string current = string.Empty;
        foreach (string sentence in Sentences(text.Trim()).SelectMany(CutLong))
        {
            if (current.Length > 0 && current.Length + 1 + sentence.Length > MaxSpokenChars)
            {
                runs.Add(current);
                current = string.Empty;
            }

            current = current.Length == 0 ? sentence : current + " " + sentence;
        }

        if (current.Length > 0) runs.Add(current);
        return runs;
    }

    private static async Task<IEnumerable<SplitSegment>> TranslatePieceAsync(
        SplitSegment piece, PieceTranslator translate, CancellationToken cancellationToken)
    {
        if (!piece.Segment.Text.Any(char.IsLetter)) return [piece];
        string translated = (await translate(piece.Segment.Text, cancellationToken)).Trim();
        if (translated.Length == 0) return [piece];
        return SplitSpoken(translated).Select(text => piece with { Segment = piece.Segment with { Text = text } });
    }

    private static IEnumerable<string> Sentences(string text)
    {
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (!EndsSentence(text, i)) continue;
            yield return text[start..(i + 1)].Trim();
            start = i + 1;
        }

        if (start < text.Length) yield return text[start..].Trim();
    }

    // A terminator ends a sentence at a space, at the end, or (CJK full-width marks) anywhere.
    private static bool EndsSentence(string text, int i)
    {
        if (!SentenceEnds.Contains(text[i])) return false;
        bool fullWidth = text[i] is '。' or '！' or '？';
        return fullWidth || i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1]);
    }

    private static IEnumerable<string> CutLong(string sentence)
    {
        while (sentence.Length > MaxSpokenChars)
        {
            int cut = sentence.LastIndexOf(' ', MaxSpokenChars);
            if (cut <= 0) cut = MaxSpokenChars;
            yield return sentence[..cut].TrimEnd();
            sentence = sentence[cut..].TrimStart();
        }

        if (sentence.Length > 0) yield return sentence;
    }
}
