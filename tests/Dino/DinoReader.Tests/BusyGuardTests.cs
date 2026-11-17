using Xunit;

namespace DinoReader.Tests;

/// <summary>The cast screen's play button waits for an edit in flight instead of being dropped (Slice 113 follow-up).</summary>
public class BusyGuardTests
{
    [Fact]
    public async Task WhenFreeAsync_CompletesAtOnceWhenNothingRuns()
    {
        var guard = new BusyGuard();

        await guard.WhenFreeAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WhenFreeAsync_WaitsForTheRunningActionAndDoesNotTakeTheGuard()
    {
        var guard = new BusyGuard();
        var release = new TaskCompletionSource();
        Task running = guard.RunAsync(() => release.Task);

        Task waiting = guard.WhenFreeAsync(TestContext.Current.CancellationToken);
        Assert.False(waiting.IsCompleted);

        release.SetResult();
        await running;
        await waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        bool ranAfter = false;
        await guard.RunAsync(() => { ranAfter = true; return Task.CompletedTask; });
        Assert.True(ranAfter);
    }

    [Fact]
    public async Task RunAsync_DropsATapWhileAnActionRuns()
    {
        var guard = new BusyGuard();
        var release = new TaskCompletionSource();
        Task running = guard.RunAsync(() => release.Task);
        bool ran = false;

        await guard.RunAsync(() => { ran = true; return Task.CompletedTask; });

        Assert.False(ran);
        Assert.True(guard.IsBusy);
        release.SetResult();
        await running;
        Assert.False(guard.IsBusy);
    }

    [Fact]
    public async Task RunWhenFreeAsync_QueuesEveryCallerAndRunsThemOneAtATime()
    {
        var guard = new BusyGuard();
        var release = new TaskCompletionSource();
        Task running = guard.RunAsync(() => release.Task);
        int inside = 0, maxInside = 0, done = 0;
        Func<Task> work = async () =>
        {
            maxInside = Math.Max(maxInside, Interlocked.Increment(ref inside));
            await Task.Yield();
            Interlocked.Decrement(ref inside);
            Interlocked.Increment(ref done);
        };

        Task first = guard.RunWhenFreeAsync(work, TestContext.Current.CancellationToken);
        Task second = guard.RunWhenFreeAsync(work, TestContext.Current.CancellationToken);
        Task third = guard.RunWhenFreeAsync(work, TestContext.Current.CancellationToken);
        Assert.Equal(0, done);

        release.SetResult();
        await running;
        await Task.WhenAll(first, second, third).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(3, done);
        Assert.Equal(1, maxInside);
    }

    [Fact]
    public async Task RunWhenFreeAsync_ACancelledWaitNeverRunsAndLeavesTheGuardFree()
    {
        var guard = new BusyGuard();
        var release = new TaskCompletionSource();
        Task running = guard.RunWhenFreeAsync(() => release.Task, TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        bool ran = false;

        Task queued = guard.RunWhenFreeAsync(() => { ran = true; return Task.CompletedTask; }, cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        release.SetResult();
        await running;

        Assert.False(ran);
        Assert.False(guard.IsBusy);
    }

    [Fact]
    public async Task RunWhenFreeAsync_ReleasesTheGuardWhenTheWorkThrows()
    {
        var guard = new BusyGuard();

        await Assert.ThrowsAsync<InvalidOperationException>(() => guard.RunWhenFreeAsync(() => throw new InvalidOperationException(), TestContext.Current.CancellationToken));

        Assert.False(guard.IsBusy);
    }

    [Fact]
    public async Task WhenFreeAsync_EndsWhenCancelledWhileTheActionStillRuns()
    {
        var guard = new BusyGuard();
        var release = new TaskCompletionSource();
        Task running = guard.RunAsync(() => release.Task);
        using var cancel = new CancellationTokenSource();

        Task waiting = guard.WhenFreeAsync(cancel.Token);
        cancel.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        release.SetResult();
        await running;
    }
}
