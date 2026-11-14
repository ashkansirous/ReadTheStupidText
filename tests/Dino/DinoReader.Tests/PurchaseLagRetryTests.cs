using DinoReader;
using Xunit;

namespace DinoReader.Tests;

public class PurchaseLagRetryTests
{
    [Fact]
    public void The_pauses_are_the_fixed_schedule_and_total_about_84_seconds()
    {
        Assert.Equal([3, 6, 10, 15, 20, 30], PurchaseLagRetry.Pauses.Select(p => (int)p.TotalSeconds));
        Assert.Equal(84, PurchaseLagRetry.Pauses.Sum(p => p.TotalSeconds));
    }

    [Fact]
    public void The_tries_stop_after_the_last_pause()
    {
        int tries = 0;
        for (int retries = 0; PurchaseLagRetry.NextPause(retries) is not null; retries++)
        {
            tries++;
        }

        Assert.Equal(PurchaseLagRetry.MaxRetries, tries);
        Assert.Null(PurchaseLagRetry.NextPause(PurchaseLagRetry.MaxRetries));
        Assert.Null(PurchaseLagRetry.NextPause(-1));
    }

    [Fact]
    public async Task A_lagging_store_is_retried_after_each_pause_and_the_wait_is_shown_then_cleared()
    {
        int calls = 0;
        var delays = new List<TimeSpan>();
        var waiting = new List<bool>();

        int result = await PurchaseLagRetry.RunAsync(
            () => ++calls < 3 ? throw new InvalidOperationException("403") : Task.FromResult(42),
            ex => ex is InvalidOperationException,
            (pause, _) => { delays.Add(pause); return Task.CompletedTask; },
            waiting.Add,
            CancellationToken.None);

        Assert.Equal(42, result);
        Assert.Equal(3, calls);
        Assert.Equal([PurchaseLagRetry.Pauses[0], PurchaseLagRetry.Pauses[1]], delays);
        Assert.Equal([true, false, true, false], waiting);
    }

    [Fact]
    public async Task The_last_refusal_reaches_the_caller_after_the_retries_are_used_up()
    {
        int calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => PurchaseLagRetry.RunAsync<int>(
            () => { calls++; throw new InvalidOperationException("403"); },
            ex => true,
            (_, _) => Task.CompletedTask,
            _ => { },
            CancellationToken.None));

        Assert.Equal(PurchaseLagRetry.MaxRetries + 1, calls);
    }

    [Fact]
    public async Task Another_kind_of_failure_is_not_retried()
    {
        int calls = 0;

        await Assert.ThrowsAsync<ArgumentException>(() => PurchaseLagRetry.RunAsync<int>(
            () => { calls++; throw new ArgumentException("other"); },
            ex => ex is InvalidOperationException,
            (_, _) => Task.CompletedTask,
            _ => { },
            CancellationToken.None));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task A_cancel_during_the_wait_stops_the_retries_and_clears_the_wait()
    {
        int calls = 0;
        var waiting = new List<bool>();
        using var cancel = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PurchaseLagRetry.RunAsync<int>(
            () => { calls++; throw new InvalidOperationException("403"); },
            ex => true,
            (_, token) => { cancel.Cancel(); token.ThrowIfCancellationRequested(); return Task.CompletedTask; },
            waiting.Add,
            cancel.Token));

        Assert.Equal(1, calls);
        Assert.Equal([true, false], waiting);
    }
}
