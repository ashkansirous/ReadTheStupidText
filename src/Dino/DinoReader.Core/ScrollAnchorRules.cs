namespace DinoReader.Core;

/// <summary>Where a list is in a layout swap (Slice 232): the list raises Scrolled itself while its layout is replaced and again for the ScrollTo that puts the anchor book back.</summary>
public enum ScrollSwapPhase
{
    /// <summary>No swap: a Scrolled event is the user scrolling.</summary>
    Idle,

    /// <summary>The layout was replaced and the ScrollTo has not been issued yet: every Scrolled event is the swap's own.</summary>
    SwapStarted,

    /// <summary>The ScrollTo(anchor) was issued: the next Scrolled event is its own and ends the swap.</summary>
    ScrollToIssued,
}

/// <summary>
/// The pure rules for keeping the first visible book across a rotation (Slice 232): the anchor is a book id, never an index, and only a Scrolled event
/// that is really the user's moves it.
/// </summary>
public static class ScrollAnchorRules
{
    /// <summary>A Scrolled event moves the anchor only when no swap is pending.</summary>
    public static bool ShouldUpdateAnchor(ScrollSwapPhase phase) => phase == ScrollSwapPhase.Idle;

    /// <summary>The phase after a Scrolled event has been handled: the first one after the ScrollTo ends the swap; during the swap before it, nothing changes.</summary>
    public static ScrollSwapPhase AfterScrolled(ScrollSwapPhase phase) =>
        phase == ScrollSwapPhase.ScrollToIssued ? ScrollSwapPhase.Idle : phase;

    /// <summary>The phase after the post-swap ScrollTo was issued.</summary>
    public static ScrollSwapPhase AfterScrollToIssued(ScrollSwapPhase phase) =>
        phase == ScrollSwapPhase.SwapStarted ? ScrollSwapPhase.ScrollToIssued : phase;

    /// <summary>Where the anchor book is in the list now: 0 (the first book) when there is no anchor (a mode toggle clears it) or the book is gone.</summary>
    public static int IndexOfAnchor(IReadOnlyList<string> bookIds, string? anchorBookId)
    {
        int index = anchorBookId is null ? -1 : bookIds.ToList().IndexOf(anchorBookId);
        return Math.Max(0, index);
    }
}
