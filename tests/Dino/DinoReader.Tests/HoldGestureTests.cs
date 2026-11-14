using DinoReader.Core;

namespace DinoReader.Tests;

public sealed class HoldGestureTests
{
    private static HoldGesture Pressed()
    {
        var hold = new HoldGesture();
        hold.Press(100, 100);
        return hold;
    }

    [Fact]
    public void StartsIdleAndPressWaits()
    {
        var hold = new HoldGesture();
        Assert.Equal(HoldPhase.Idle, hold.Phase);
        hold.Press(1, 1);
        Assert.Equal(HoldPhase.Pressed, hold.Phase);
        Assert.True(hold.CanFire());
    }

    [Fact]
    public void SmallMoveKeepsWaitingAndBigMoveCancels()
    {
        HoldGesture hold = Pressed();
        Assert.False(hold.Move(105, 95));
        Assert.Equal(HoldPhase.Pressed, hold.Phase);
        Assert.True(hold.Move(100, 113));
        Assert.Equal(HoldPhase.Cancelled, hold.Phase);
        Assert.False(hold.CanFire());
    }

    [Fact]
    public void MoveWhenNotPressedDoesNothing()
    {
        var hold = new HoldGesture();
        Assert.False(hold.Move(500, 500));
        Assert.Equal(HoldPhase.Idle, hold.Phase);
    }

    [Fact]
    public void FiredWithoutOpeningSwallowsTheReleasingTapOnce()
    {
        HoldGesture hold = Pressed();
        hold.Fired(opened: false);
        Assert.Equal(HoldPhase.Fired, hold.Phase);
        hold.Release(0);
        Assert.Equal(HoldPhase.Idle, hold.Phase);
        Assert.True(hold.ShouldSwallowTap(0));
        Assert.False(hold.ShouldSwallowTap(0));
    }

    [Fact]
    public void TapBeforeReleaseIsSwallowedAndReleaseLeavesNothing()
    {
        HoldGesture hold = Pressed();
        hold.Fired(opened: false);
        Assert.True(hold.ShouldSwallowTap(0));
        hold.Release(0);
        Assert.Equal(HoldPhase.Idle, hold.Phase);
        Assert.False(hold.ShouldSwallowTap(0));
    }

    [Fact]
    public void FiredAndOpenedLeavesNothingToSwallow()
    {
        HoldGesture hold = Pressed();
        hold.Fired(opened: true);
        Assert.Equal(HoldPhase.Idle, hold.Phase);
        hold.Release(0);
        Assert.False(hold.ShouldSwallowTap(0));
    }

    [Fact]
    public void ReleaseWithoutATapNeverSwallowsTheNextGenuineTap()
    {
        HoldGesture hold = Pressed();
        hold.Fired(opened: false);
        hold.Release(0); // dragged off: no Tapped came
        hold.Press(10, 10);
        hold.Release(0);
        Assert.False(hold.ShouldSwallowTap(0));
    }

    [Fact]
    public void ReleaseBeforeFiringIsAnOrdinaryTap()
    {
        HoldGesture hold = Pressed();
        hold.Release(0);
        Assert.Equal(HoldPhase.Idle, hold.Phase);
        Assert.False(hold.CanFire());
        Assert.False(hold.ShouldSwallowTap(0));
    }

    [Fact]
    public void CancelledPressReleasesToIdleAndTapIsNotSwallowed()
    {
        HoldGesture hold = Pressed();
        hold.Cancel();
        Assert.Equal(HoldPhase.Cancelled, hold.Phase);
        Assert.False(hold.CanFire());
        hold.Release(0);
        Assert.Equal(HoldPhase.Idle, hold.Phase);
        Assert.False(hold.ShouldSwallowTap(0));
    }

    [Fact]
    public void CancelAfterFiringKeepsTheSwallowUntilTheTouchEnds()
    {
        HoldGesture hold = Pressed();
        hold.Fired(opened: false);
        hold.CancelKeepingSwallow(); // a relayout in the middle of the touch
        Assert.Equal(HoldPhase.Fired, hold.Phase);
        hold.Release(0);
        Assert.True(hold.ShouldSwallowTap(0));
        Assert.False(hold.ShouldSwallowTap(0));
    }

    [Fact]
    public void CancelAfterFiringAndReleaseKeepsTheSwallowForTheTap()
    {
        HoldGesture hold = Pressed();
        hold.Fired(opened: false);
        hold.Release(0);
        hold.CancelKeepingSwallow(); // the relayout lands between the release and the tap
        Assert.True(hold.ShouldSwallowTap(0));
    }

    [Fact]
    public void CancelAfterFiringClearsTheSwallowForARebind()
    {
        HoldGesture hold = Pressed();
        hold.Fired(opened: false);
        hold.Cancel(); // the cell was given another book
        Assert.Equal(HoldPhase.Idle, hold.Phase);
        Assert.False(hold.ShouldSwallowTap(0));
    }

    [Fact]
    public void CancelAfterFiringAndReleaseClearsTheSwallowForARebind()
    {
        HoldGesture hold = Pressed();
        hold.Fired(opened: false);
        hold.Release(0);
        hold.Cancel();
        Assert.False(hold.ShouldSwallowTap(0));
    }

    [Fact]
    public void NextPressClearsAKeptSwallow()
    {
        HoldGesture hold = Pressed();
        hold.Fired(opened: false);
        hold.CancelKeepingSwallow();
        hold.Press(5, 5);
        hold.Release(0);
        Assert.False(hold.ShouldSwallowTap(0));
    }

    [Fact]
    public void LateTimerAfterCancelDoesNotFire()
    {
        HoldGesture hold = Pressed();
        hold.Move(200, 100);
        hold.Fired(opened: true);
        Assert.Equal(HoldPhase.Cancelled, hold.Phase);
    }

    [Fact]
    public void TapRightAfterReleaseIsSwallowed()
    {
        HoldGesture hold = Pressed();
        hold.Fired(opened: false);
        hold.Release(1000);
        Assert.True(hold.ShouldSwallowTap(1000 + HoldGesture.SwallowWindowMilliseconds));
    }

    [Fact]
    public void TapAfterTheSwallowWindowIsNotSwallowed()
    {
        HoldGesture hold = Pressed();
        hold.Fired(opened: false);
        hold.Release(1000);
        Assert.False(hold.ShouldSwallowTap(1000 + HoldGesture.SwallowWindowMilliseconds + 1));
    }

    [Fact]
    public void CancelKeepingSwallowThenALateTapIsNotSwallowed()
    {
        HoldGesture hold = Pressed();
        hold.Fired(opened: false);
        hold.Release(1000);
        hold.CancelKeepingSwallow(); // a scroll after the release; the cell is then recycled without Cancel
        Assert.False(hold.ShouldSwallowTap(5000));
    }

    [Fact]
    public void NewPressRestartsFromAnyLeftoverState()
    {
        HoldGesture hold = Pressed();
        hold.Move(200, 100);
        hold.Press(5, 5);
        Assert.Equal(HoldPhase.Pressed, hold.Phase);
        Assert.False(hold.Move(8, 8));
    }
}
