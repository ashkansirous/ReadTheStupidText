using DinoReader.Core;

namespace DinoReader.Tests;

public sealed class ShelfDragSessionTests
{
    private static readonly string[] Before = ["a", "b", "c"];
    private static readonly string[] Moved = ["b", "c", "a"];

    // Replays the platform's events the way the view does: apply the outcome's order to a shelf, and count the visual ends.
    private sealed class Shelf
    {
        public IReadOnlyList<string> Order = Before;
        public int VisualEnds;
        public long Now = 5_000;
        public readonly ShelfDragSession Session = new();

        public Shelf Lift()
        {
            Session.Begin(Order);
            Order = Moved; // the drag moved the book
            return this;
        }

        public Shelf Apply(ShelfDragOutcome outcome)
        {
            if (outcome.Order is { } order)
            {
                Order = order;
            }

            VisualEnds += outcome.EndVisual ? 1 : 0;
            return this;
        }

        public Shelf Drop() => Apply(Session.Drop(Now));

        public Shelf Completed() => Apply(Session.Completed(Order, Now));
    }

    [Fact]
    public void DropThenCompletedKeepsTheDrop()
    {
        Shelf shelf = new Shelf().Lift().Drop().Completed();
        Assert.Equal(Moved, shelf.Order);
        Assert.Equal(1, shelf.VisualEnds);
    }

    [Fact]
    public void CompletedThenDropKeepsTheDrop()
    {
        Shelf shelf = new Shelf().Lift().Completed().Drop();
        Assert.Equal(Moved, shelf.Order);
        Assert.Equal(1, shelf.VisualEnds);
    }

    [Fact]
    public void DropWithoutCompletedKeepsTheDrop()
    {
        Shelf shelf = new Shelf().Lift().Drop();
        Assert.Equal(Moved, shelf.Order);
        Assert.Equal(1, shelf.VisualEnds);
    }

    [Fact]
    public void CompletedWithoutADropPutsTheOrderBack()
    {
        Shelf shelf = new Shelf().Lift().Completed();
        Assert.Equal(Before, shelf.Order);
        Assert.Equal(1, shelf.VisualEnds);
    }

    [Fact]
    public void CancelledDragLeavesNothingForTheNextDrop()
    {
        Shelf shelf = new Shelf().Lift().Completed();
        shelf.Session.Begin(shelf.Order);
        shelf.Order = Moved;
        shelf.Drop().Completed();
        Assert.Equal(Moved, shelf.Order);
    }

    [Fact]
    public void EventsWithNoDragDoNothing()
    {
        var shelf = new Shelf();
        shelf.Drop().Completed();
        Assert.Equal(Before, shelf.Order);
        Assert.Equal(0, shelf.VisualEnds);
    }

    [Fact]
    public void RepeatedCompletedDoesNotUndoTwice()
    {
        Shelf shelf = new Shelf().Lift().Completed();
        shelf.Order = Moved; // something else reordered afterwards
        shelf.Completed();
        Assert.Equal(Moved, shelf.Order);
    }

    [Fact]
    public void NextBeginDiscardsAnAbandonedDragsOrder()
    {
        string[] second = ["c", "a", "b"];
        Shelf shelf = new Shelf().Lift().Completed(); // first drag cancelled: Before is back, Moved is the abandoned order
        shelf.Session.Begin(shelf.Order);
        shelf.Order = second;
        shelf.Drop(); // the second drag lands
        shelf.Drop(); // a duplicated drop must not bring the first drag's order back
        shelf.Completed();
        Assert.Equal(second, shelf.Order);
    }

    [Fact]
    public void ResetAfterACancelledDragStopsAnyLaterDropApplyingIt()
    {
        Shelf shelf = new Shelf().Lift().Completed();
        shelf.Session.Reset(); // the mode closed (Esc, Cancel, Done)
        shelf.Drop().Drop();
        Assert.Equal(Before, shelf.Order);
        Assert.Equal(1, shelf.VisualEnds);
    }

    [Fact]
    public void LateDropInsideTheWindowKeepsTheDrop()
    {
        Shelf shelf = new Shelf().Lift().Completed();
        shelf.Now += ShelfDragSession.LateDropWindowMilliseconds;
        shelf.Drop();
        Assert.Equal(Moved, shelf.Order);
    }

    [Fact]
    public void StrayDropAfterTheWindowDoesNothing()
    {
        Shelf shelf = new Shelf().Lift().Completed();
        shelf.Now += ShelfDragSession.LateDropWindowMilliseconds + 1;
        shelf.Drop();
        Assert.Equal(Before, shelf.Order);
        Assert.Equal(1, shelf.VisualEnds);
    }

    [Fact]
    public void CancelAfterARebuildMidDragStillRestoresTheOrder()
    {
        Shelf shelf = new Shelf().Lift(); // the view rebuilds its entries here and does not touch the session
        shelf.Completed();
        Assert.Equal(Before, shelf.Order);
        Assert.Equal(1, shelf.VisualEnds);
    }

    [Fact]
    public void ResetDropsALateDrop()
    {
        Shelf shelf = new Shelf().Lift().Completed();
        shelf.Session.Reset();
        shelf.Drop();
        Assert.Equal(Before, shelf.Order);
    }
}
