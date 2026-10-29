using ReadTheStupidText.Application.Reading;
using Xunit;

namespace ReadTheStupidText.Tests;

public class PackResumeTests
{
    private static readonly NetworkProfile[] Wifi = [NetworkProfile.WiFi];
    private static readonly NetworkProfile[] Cellular = [NetworkProfile.Cellular];

    [Theory]
    [InlineData(PackDownloadResult.Offline, true)]
    [InlineData(PackDownloadResult.Failed, true)]
    [InlineData(PackDownloadResult.HashMismatch, false)]
    [InlineData(PackDownloadResult.InvalidPack, false)]
    [InlineData(PackDownloadResult.Ok, false)]
    public void Only_offline_and_failed_results_are_resumed(PackDownloadResult last, bool expected) =>
        Assert.Equal(expected, PackResumePolicy.ShouldResume(last, false, false, true, Wifi, false));

    [Fact]
    public void A_cancelled_download_never_restarts() =>
        Assert.False(PackResumePolicy.ShouldResume(PackDownloadResult.Offline, true, true, true, Wifi, false));

    [Fact]
    public void Nothing_restarts_without_a_network() =>
        Assert.False(PackResumePolicy.ShouldResume(PackDownloadResult.Offline, false, false, false, [], false));

    [Fact]
    public void Cellular_only_waits_unless_download_now()
    {
        Assert.False(PackResumePolicy.ShouldResume(PackDownloadResult.Offline, false, false, true, Cellular, false));
        Assert.True(PackResumePolicy.ShouldResume(PackDownloadResult.Offline, false, true, true, Cellular, false));
    }

    [Fact]
    public void Wifi_listed_next_to_cellular_counts_as_wifi() =>
        Assert.True(PackResumePolicy.ShouldResume(PackDownloadResult.Offline, false, false, true, [NetworkProfile.WiFi, NetworkProfile.Cellular], false));

    [Fact]
    public void Only_one_restart_per_trigger() =>
        Assert.False(PackResumePolicy.ShouldResume(PackDownloadResult.Offline, false, false, true, Wifi, true));

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    // A resume that signals when the coordinator called it and returns what the test says.
    private sealed class Probe(Func<Task<bool>> body)
    {
        private TaskCompletionSource _called = new();
        public int Calls;

        public Func<Task<bool>> Resume => async () =>
        {
            Calls++;
            var current = _called;
            try
            {
                return await body();
            }
            finally
            {
                current.TrySetResult();
            }
        };

        public async Task TriggerAndWaitAsync(PackResumeCoordinator coordinator, int expectedOffered = 1)
        {
            _called = new TaskCompletionSource();
            Assert.Equal(expectedOffered, coordinator.Trigger(PackResumeReason.Network, true, Wifi));
            if (expectedOffered > 0)
            {
                await _called.Task.WaitAsync(Wait);
                await Task.Delay(20); // the coordinator's bookkeeping after the call
            }
        }
    }

    [Fact]
    public async Task An_offline_pack_stays_after_a_restart_and_restarts_on_every_trigger()
    {
        var coordinator = new PackResumeCoordinator();
        var probe = new Probe(() => Task.FromResult(true));
        coordinator.Report("voice", PackRunEnd.Offline, probe.Resume);

        await probe.TriggerAndWaitAsync(coordinator);
        await probe.TriggerAndWaitAsync(coordinator);

        Assert.Equal(2, probe.Calls);
        Assert.Equal(PackResumeState.Waiting, coordinator.StateOf("voice"));
    }

    [Fact]
    public async Task A_busy_resume_keeps_the_entry()
    {
        var coordinator = new PackResumeCoordinator();
        var probe = new Probe(() => Task.FromResult(false));
        coordinator.Report("ocr:fa", PackRunEnd.Offline, probe.Resume);

        await probe.TriggerAndWaitAsync(coordinator);

        Assert.Equal(1, coordinator.TrackedCount);
        await probe.TriggerAndWaitAsync(coordinator); // still offered on the next trigger
    }

    [Fact]
    public async Task A_resume_that_throws_keeps_the_entry()
    {
        var coordinator = new PackResumeCoordinator();
        var probe = new Probe(() => throw new InvalidOperationException("boom"));
        coordinator.Report("voice", PackRunEnd.Offline, probe.Resume);

        await probe.TriggerAndWaitAsync(coordinator);

        Assert.Equal(PackResumeState.Waiting, coordinator.StateOf("voice"));
    }

    [Fact]
    public async Task A_failed_pack_gets_one_automatic_retry_then_needs_the_user()
    {
        var coordinator = new PackResumeCoordinator();
        Probe? probe = null;
        probe = new Probe(() =>
        {
            coordinator.Report("story-voice:fa", PackRunEnd.Failed, probe!.Resume); // the retried run fails again
            return Task.FromResult(true);
        });
        coordinator.Report("story-voice:fa", PackRunEnd.Failed, probe.Resume);
        Assert.Equal(PackResumeState.Waiting, coordinator.StateOf("story-voice:fa"));

        await probe.TriggerAndWaitAsync(coordinator);

        Assert.Equal(PackResumeState.NeedsUserRetry, coordinator.StateOf("story-voice:fa"));
        await probe.TriggerAndWaitAsync(coordinator, expectedOffered: 0);
        Assert.Equal(1, probe.Calls);
    }

