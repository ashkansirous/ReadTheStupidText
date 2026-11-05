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
        hold.Release();
        Assert.Equal(HoldPhase.Idle, hold.Phase);
        Assert.True(hold.ShouldSwallowTap());
        Assert.False(hold.ShouldSwallowTap());
    }

    [Fact]
    public void TapBeforeReleaseIsSwallowedAndReleaseLeavesNothing()
    {
        HoldGesture hold = Pressed();
        hold.Fired(opened: false);
        Assert.True(hold.ShouldSwallowTap());
        hold.Release();
        Assert.Equal(HoldPhase.Idle, hold.Phase);
        Assert.False(hold.ShouldSwallowTap());
    }

    [Fact]
    public void FiredAndOpenedLeavesNothingToSwallow()
    {
        HoldGesture hold = Pressed();
        hold.Fired(opened: true);
        Assert.Equal(HoldPhase.Idle, hold.Phase);
        hold.Release();
        Assert.False(hold.ShouldSwallowTap());
    }

    [Fact]
    public void ReleaseWithoutATapNeverSwallowsTheNextGenuineTap()
    {
        HoldGesture hold = Pressed();
        hold.Fired(opened: false);
        hold.Release(); // dragged off: no Tapped came
        hold.Press(10, 10);
        hold.Release();
        Assert.False(hold.ShouldSwallowTap());
    }

    [Fact]
    public void ReleaseBeforeFiringIsAnOrdinaryTap()
    {
        HoldGesture hold = Pressed();
        hold.Release();
        Assert.Equal(HoldPhase.Idle, hold.Phase);
        Assert.False(hold.CanFire());
        Assert.False(hold.ShouldSwallowTap());
    }

    [Fact]
    public void CancelledPressReleasesToIdleAndTapIsNotSwallowed()
    {
        HoldGesture hold = Pressed();
        hold.Cancel();
        Assert.Equal(HoldPhase.Cancelled, hold.Phase);
        Assert.False(hold.CanFire());
        hold.Release();
        Assert.Equal(HoldPhase.Idle, hold.Phase);
        Assert.False(hold.ShouldSwallowTap());
    }

    [Fact]
    public void CancelAfterFiringDropsTheSwallow()
    {
        HoldGesture hold = Pressed();
        hold.Fired(opened: false);
        hold.Cancel();
        Assert.Equal(HoldPhase.Idle, hold.Phase);
        Assert.False(hold.ShouldSwallowTap());
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
    public void NewPressRestartsFromAnyLeftoverState()
    {
        HoldGesture hold = Pressed();
        hold.Move(200, 100);
        hold.Press(5, 5);
        Assert.Equal(HoldPhase.Pressed, hold.Phase);
        Assert.False(hold.Move(8, 8));
    }
}
