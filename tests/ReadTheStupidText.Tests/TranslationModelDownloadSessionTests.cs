using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Mobile.Shared.ModelDownload;

namespace ReadTheStupidText.Tests;

public class TranslationModelDownloadSessionTests
{
    private sealed class FakeNetwork : ITranslationNetwork
    {
        public bool IsOnline { get; set; } = true;

        public IReadOnlyCollection<NetworkProfile> Profiles { get; set; } = [NetworkProfile.WiFi];

        public event EventHandler? Changed;

        public void Change(bool online, params NetworkProfile[] profiles)
        {
            IsOnline = online;
            Profiles = profiles;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    private static TranslationModelDownloadSession Session(FakeNetwork network, Func<int, Task> attempt, bool mobile = false)
    {
        int calls = 0;
        return new TranslationModelDownloadSession("mlkit-fa", (_, _) => attempt(Interlocked.Increment(ref calls)), network, mobile);
    }

    private static async Task WaitUntilAsync(TranslationModelDownloadSession session, TranslationDownloadState state)
    {
        DateTime end = DateTime.UtcNow + Limit;
        while (session.Snapshot.State != state)
        {
            Assert.True(DateTime.UtcNow < end, $"never reached {state}, is {session.Snapshot.State}");
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task A_good_attempt_is_done()
    {
        using var session = Session(new FakeNetwork(), _ => Task.CompletedTask);
        await session.RunAsync(CancellationToken.None).WaitAsync(Limit);
        Assert.Equal(TranslationDownloadState.Done, session.Snapshot.State);
    }

    [Fact]
    public async Task A_failure_with_internet_stays_failed()
    {
        using var session = Session(new FakeNetwork(), _ => throw new InvalidOperationException("x"));
        await Assert.ThrowsAsync<TranslationModelUnavailableException>(() => session.RunAsync(CancellationToken.None).WaitAsync(Limit));
        Assert.Equal(TranslationDownloadState.Failed, session.Snapshot.State);
    }

    [Fact]
    public async Task A_failure_offline_waits_and_the_network_coming_back_restarts_it()
    {
        var network = new FakeNetwork { IsOnline = false };
        using var session = Session(network, call => call == 1 ? throw new InvalidOperationException("offline") : Task.CompletedTask);
        Task run = session.RunAsync(CancellationToken.None);
        await WaitUntilAsync(session, TranslationDownloadState.WaitingForInternet);
        network.Change(true, NetworkProfile.WiFi);
        await run.WaitAsync(Limit);
        Assert.Equal(TranslationDownloadState.Done, session.Snapshot.State);
    }

    [Fact]
    public async Task Mobile_data_does_not_restart_unless_accepted_but_retry_always_does()
    {
        var network = new FakeNetwork { IsOnline = false };
        using var session = Session(network, call => call == 1 ? throw new InvalidOperationException("offline") : Task.CompletedTask);
        Task run = session.RunAsync(CancellationToken.None);
        await WaitUntilAsync(session, TranslationDownloadState.WaitingForInternet);
        network.Change(true, NetworkProfile.Cellular);
        await Task.Delay(100);
        Assert.Equal(TranslationDownloadState.WaitingForInternet, session.Snapshot.State);
        session.Retry();
        await run.WaitAsync(Limit);
        Assert.Equal(TranslationDownloadState.Done, session.Snapshot.State);
    }

    [Fact]
    public async Task A_failure_after_a_network_restart_with_internet_ends_failed_with_the_cause()
    {
        var network = new FakeNetwork { IsOnline = false };
        using var session = Session(network, call => throw new InvalidOperationException(call == 1 ? "offline" : "storage full"));
        Task run = session.RunAsync(CancellationToken.None);
        await WaitUntilAsync(session, TranslationDownloadState.WaitingForInternet);
        network.Change(true, NetworkProfile.WiFi);
        TranslationModelUnavailableException failure = await Assert.ThrowsAsync<TranslationModelUnavailableException>(() => run.WaitAsync(Limit));
        Assert.Equal("storage full", failure.InnerException?.Message);
        Assert.Equal(TranslationDownloadState.Failed, session.Snapshot.State);
    }

    [Fact]
    public async Task Flapping_wifi_keeps_waiting_when_the_restarted_attempt_fails_offline()
    {
        var network = new FakeNetwork { IsOnline = false };
        using var session = Session(network, call =>
        {
            if (call == 2)
            {
                network.IsOnline = false; // the Wi-Fi dropped again during the restarted attempt
            }

            return call <= 2 ? throw new InvalidOperationException("offline") : Task.CompletedTask;
        });
        Task run = session.RunAsync(CancellationToken.None);
        await WaitUntilAsync(session, TranslationDownloadState.WaitingForInternet);
        network.Change(true, NetworkProfile.WiFi);
        await Task.Delay(300);
        Assert.Equal(TranslationDownloadState.WaitingForInternet, session.Snapshot.State);
        network.Change(true, NetworkProfile.WiFi);
        await run.WaitAsync(Limit);
        Assert.Equal(TranslationDownloadState.Done, session.Snapshot.State);
    }

    [Fact]
    public async Task A_stalled_offline_attempt_waits_even_if_the_network_is_back_by_the_time_it_settles()
    {
        var network = new FakeNetwork { IsOnline = false, Profiles = [NetworkProfile.Cellular] };
        // mobile data: a back-online network does not restart it by itself, so the wait stays visible
        int calls = 0;
        using var session = new TranslationModelDownloadSession("mlkit-fa", async (_, token) =>
        {
            if (Interlocked.Increment(ref calls) > 1)
            {
                return;
            }

            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            finally
            {
                network.IsOnline = true; // connectivity returns right after the watchdog cancelled the attempt
            }
        }, network, false, TimeSpan.FromMilliseconds(120));
        Task run = session.RunAsync(CancellationToken.None);
        await WaitUntilAsync(session, TranslationDownloadState.WaitingForInternet);
        session.Retry();
        await run.WaitAsync(Limit);
        Assert.Equal(TranslationDownloadState.Done, session.Snapshot.State);
    }

    [Fact]
    public async Task A_download_with_no_progress_and_no_internet_for_the_stall_limit_waits()
    {
        var network = new FakeNetwork { IsOnline = false };
        using var session = new TranslationModelDownloadSession("mlkit-fa", (_, token) => Task.Delay(Timeout.Infinite, token), network, false, TimeSpan.FromMilliseconds(120));
        Task run = session.RunAsync(CancellationToken.None);
        await WaitUntilAsync(session, TranslationDownloadState.WaitingForInternet);
        Assert.True(session.WaitedTotal >= TimeSpan.Zero);
        session.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(Limit));
    }

    [Fact]
    public async Task A_slow_download_with_internet_is_not_a_stall()
    {
        using var session = new TranslationModelDownloadSession("mlkit-fa", (_, _) => Task.Delay(400), new FakeNetwork(), false, TimeSpan.FromMilliseconds(120));
        await session.RunAsync(CancellationToken.None).WaitAsync(Limit);
        Assert.Equal(TranslationDownloadState.Done, session.Snapshot.State);
    }

    [Fact]
    public async Task Cancel_while_waiting_is_final_and_nothing_restarts()
    {
        var network = new FakeNetwork { IsOnline = false };
        int attempts = 0;
        using var session = Session(network, _ =>
        {
            Interlocked.Increment(ref attempts);
            throw new InvalidOperationException("offline");
        });
        Task run = session.RunAsync(CancellationToken.None);
        await WaitUntilAsync(session, TranslationDownloadState.WaitingForInternet);
        session.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(Limit));
        network.Change(true, NetworkProfile.WiFi);
        session.Retry();
        await Task.Delay(100);
        Assert.Equal(TranslationDownloadState.Cancelled, session.Snapshot.State);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Cancelling_the_caller_token_cancels_a_running_attempt()
    {
        var started = new TaskCompletionSource();
        using var session = new TranslationModelDownloadSession("mlkit-fa", async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        }, new FakeNetwork(), false);
        using var cts = new CancellationTokenSource();
        Task run = session.RunAsync(cts.Token);
        await started.Task.WaitAsync(Limit);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(Limit));
        Assert.Equal(TranslationDownloadState.Cancelled, session.Snapshot.State);
    }

    [Fact]
    public async Task Progress_is_reported_while_downloading()
    {
        var seen = new List<double?>();
        using var session = new TranslationModelDownloadSession("mlkit-fa", (progress, _) =>
        {
            progress.Report(0.4);
            return Task.Delay(50);
        }, new FakeNetwork(), false);
        session.Changed += snapshot => seen.Add(snapshot.Progress);
        await session.RunAsync(CancellationToken.None).WaitAsync(Limit);
        Assert.Contains(0.4, seen);
    }

    [Theory]
    [InlineData("mlkit-fa", new[] { "en", "fa" })]
    [InlineData("mlkit-de+fa", new[] { "de", "fa" })]
    [InlineData("mlkit-en", new[] { "en", "en" })]
    public void The_pack_key_names_the_non_english_languages(string expected, string[] languages)
    {
        Assert.Equal(expected, TranslationModelDownloadSession.PackKey(languages));
    }
}
