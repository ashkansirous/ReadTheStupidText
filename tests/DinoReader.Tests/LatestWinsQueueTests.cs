using DinoReader.Core;

namespace DinoReader.Tests;

public class LatestWinsQueueTests
{
    private static int Merge(int a, int b) => a | b;

    [Fact]
    public async Task Request_WhenIdle_RunsThePassAtOnce()
    {
        var seen = new List<int>();
        var queue = new LatestWinsQueue<int>(Merge, r => { seen.Add(r); return Task.FromResult(true); }, _ => { });

        Task<bool> task = queue.Request(1);

        Assert.Equal([1], seen); // synchronous: a pass that never awaits has run before Request returns
        Assert.True(await task);
    }

    [Fact]
    public async Task Request_DuringAPass_IsMergedIntoOnePendingPassAfterIt()
    {
        var seen = new List<int>();
        var gate = new TaskCompletionSource();
        LatestWinsQueue<int>? queue = null;
        queue = new LatestWinsQueue<int>(Merge, async r =>
        {
            seen.Add(r);
            if (r == 1)
            {
                _ = queue!.Request(2);
                _ = queue.Request(4);
                await gate.Task;
            }

            return true;
        }, _ => { });

        Task<bool> first = queue.Request(1);
        gate.SetResult();
        await first;

        Assert.Equal([1, 6], seen); // 2 and 4 became one pass, never nested inside the first
    }

    [Fact]
    public async Task Request_ThatReplacesThePending_CompletesWithThePassThatServedIt()
    {
        var gate = new TaskCompletionSource();
        var queue = new LatestWinsQueue<int>(Merge, async r => { await gate.Task; return r == 1; }, _ => { });

        Task<bool> first = queue.Request(1);
        Task<bool> pending = queue.Request(2);
        gate.SetResult();

        Assert.True(await first);
        Assert.False(await pending);
    }

    [Fact]
    public async Task APassThatThrows_IsReportedAndTheNextRequestRunsFromScratch()
    {
        var errors = new List<Exception>();
        var seen = new List<int>();
        var queue = new LatestWinsQueue<int>(Merge, r =>
        {
            seen.Add(r);
            return r == 1 ? throw new InvalidOperationException("boom") : Task.FromResult(true);
        }, errors.Add);

        Assert.False(await queue.Request(1));
        Assert.True(await queue.Request(2));

        Assert.Single(errors);
        Assert.Equal([1, 2], seen);
    }

    [Fact]
    public async Task AFailingReporter_NeverStopsTheQueue()
    {
        var queue = new LatestWinsQueue<int>(Merge, r => throw new InvalidOperationException(), _ => throw new InvalidOperationException());

        Assert.False(await queue.Request(1));
        Assert.False(await queue.Request(2)); // would hang or fault if the first drain had left the queue marked running
    }
}
