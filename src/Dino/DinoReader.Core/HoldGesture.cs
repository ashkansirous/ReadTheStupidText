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
    private long _swallowExpiresAt;

    /// <summary>A release's own tap arrives within milliseconds; a swallow left by a fired hold expires this long after that release, so a stale one can never eat a later tap (Slice 298).</summary>
    public const long SwallowWindowMilliseconds = 500;

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

    /// <summary>The cover hid, unloaded or was given another book (Slice 298): the hold is over and nothing is left to swallow, a fired hold included, so the first tap on the next book is never dropped.</summary>
    public void Cancel()
    {
        _swallowTap = false;
        Phase = Phase == HoldPhase.Pressed ? HoldPhase.Cancelled : HoldPhase.Idle;
    }

    /// <summary>The list began to scroll or the reorder layer took over: a waiting hold is over, but a fired hold keeps its swallow, so a relayout in the middle of the touch
    /// does not turn the release into a tap that opens the sheet; the release, or the next press, ends it.</summary>
    public void CancelKeepingSwallow()
    {
        if (Phase == HoldPhase.Pressed)
        {
            Phase = HoldPhase.Cancelled;
        }
        else if (Phase != HoldPhase.Fired)
        {
            Phase = HoldPhase.Idle;
        }
    }

    /// <summary>The finger or mouse button came up (or the system cancelled the touch). Always lands in Idle; a fired hold leaves one tap to swallow.</summary>
    public void Release(long nowMilliseconds)
    {
        if (Phase == HoldPhase.Fired)
        {
            _swallowTap = true;
            _swallowExpiresAt = nowMilliseconds + SwallowWindowMilliseconds;
        }

        Phase = HoldPhase.Idle;
    }

    /// <summary>A tap arrived. Returns true when it is the release of the press that fired the hold and must not open the sheet. The tap may come before or after <see cref="Release"/>.</summary>
    public bool ShouldSwallowTap(long nowMilliseconds)
    {
        bool swallow = (_swallowTap && nowMilliseconds <= _swallowExpiresAt) || Phase == HoldPhase.Fired;
        _swallowTap = false;
        if (Phase == HoldPhase.Fired)
        {
            Phase = HoldPhase.Idle; // this tap used up the fired hold; the release that follows has nothing left to leave
        }

        return swallow;
    }
}
