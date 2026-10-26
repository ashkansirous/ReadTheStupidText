using System.Diagnostics;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// How the review page asks the main page to voice a head into the sentence cache (Slice 139): the main page owns the voice
/// plan (which pack and engine would read this language), so it registers the handler and the review page only calls it.
/// Nothing happens while no handler is set.
/// </summary>
public sealed class EarlyStartVoice
{
    /// <summary>The prefix of every early-start debug line.</summary>
    public const string LogTag = "[EarlyStart]";

    /// <summary>Voices <c>text</c> in <c>language</c> without playing it; cancelling the token stops the preparation. Returns
    /// whether something was prepared (false when it did nothing: no neural voice, a read running, the page gone).</summary>
    public Func<string, string, CancellationToken, Task<bool>>? Handler { get; set; }

    /// <summary>The heads whose preparation ran to the end, until a read takes them or the text changes (see <see cref="TakeEarly"/>).</summary>
    private readonly List<(string Text, string Language)> _preparedHeads = [];

    /// <summary>Forgets what was prepared: the text it was prepared for has changed.</summary>
    public void Reset()
    {
        _preparedHeads.Clear();
        EarlyFirstChunks.Clear();
    }

    /// <summary>Stops using <paramref name="handler"/> (its page is gone); another page's handler stays.</summary>
    public void Clear(Func<string, string, CancellationToken, Task<bool>> handler)
    {
        if (Handler == handler)
        {
            Handler = null;
        }
    }

    public async Task VoiceAsync(string text, string language, CancellationToken cancellationToken)
    {
        if (Handler is not { } handler)
        {
            Debug.WriteLine($"{LogTag} voice skipped ({language}): no handler registered");
            return;
        }

        Debug.WriteLine($"{LogTag} voice start ({language}, {text.Length} chars)");
        EarlyFirstChunks.Register(text); // a read of this text starts with exactly this head as its first chunk
        bool prepared = await RunHandlerAsync(handler, text, language, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            Debug.WriteLine($"{LogTag} voice cancel ({language}, prepared={prepared})");
            return;
        }

        Debug.WriteLine($"{LogTag} voice done ({language}, prepared={prepared})");
        if (prepared)
        {
            _preparedHeads.Add((text, language));
        }
    }

    // A preparation that was superseded or cancelled ends cancelled: it stored nothing, so it is not prepared.
    private static async Task<bool> RunHandlerAsync(
        Func<string, string, CancellationToken, Task<bool>> handler, string text, string language, CancellationToken cancellationToken)
    {
        try
        {
            return await handler(text, language, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether a read of <paramref name="readText"/> in <paramref name="language"/> had its head prepared in that language before
    /// the tap (a usage count's <c>early</c>). Taken once: the next read asks again.
    /// </summary>
    public bool TakeEarly(string readText, string language)
    {
        string trimmed = readText.TrimStart();
        bool early = _preparedHeads.Exists(head => head.Language == language && trimmed.StartsWith(head.Text, StringComparison.Ordinal));
        _preparedHeads.Clear();
        return early;
    }
}
