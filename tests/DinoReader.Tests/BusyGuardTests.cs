using Xunit;

namespace DinoReader.Tests;

/// <summary>The cast screen's play button waits for an edit in flight instead of being dropped (Slice 113 follow-up).</summary>
public class BusyGuardTests
{
    [Fact]
    public async Task WhenFreeAsync_CompletesAtOnceWhenNothingRuns()
    {
        var guard = new BusyGuard();

        await guard.WhenFreeAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task WhenFreeAsync_WaitsForTheRunningActionAndDoesNotTakeTheGuard()
    {
        var guard = new BusyGuard();
        var release = new TaskCompletionSource();
        Task running = guard.RunAsync(() => release.Task);

        Task waiting = guard.WhenFreeAsync();
        Assert.False(waiting.IsCompleted);

        release.SetResult();
        await running;
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        bool ranAfter = false;
        await guard.RunAsync(() => { ranAfter = true; return Task.CompletedTask; });
        Assert.True(ranAfter);
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

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(5)));
        release.SetResult();
        await running;
    }
}
