using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>The head start for a reader that has none: every call does nothing.</summary>
internal sealed class NoHeadStart : IHeadStart
{
    public bool ReadInProgress => false;

    public Task PrepareAsync(string text) => HeadStartRun.Skipped;

    public void CancelPrepare()
    {
    }
}
