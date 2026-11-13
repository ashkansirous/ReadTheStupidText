using System.Diagnostics;
using ReadTheStupidText.Application.Reading;
using Xunit;

namespace ReadTheStupidText.Tests;

public sealed class PackStallWatchTests
{
    private static long After(long start, double seconds) => start + (long)(seconds * Stopwatch.Frequency);

    private static PackStallClock NewClock(long start)
    {
        var clock = new PackStallClock(PackStallWatch.StallTimeout, TimeSpan.FromSeconds(15), start);
        clock.Arm(start);
        return clock;
    }

    [Fact]
    public void SlowConnectBeforeTheResponseIsNeverAStall()
    {
        long start = Stopwatch.GetTimestamp();
        var clock = new PackStallClock(PackStallWatch.StallTimeout, TimeSpan.FromSeconds(15), start);

        for (int second = 5; second <= 120; second += 5)
        {
            Assert.False(clock.Tick(After(start, second), active: true)); // 2 min of connecting, TLS or queueing
        }

        clock.Arm(After(start, 120)); // the response began
        for (int second = 125; second < 180; second += 5)
        {
            Assert.False(clock.Tick(After(start, second), active: true));
        }

        Assert.True(clock.Tick(After(start, 180), active: true));
    }

    [Fact]
    public void ArmingTwiceKeepsTheFirstStart()
    {
        long start = Stopwatch.GetTimestamp();
        var clock = new PackStallClock(PackStallWatch.StallTimeout, TimeSpan.FromSeconds(15), start);
        clock.Arm(start);
        clock.Tick(After(start, 5), active: true);
        clock.Arm(After(start, 30)); // a second headers callback must not give a silent download a fresh minute

        for (int second = 10; second < 60; second += 5)
        {
            Assert.False(clock.Tick(After(start, second), active: true));
        }

        Assert.True(clock.Tick(After(start, 60), active: true));
    }

    [Fact]
    public void DownloadStartedInTheBackgroundStallsOnceTheAppIsActive()
    {
        long start = Stopwatch.GetTimestamp();
        var clock = NewClock(start);

        for (int second = 5; second <= 600; second += 5)
        {
            Assert.False(clock.Tick(After(start, second), active: false)); // 10 min with the app backgrounded: not counted
        }

        for (int second = 605; second < 660; second += 5)
        {
            Assert.False(clock.Tick(After(start, second), active: true));
        }

        Assert.True(clock.Tick(After(start, 665), active: true)); // 60 s of active, silent time
    }

    [Fact]
    public void SlowButMovingDownloadNeverStalls()
    {
        long start = Stopwatch.GetTimestamp();
        var clock = NewClock(start);

        for (int second = 5; second <= 600; second += 5)
        {
            if (second % 40 == 0)
            {
                clock.Reset(After(start, second)); // a progress callback every 40 s
            }

            Assert.False(clock.Tick(After(start, second), active: true));
        }
    }

    [Fact]
    public void SilentActiveDownloadStallsAfterSixtySeconds()
    {
        long start = Stopwatch.GetTimestamp();
        var clock = NewClock(start);
        bool stalled = false;

        for (int second = 5; second <= 55; second += 5)
        {
            stalled |= clock.Tick(After(start, second), active: true);
        }

        Assert.False(stalled);
        Assert.True(clock.Tick(After(start, 60), active: true));
    }

    [Fact]
    public void ResumeAfterSuspendGetsAFreshSixtySeconds()
    {
        long start = Stopwatch.GetTimestamp();
        var clock = NewClock(start);
        clock.Tick(After(start, 5), active: true);
        clock.Tick(After(start, 10), active: true);

        Assert.False(clock.Tick(After(start, 900), active: true)); // suspended 15 min, the timer ran once on wake
        for (int second = 905; second < 960; second += 5)
        {
            Assert.False(clock.Tick(After(start, second), active: true));
        }

        Assert.True(clock.Tick(After(start, 960), active: true));
    }

    [Fact]
    public void InactiveOrOfflineTicksDoNotCountAndStartAFreshTimeout()
    {
        long start = Stopwatch.GetTimestamp();
        var clock = NewClock(start);

        for (int second = 5; second <= 120; second += 5)
        {
            Assert.False(clock.Tick(After(start, second), active: false));
        }

        for (int second = 125; second < 180; second += 5)
        {
            Assert.False(clock.Tick(After(start, second), active: true));
        }

        Assert.True(clock.Tick(After(start, 185), active: true));
    }

    [Fact]
    public void ProgressGateLetsOneLinePerIntervalThrough()
    {
        var gate = new PackProgressLogGate(PackStallWatch.ProgressInterval);
        long start = Stopwatch.GetTimestamp();

        Assert.True(gate.ShouldLog(start));
        Assert.False(gate.ShouldLog(After(start, 4)));
        Assert.True(gate.ShouldLog(After(start, 5)));
        Assert.False(gate.ShouldLog(After(start, 6)));
        Assert.True(gate.ShouldLog(After(start, 10.5)));
    }
}
