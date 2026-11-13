using ReadTheStupidText.Mobile.Shared.ModelDownload;

namespace ReadTheStupidText.Tests;

public class SingleModelCallTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task An_abandoned_call_is_reused_not_started_again()
    {
        var single = new SingleModelCall();
        var release = new TaskCompletionSource();
        int starts = 0;
        Func<Task> start = () => { Interlocked.Increment(ref starts); return release.Task; };
        using var cancel = new CancellationTokenSource();
        Task first = single.RunAsync("fa", start, cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Limit));
        Task second = single.RunAsync("fa", start, CancellationToken.None);
        release.SetResult();
        await second.WaitAsync(Limit);
        Assert.Equal(1, starts);
    }

    [Fact]
    public async Task A_finished_call_lets_the_next_one_start()
    {
        var single = new SingleModelCall();
        int starts = 0;
        Func<Task> start = () => { Interlocked.Increment(ref starts); return Task.CompletedTask; };
        await single.RunAsync("fa", start, CancellationToken.None);
        await single.RunAsync("fa", start, CancellationToken.None);
        Assert.Equal(2, starts);
    }

    [Fact]
    public async Task A_call_for_another_pair_waits_for_the_running_one()
    {
        var single = new SingleModelCall();
        var release = new TaskCompletionSource();
        int starts = 0;
        using var cancel = new CancellationTokenSource();
        Task first = single.RunAsync("fa", () => { Interlocked.Increment(ref starts); return release.Task; }, cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Limit));
        Task other = single.RunAsync("de", () => { Interlocked.Increment(ref starts); return Task.CompletedTask; }, CancellationToken.None);
        await Task.Delay(100);
        Assert.Equal(1, starts);
        release.SetResult();
        await other.WaitAsync(Limit);
        Assert.Equal(2, starts);
    }
}
