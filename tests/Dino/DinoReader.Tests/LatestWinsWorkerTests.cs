using DinoReader.Core;
using Xunit;

namespace DinoReader.Tests;

public class LatestWinsWorkerTests
{
    [Fact]
    public async Task A_use_during_a_run_is_served_once_with_the_latest_value()
    {
        var served = new List<string>();
        var gate = new TaskCompletionSource();
        var finished = new TaskCompletionSource();
        var worker = new LatestWinsWorker<string>(async v =>
        {
            served.Add(v);
            if (v == "fa") await gate.Task;
            if (v == "ar") finished.TrySetResult();
        }, _ => { });

        worker.Use("fa");
        worker.Use("ru");
        worker.Use("ar");
        gate.SetResult();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(["fa", "ar"], served);
    }

    [Fact]
    public async Task A_failed_run_is_reported_and_the_loop_keeps_serving()
    {
        var errors = new List<Exception>();
        var done = new TaskCompletionSource();
        var worker = new LatestWinsWorker<string>(v =>
        {
            if (v == "fa") throw new InvalidOperationException("boom");
            done.TrySetResult();
            return Task.CompletedTask;
        }, errors.Add);

        worker.Use("fa");
        worker.Use("ar");
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(errors);
    }
}
