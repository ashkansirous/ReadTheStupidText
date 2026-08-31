namespace ReadTheStupidText.Application.Documents;

/// <summary>Thrown when an uploaded document exceeds an extractor's soft
/// page/size cap, so a huge document is rejected up front instead of silently
/// truncated or left to hang synthesis.</summary>
public sealed class DocumentTooLargeException(int actual, int limit)
    : Exception($"Document has {actual} pages, exceeding the {limit}-page limit.")
{
    public int Actual { get; } = actual;

    public int Limit { get; } = limit;
}