    [Fact]
    public async Task A_failed_retry_that_found_the_pack_busy_is_not_spent()
    {
        var coordinator = new PackResumeCoordinator();
        var probe = new Probe(() => Task.FromResult(false));
        coordinator.Report("voice", PackRunEnd.Failed, probe.Resume);

        await probe.TriggerAndWaitAsync(coordinator);
        await probe.TriggerAndWaitAsync(coordinator);

        Assert.Equal(2, probe.Calls);
        Assert.Equal(PackResumeState.Waiting, coordinator.StateOf("voice"));
    }

    [Fact]
    public async Task Two_triggers_at_once_spend_the_failed_retry_once_and_the_second_does_not_give_it_back()
    {
        var coordinator = new PackResumeCoordinator();
        var release = new TaskCompletionSource();
        Probe? probe = null;
        probe = new Probe(async () =>
        {
            await release.Task;
            coordinator.Report("voice", PackRunEnd.Failed, probe!.Resume); // the retried run fails again
            return true;
        });
        coordinator.Report("voice", PackRunEnd.Failed, probe.Resume);

        Assert.Equal(1, coordinator.Trigger(PackResumeReason.Network, true, Wifi));
        Assert.Equal(0, coordinator.Trigger(PackResumeReason.Foreground, true, Wifi)); // the retry is already in use
        release.SetResult();

        await WaitUntilAsync(() => coordinator.StateOf("voice") == PackResumeState.NeedsUserRetry);
        Assert.Equal(1, probe.Calls);
    }

    [Fact]
    public async Task A_busy_restart_gives_back_only_a_retry_it_spent()
    {
        var coordinator = new PackResumeCoordinator();
        var probe = new Probe(() => Task.FromResult(false));
        coordinator.Report("voice", PackRunEnd.Failed, probe.Resume);

        await probe.TriggerAndWaitAsync(coordinator);
        await probe.TriggerAndWaitAsync(coordinator); // given back by the trigger that spent it, so the next one has it

        Assert.Equal(2, probe.Calls);
    }

    [Fact]
    public void An_offline_or_failed_end_of_a_run_older_than_the_last_cancel_is_dropped()
    {
        var coordinator = new PackResumeCoordinator();
        long started = coordinator.EpochOf("story-voice:fa");

        coordinator.Report("story-voice:fa", PackRunEnd.Cancelled, () => Task.FromResult(false));
        coordinator.Report("story-voice:fa", PackRunEnd.Offline, () => Task.FromResult(true), epoch: started);
        coordinator.Report("story-voice:fa", PackRunEnd.Failed, () => Task.FromResult(true), epoch: started);

        Assert.Equal(PackResumeState.None, coordinator.StateOf("story-voice:fa"));
        coordinator.Report("story-voice:fa", PackRunEnd.Offline, () => Task.FromResult(true), epoch: coordinator.EpochOf("story-voice:fa"));
        Assert.Equal(PackResumeState.Waiting, coordinator.StateOf("story-voice:fa"));
    }

    [Fact]
    public void A_cancel_of_one_key_leaves_the_other_keys_alone()
    {
        var coordinator = new PackResumeCoordinator();
        coordinator.Report("a", PackRunEnd.Cancelled, () => Task.FromResult(false));

        Assert.Equal(0, coordinator.EpochOf("b"));
        coordinator.Report("b", PackRunEnd.Offline, () => Task.FromResult(true), epoch: 0);
        Assert.Equal(PackResumeState.Waiting, coordinator.StateOf("b"));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime end = DateTime.UtcNow + Wait;
        while (!condition() && DateTime.UtcNow < end)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    [Fact]
    public void Offline_gives_a_later_failure_its_retry_again()
    {
        var coordinator = new PackResumeCoordinator();
        coordinator.Report("voice", PackRunEnd.Failed, () => Task.FromResult(true));
        coordinator.Report("voice", PackRunEnd.Offline, () => Task.FromResult(true));
        coordinator.Report("voice", PackRunEnd.Failed, () => Task.FromResult(true));

        Assert.Equal(PackResumeState.Waiting, coordinator.StateOf("voice"));
    }

    [Theory]
    [InlineData(PackRunEnd.Finished)]
    [InlineData(PackRunEnd.Cancelled)]
    public void Finished_and_cancelled_runs_leave(PackRunEnd end)
    {
        var coordinator = new PackResumeCoordinator();
        coordinator.Report("a", PackRunEnd.Offline, () => Task.FromResult(true));
        coordinator.Report("a", end, () => Task.FromResult(true));

        Assert.Equal(PackResumeState.None, coordinator.StateOf("a"));
        Assert.Equal(0, coordinator.Trigger(PackResumeReason.Network, true, Wifi));
    }

    [Fact]
    public void Download_now_is_the_only_way_a_waiting_pack_restarts_on_mobile_data()
    {
        var coordinator = new PackResumeCoordinator();
        coordinator.Report("a", PackRunEnd.Offline, () => Task.FromResult(true));
        coordinator.Report("b", PackRunEnd.Offline, () => Task.FromResult(true), downloadNow: true);

        Assert.Equal(1, coordinator.Trigger(PackResumeReason.Network, true, Cellular));
    }

    [Theory]
    [InlineData(PackDownloadResult.Ok, PackRunEnd.Finished)]
    [InlineData(PackDownloadResult.InvalidPack, PackRunEnd.Finished)]
    [InlineData(PackDownloadResult.HashMismatch, PackRunEnd.Finished)]
    [InlineData(PackDownloadResult.Offline, PackRunEnd.Offline)]
    [InlineData(PackDownloadResult.Failed, PackRunEnd.Failed)]
    public void A_download_result_maps_to_a_run_end(PackDownloadResult result, PackRunEnd expected) =>
        Assert.Equal(expected, IPackResume.EndOf(result));
}
