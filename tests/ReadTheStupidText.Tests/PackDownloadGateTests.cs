using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

/// <summary>Two downloads of one voice pack never run together (Slice 173); different packs do not wait for each other.</summary>
public class PackDownloadGateTests
{
    [Fact]
    public async Task A_second_download_of_the_same_pack_waits_for_the_first()
    {
        using IDisposable first = await PackDownloadGate.EnterAsync("gate-test-same", CancellationToken.None);

        Task<IDisposable> second = PackDownloadGate.EnterAsync("gate-test-same", CancellationToken.None);
        await Task.Delay(50);
        Assert.False(second.IsCompleted);

        first.Dispose();
        using IDisposable entered = await second.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Different_packs_do_not_wait_for_each_other()
    {
        using IDisposable first = await PackDownloadGate.EnterAsync("gate-test-a", CancellationToken.None);

        using IDisposable other = await PackDownloadGate.EnterAsync("gate-test-b", CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(other);
    }

    [Fact]
    public async Task A_waiting_download_that_is_cancelled_leaves_the_gate_to_the_next()
    {
        using IDisposable first = await PackDownloadGate.EnterAsync("gate-test-cancel", CancellationToken.None);
        using var cancel = new CancellationTokenSource();

        Task<IDisposable> waiting = PackDownloadGate.EnterAsync("gate-test-cancel", cancel.Token);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);

        first.Dispose();
        using IDisposable next = await PackDownloadGate.EnterAsync("gate-test-cancel", CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
    }
}
