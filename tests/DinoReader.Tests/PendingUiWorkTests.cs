using DinoReader.Core;

namespace DinoReader.Tests;

public class PendingUiWorkTests
{
    [Fact]
    public void Requests_or_their_bits_together()
    {
        var pending = new PendingUiWork();
        pending.Request(UiWork.Chrome);
        pending.Request(UiWork.Tab);

        Assert.True(pending.TryTake(UiWork.Chrome | UiWork.Tab, out UiWork taken));
        Assert.Equal(UiWork.Chrome | UiWork.Tab, taken);
        Assert.False(pending.Any);
    }

    [Fact]
    public void A_bit_the_pass_cannot_run_stays_waiting()
    {
        var pending = new PendingUiWork();
        pending.Request(UiWork.Shelf | UiWork.Chrome);

        Assert.True(pending.TryTake(UiWork.Shelf, out UiWork taken));

        Assert.Equal(UiWork.Shelf, taken);
        Assert.True(pending.Has(UiWork.Chrome));
    }

    [Fact]
    public void Nothing_runnable_takes_nothing_and_loses_nothing()
    {
        var pending = new PendingUiWork();
        pending.Request(UiWork.Chrome);

        Assert.False(pending.TryTake(UiWork.None, out UiWork taken));

        Assert.Equal(UiWork.None, taken);
        Assert.True(pending.Has(UiWork.Chrome));
    }

    [Fact]
    public void Unfinished_work_is_put_back()
    {
        var pending = new PendingUiWork();
        pending.Request(UiWork.TabBuild | UiWork.Chrome);
        pending.TryTake(UiWork.TabBuild | UiWork.Chrome, out UiWork taken);

        pending.Restore(taken & UiWork.TabBuild);

        Assert.True(pending.Has(UiWork.TabBuild));
        Assert.False(pending.Has(UiWork.Chrome));
    }

    [Fact]
    public void A_request_made_while_a_pass_runs_survives_the_pass_finishing()
    {
        var pending = new PendingUiWork();
        pending.Request(UiWork.Chrome);
        pending.TryTake(UiWork.Chrome, out _);

        pending.Request(UiWork.Chrome); // a size change during the pass
        pending.Restore(UiWork.None); // the pass succeeded

        Assert.True(pending.Has(UiWork.Chrome));
    }

    [Fact]
    public void A_replay_with_nothing_waiting_has_nothing_to_post()
    {
        var pending = new PendingUiWork();
        pending.Request(UiWork.Chrome);
        pending.TryTake(UiWork.Chrome, out _);

        Assert.False(pending.Any); // Replay posts a pass only when Any: a finished pass is never asked again, so a handler event after it cannot loop
    }

    [Fact]
    public void Tab_build_without_the_tabs_handler_is_not_runnable_so_a_replay_does_not_loop()
    {
        UiWork runnable = UiWorkRules.Runnable(pageHandler: true, tabsHandler: false, windowSized: true);

        Assert.False(UiWorkRules.ShouldReplay(UiWork.TabBuild, runnable));
        Assert.True(UiWorkRules.ShouldReplay(UiWork.TabBuild, UiWorkRules.Runnable(true, true, true)));
    }

    [Fact]
    public void Nothing_is_runnable_without_the_page_handler_and_chrome_needs_a_size()
    {
        Assert.Equal(UiWork.None, UiWorkRules.Runnable(false, true, true));
        Assert.False(UiWorkRules.Runnable(true, true, false).HasFlag(UiWork.Chrome));
        Assert.True(UiWorkRules.Runnable(true, true, true).HasFlag(UiWork.Chrome));
    }

    [Fact]
    public void A_forced_shelf_that_did_not_apply_is_retried_once_then_dropped()
    {
        UiWork first = UiWork.Shelf | UiWork.ShelfForce;
        UiWork back = UiWorkRules.ShelfUnfinished(first, applied: false);
        Assert.Equal(first | UiWork.ShelfRetried, back);

        Assert.Equal(UiWork.None, UiWorkRules.ShelfUnfinished(back, applied: false)); // the retry did not apply either: no loop
        Assert.Equal(UiWork.None, UiWorkRules.ShelfUnfinished(first, applied: true));
    }

    [Fact]
    public void A_check_only_shelf_stays_a_check_when_put_back()
    {
        Assert.Equal(UiWork.Shelf | UiWork.ShelfRetried, UiWorkRules.ShelfUnfinished(UiWork.Shelf, applied: false));
    }

    [Fact]
    public void A_new_shelf_trigger_gives_a_new_retry_allowance()
    {
        var pending = new PendingUiWork();
        pending.Restore(UiWorkRules.ShelfUnfinished(UiWork.Shelf, applied: false));
        Assert.True(pending.Has(UiWork.ShelfRetried));

        pending.Request(UiWork.Shelf);

        Assert.False(pending.Has(UiWork.ShelfRetried));
    }

    [Fact]
    public void Restore_alone_does_not_clear_the_retry_mark()
    {
        var pending = new PendingUiWork();
        pending.Restore(UiWork.Shelf | UiWork.ShelfRetried);

        Assert.True(pending.Has(UiWork.ShelfRetried));
        Assert.Equal(UiWork.Shelf | UiWork.ShelfRetried, pending.Snapshot());
    }

    [Fact]
    public void A_failed_pass_leaves_the_bit_for_the_next_replay()
    {
        var pending = new PendingUiWork();
        pending.Request(UiWork.Chrome);
        pending.TryTake(UiWork.Chrome, out UiWork taken);

        pending.Restore(taken);

        Assert.True(pending.Any);
    }
}
