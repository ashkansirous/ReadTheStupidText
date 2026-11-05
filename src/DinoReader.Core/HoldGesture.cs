namespace DinoReader.Core;

/// <summary>Where a press on a cover is in its 1 second hold (Slice 264).</summary>
public enum HoldPhase
{
    Idle,
    Pressed,
    Fired,
    Cancelled,
}

/// <summary>
/// The hold-to-reorder press as one pure state machine (Slice 264): Idle -> Pressed -> Fired or Cancelled, and every release goes back to Idle.
/// The view only feeds it events (press, move, timer, release, tap, cancel). A tap is swallowed only when that same press fired the hold
/// and the reorder layer did not take the release; the next press always clears that, so a later genuine tap is never swallowed.
/// </summary>
public sealed class HoldGesture
{
    private double _startX;
    private double _startY;
    private bool _swallowTap;

    public HoldPhase Phase { get; private set; } = HoldPhase.Idle;

    /// <summary>A new press starts the hold from Idle, or from any leftover state (a lost release must not poison the next press).</summary>
    public void Press(double x, double y)
    {
        _startX = x;
        _startY = y;
        _swallowTap = false;
        Phase = HoldPhase.Pressed;
    }

    /// <summary>Moving past the slop turns a waiting press into a scroll or swipe. Returns true when this move cancelled it.</summary>
    public bool Move(double x, double y)
    {
        if (Phase != HoldPhase.Pressed || !ShelfOrderLayoutRules.MovedPastHoldSlop(x - _startX, y - _startY))
        {
            return false;
        }

        Phase = HoldPhase.Cancelled;
        return true;
    }

    /// <summary>The 1 second timer ran out. Returns false when the press was no longer waiting (nothing to open).
    /// Call <see cref="Fired"/> with the outcome after the gallery handled the hold.</summary>
    public bool CanFire() => Phase == HoldPhase.Pressed;

    /// <summary>The hold fired; <paramref name="opened"/> tells whether the reorder layer opened and so takes the release itself (then there is nothing to swallow here).</summary>
    public void Fired(bool opened)
    {
        if (Phase != HoldPhase.Pressed)
        {
            return;
        }

        Phase = opened ? HoldPhase.Idle : HoldPhase.Fired;
    }

    /// <summary>Scrolling began, the cover hid, unloaded or was given another book: the hold is over. A fired hold also loses its swallow.</summary>
    public void Cancel()
    {
        Phase = Phase == HoldPhase.Pressed ? HoldPhase.Cancelled : HoldPhase.Idle;
        _swallowTap = false;
    }

    /// <summary>The finger or mouse button came up (or the system cancelled the touch). Always lands in Idle; a fired hold leaves one tap to swallow.</summary>
    public void Release()
    {
        _swallowTap = _swallowTap || Phase == HoldPhase.Fired;
        Phase = HoldPhase.Idle;
    }

    /// <summary>A tap arrived. Returns true when it is the release of the press that fired the hold and must not open the sheet. The tap may come before or after <see cref="Release"/>.</summary>
    public bool ShouldSwallowTap()
    {
        bool swallow = _swallowTap || Phase == HoldPhase.Fired;
        _swallowTap = false;
        if (Phase == HoldPhase.Fired)
        {
            Phase = HoldPhase.Idle; // this tap used up the fired hold; the release that follows has nothing left to leave
        }

        return swallow;
    }
}
