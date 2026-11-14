using DinoReader;
using Xunit;

namespace DinoReader.Tests;

public class PurchaseLagRetryTests
{
    private static readonly TimeSpan NegativeCacheOfTheWorker = TimeSpan.FromSeconds(30); // NOT_OWNED_CACHE_MS in worker/entitlements/gate.ts

    [Fact]
    public void A_book_not_just_bought_is_never_retried() =>
        Assert.Null(PurchaseLagRetry.NextPause(null, 0));

    [Fact]
    public void A_purchase_older_than_the_window_is_taken_at_its_word() =>
        Assert.Null(PurchaseLagRetry.NextPause(PurchaseLagRetry.Window + TimeSpan.FromSeconds(1), 0));

    [Fact]
    public void Every_pause_outlasts_the_workers_negative_cache_and_the_tries_stop()
    {
        int tries = 0;
        for (int retries = 0; PurchaseLagRetry.NextPause(TimeSpan.FromSeconds(10), retries) is { } pause; retries++)
        {
            Assert.True(pause > NegativeCacheOfTheWorker);
            tries++;
        }

        Assert.Equal(PurchaseLagRetry.MaxRetries, tries);
        Assert.Null(PurchaseLagRetry.NextPause(TimeSpan.FromSeconds(10), PurchaseLagRetry.MaxRetries));
    }

    [Fact]
    public void The_total_wait_stays_inside_the_window()
    {
        TimeSpan total = TimeSpan.Zero;
        for (int retries = 0; PurchaseLagRetry.NextPause(TimeSpan.Zero, retries) is { } pause; retries++)
        {
            total += pause;
        }

        Assert.True(total < PurchaseLagRetry.Window);
    }

    [Fact]
    public async Task A_lagging_store_is_retried_after_the_pause_and_the_wait_is_shown_then_cleared()
    {
        int calls = 0;
        var delays = new List<TimeSpan>();
        var waiting = new List<bool>();

        int result = await PurchaseLagRetry.RunAsync(
            () => ++calls < 3 ? throw new InvalidOperationException("403") : Task.FromResult(42),
            ex => ex is InvalidOperationException,
            () => TimeSpan.FromSeconds(5),
            (pause, _) => { delays.Add(pause); return Task.CompletedTask; },
            waiting.Add,
            CancellationToken.None);

        Assert.Equal(42, result);
        Assert.Equal(3, calls);
        Assert.Equal([PurchaseLagRetry.Pause, PurchaseLagRetry.Pause], delays);
        Assert.Equal([true, false, true, false], waiting);
    }

    [Fact]
    public async Task A_book_not_just_bought_throws_at_once_without_waiting()
    {
        var delays = new List<TimeSpan>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => PurchaseLagRetry.RunAsync<int>(
            () => throw new InvalidOperationException("403"),
            ex => true,
            () => null,
            (pause, _) => { delays.Add(pause); return Task.CompletedTask; },
            _ => { },
            CancellationToken.None));

        Assert.Empty(delays);
    }

    [Fact]
    public async Task The_last_refusal_reaches_the_caller_after_the_retries_are_used_up()
    {
        int calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => PurchaseLagRetry.RunAsync<int>(
            () => { calls++; throw new InvalidOperationException("403"); },
            ex => true,
            () => TimeSpan.FromSeconds(1),
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
            () => TimeSpan.Zero,
            (_, _) => Task.CompletedTask,
            _ => { },
            CancellationToken.None));

        Assert.Equal(1, calls);
    }
}
