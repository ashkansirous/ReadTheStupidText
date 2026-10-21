namespace DinoReader.Core;

/// <summary>
/// Auto-follow (Decision 80): the shown page follows the page being read until the child swipes
/// somewhere else. Swiping back onto the reading page, pressing Play or tapping a sentence follows
/// again. While the child is elsewhere, <see cref="IsWandering"/> is true (the "Reading page N" pill).
/// </summary>
public sealed class PageFollower
{
    public int ReadingPage { get; private set; }

    public int ShownPage { get; private set; }

    public bool IsFollowing { get; private set; } = true;

    public bool IsWandering => !IsFollowing;

    /// <summary>Reading reached <paramref name="page"/>; the shown page goes with it only while following.
    /// A wanderer who is already looking at that page is following again: the pill has nothing to say.</summary>
    public void Read(int page)
    {
        ReadingPage = page;
        if (page == ShownPage)
        {
            IsFollowing = true;
        }

        if (IsFollowing)
        {
            ShownPage = page;
        }
    }

    /// <summary>The pages were laid out again (a new size or mode), so page numbers changed. The reading page is
    /// remapped; a follower goes with it, a wanderer keeps looking at <paramref name="shownPage"/>, the page
    /// that now holds what they were looking at.</summary>
    public void Relaid(int readingPage, int shownPage)
    {
        ReadingPage = readingPage;
        if (IsFollowing)
        {
            ShownPage = readingPage;
            return;
        }

        ShownPage = shownPage;
        IsFollowing = shownPage == readingPage;
    }

    /// <summary>The child moved to <paramref name="page"/> by hand.</summary>
    public void Swiped(int page)
    {
        ShownPage = page;
        IsFollowing = page == ReadingPage;
    }

    /// <summary>Follow again and jump to the page being read.</summary>
    public void Resume()
    {
        IsFollowing = true;
        ShownPage = ReadingPage;
    }

    /// <summary>Start following at <paramref name="page"/> (a tap on a sentence, or a fresh read).</summary>
    public void ResumeAt(int page)
    {
        ReadingPage = page;
        Resume();
    }
}
