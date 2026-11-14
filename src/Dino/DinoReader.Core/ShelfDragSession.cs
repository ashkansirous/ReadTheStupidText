namespace DinoReader.Core;

/// <summary>What the view does after a drag event (Slice 298): put <see cref="Order"/> on the shelf when it is not null, and finish the lifted cover's drop animation when <see cref="EndVisual"/> is true.</summary>
public readonly record struct ShelfDragOutcome(IReadOnlyList<string>? Order, bool EndVisual)
{
    public static ShelfDragOutcome None { get; } = new(null, false);
}

/// <summary>
/// The end of one shelf drag as a pure state machine (Slice 298, no MAUI): the platform raises Drop (on the grid) and DropCompleted (on the dragged cover), in either order, or only
/// DropCompleted when the drag is cancelled or dropped elsewhere. The view only forwards those events and applies the outcome, so the result does not depend on their order:
/// a drop that landed keeps the shelf as dropped; a drag that ended with no drop puts the order from before the lift back, and a Drop that only arrives afterwards re-applies the order it ended with.
/// </summary>
public sealed class ShelfDragSession
{
    private enum Stage
    {
        Idle,
        Dragging,
        Dropped, // the drop landed first; the completed event that follows has nothing to do
        UndoneAwaitingDrop, // completed came first with no drop yet: the order was put back, a late drop restores what it ended with
    }

    /// <summary>How long after Completed a Drop still counts as the same drag's late drop; a stray Drop later than this does nothing.</summary>
    public const long LateDropWindowMilliseconds = 1000;

    private Stage _stage = Stage.Idle;
    private IReadOnlyList<string> _before = [];
    private IReadOnlyList<string> _endedWith = [];
    private long _completedAtMs;

    /// <summary>A lift began: remember the order to put back when no drop lands.</summary>
    public void Begin(IReadOnlyList<string> orderBeforeDrag)
    {
        _before = orderBeforeDrag;
        _endedWith = [];
        _stage = Stage.Dragging;
    }

    /// <summary>The drop on the grid: the order on screen stands. <paramref name="nowMs"/> is the clock (tick milliseconds), passed in so this stays pure.</summary>
    public ShelfDragOutcome Drop(long nowMs)
    {
        switch (_stage)
        {
            case Stage.Dragging:
                _stage = Stage.Dropped;
                return new ShelfDragOutcome(null, true);
            case Stage.UndoneAwaitingDrop:
                _stage = Stage.Idle;
                bool late = nowMs - _completedAtMs <= LateDropWindowMilliseconds;
                return late ? new ShelfDragOutcome(_endedWith, false) : ShelfDragOutcome.None; // completed ran first: it already ended the visual and put the old order back
            default:
                return ShelfDragOutcome.None;
        }
    }

    /// <summary>The drag ended on the dragged cover (also a cancel, Esc, or a drop outside the grid). <paramref name="current"/> is the order on screen right now.</summary>
    public ShelfDragOutcome Completed(IReadOnlyList<string> current, long nowMs)
    {
        switch (_stage)
        {
            case Stage.Dragging:
                _endedWith = current;
                _completedAtMs = nowMs;
                _stage = Stage.UndoneAwaitingDrop;
                return new ShelfDragOutcome(_before, true);
            case Stage.Dropped:
                _stage = Stage.Idle;
                return ShelfDragOutcome.None;
            default:
                return ShelfDragOutcome.None;
        }
    }

    /// <summary>The mode closed: nothing of this drag is left to finish. Not called on a mid-drag rebuild, so a cancel afterwards still restores the pre-drag order.</summary>
    public void Reset() => _stage = Stage.Idle;
}
