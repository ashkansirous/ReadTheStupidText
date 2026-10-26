namespace ReadTheStupidText.Application.Reading;

/// <summary>What a page should do with its wait overlay.</summary>
public enum ReadWaitAction
{
    /// <summary>Leave the wait as it is.</summary>
    None,

    /// <summary>Begin the wait (or re-word the one this page began) for the stage given.</summary>
    Begin,

    /// <summary>End the wait this page began.</summary>
    End,
}

/// <summary>
/// Who owns the page's wait while a read runs (Slice 148): the one wait this page began, either the loading wait (before the
/// first audio) or the stalled wait (mid-read, the next chunk is late). Whichever it began, it ends when the read no longer needs
/// it, and Cancel always ends it; a wait someone else began (a save, an import) is never touched. Pure; UI thread only.
/// </summary>
public sealed class ReadWaitOwner
{
    /// <summary>True while this page has begun a wait it has not yet ended.</summary>
    public bool Owns { get; private set; }

    /// <summary>
    /// The page's next move for the read's state. Loading wins over a stall. <paramref name="foreignWaitActive"/> is true when a
    /// wait this page did not begin is showing; then a stall does not take the overlay.
    /// </summary>
    public ReadWaitAction Next(bool loading, bool stalled, bool foreignWaitActive)
    {
        if (loading)
        {
            return Take();
        }

        if (stalled)
        {
            return Owns || !foreignWaitActive ? Take() : ReadWaitAction.None;
        }

        return Release();
    }

    /// <summary>Cancel, or the page leaving: end the wait if this page began one.</summary>
    public ReadWaitAction Cancelled()
    {
        return Release();
    }

    private ReadWaitAction Take()
    {
        Owns = true;
        return ReadWaitAction.Begin;
    }

    private ReadWaitAction Release()
    {
        if (!Owns)
        {
            return ReadWaitAction.None;
        }

        Owns = false;
        return ReadWaitAction.End;
    }
}
