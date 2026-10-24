namespace ReadTheStupidText.Application.Translation;

/// <summary>What the main page, the capture wizard or a file import hands the review page (Slice 101).</summary>
/// <param name="Text">The text to check.</param>
/// <param name="Original">The text a translation came from, when <paramref name="Text"/> is one (Edit after Translate and read).</param>
/// <param name="FailedPages">The 1-based capture pages that could not be read, for the banner.</param>
/// <param name="ReadPages">The 1-based capture pages whose text is in <paramref name="Text"/>.</param>
public sealed record ReviewRequest(string Text, string? Original = null, IReadOnlyList<int>? FailedPages = null, IReadOnlyList<int>? ReadPages = null);

/// <summary>What the review page hands back when the person taps Read: the text, its original if translated, and its language if known.</summary>
public sealed record ReviewResult(string Text, string? Original, string? Language);

/// <summary>
/// The two one-shot hand-offs between the main page and the review page (Slice 101). A singleton, not a
/// Shell query parameter: a one-shot hand-off to the cached root page replays on later navigation.
/// </summary>
public sealed class ReviewHandOff
{
    private ReviewRequest? _request;
    private ReviewResult? _result;

    public void Open(ReviewRequest request) => _request = request;

    /// <summary>The request left for the review page, cleared so it is applied once.</summary>
    public ReviewRequest? TakeRequest()
    {
        ReviewRequest? request = _request;
        _request = null;
        return request;
    }

    public void Finish(ReviewResult result) => _result = result;

    /// <summary>The result left for the main page, cleared so it is applied once.</summary>
    public ReviewResult? TakeResult()
    {
        ReviewResult? result = _result;
        _result = null;
        return result;
    }
}
