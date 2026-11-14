using DinoReader.Core;

namespace DinoReader.Tests;

public class SettleGateTests
{
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(80);

    [Fact]
    public async Task FinishedWork_ReturnsTrue_AndReportsNothing()
    {
        int timeouts = 0;
        bool settled = await new SettleGate().WaitAsync(Task.CompletedTask, TimeSpan.FromSeconds(5), () => timeouts++, CancellationToken.None);
        Assert.True(settled);
        Assert.Equal(0, timeouts);
    }

    [Fact]
    public async Task SlowWork_TimesOutOnce_ForEveryCaller()
    {
        var gate = new SettleGate();
        var never = new TaskCompletionSource();
        int timeouts = 0;
        Task<bool> first = gate.WaitAsync(never.Task, Short, () => Interlocked.Increment(ref timeouts), CancellationToken.None);
        Task<bool> second = gate.WaitAsync(never.Task, Short, () => Interlocked.Increment(ref timeouts), CancellationToken.None);
        Assert.False(await first);
        Assert.False(await second);

        System.Diagnostics.Stopwatch later = System.Diagnostics.Stopwatch.StartNew();
        Assert.False(await gate.WaitAsync(never.Task, TimeSpan.FromSeconds(30), () => Interlocked.Increment(ref timeouts), CancellationToken.None));
        Assert.True(later.Elapsed < TimeSpan.FromSeconds(5), "a caller after the timeout must not wait the budget again");
        Assert.Equal(1, timeouts);
    }

    [Fact]
    public async Task ThrowingTimeoutCallback_NeverFaultsTheSharedWait()
    {
        var gate = new SettleGate();
        var never = new TaskCompletionSource();
        Task<bool> first = gate.WaitAsync(never.Task, Short, () => throw new InvalidOperationException("report failed"), CancellationToken.None);
        Task<bool> second = gate.WaitAsync(never.Task, Short, () => { }, CancellationToken.None);
        Assert.False(await first);
        Assert.False(await second);
    }

    [Fact]
    public async Task ANewTask_StartsANewWait()
    {
        var gate = new SettleGate();
        int timeouts = 0;
        Assert.False(await gate.WaitAsync(new TaskCompletionSource().Task, Short, () => timeouts++, CancellationToken.None));
        Assert.False(await gate.WaitAsync(new TaskCompletionSource().Task, Short, () => timeouts++, CancellationToken.None));
        Assert.Equal(2, timeouts);
    }

    [Fact]
    public async Task ACallersCancel_StopsOnlyItsOwnWait()
    {
        var gate = new SettleGate();
        var work = new TaskCompletionSource();
        using var cancel = new CancellationTokenSource();
        Task<bool> cancelled = gate.WaitAsync(work.Task, TimeSpan.FromSeconds(30), () => { }, cancel.Token);
        Task<bool> other = gate.WaitAsync(work.Task, TimeSpan.FromSeconds(30), () => { }, CancellationToken.None);
        await cancel.CancelAsync();
        Assert.False(await cancelled);
        work.SetResult();
        Assert.True(await other);
    }
}
