using DinoReader.Core;
using Xunit;

namespace DinoReader.Tests;

public class ScanRequestsTests
{
    [Fact]
    public void FirstRequestStartsTheLoopAndRunsOneScan()
    {
        var requests = new ScanRequests();
        Assert.True(requests.Request());
        Assert.True(requests.TakeNext());
        Assert.False(requests.TakeNext());
    }

    [Fact]
    public void ManyRequestsDuringAScanGiveExactlyOneMoreScan()
    {
        var requests = new ScanRequests();
        Assert.True(requests.Request());
        Assert.True(requests.TakeNext()); // the scan runs
        Assert.False(requests.Request());
        Assert.False(requests.Request());
        Assert.False(requests.Request());
        Assert.True(requests.TakeNext()); // one rerun
        Assert.False(requests.TakeNext());
    }

    [Fact]
    public void ARequestAfterTheLoopEndedStartsANewLoop()
    {
        var requests = new ScanRequests();
        Assert.True(requests.Request());
        Assert.True(requests.TakeNext());
        Assert.False(requests.TakeNext());
        Assert.True(requests.Request());
        Assert.True(requests.TakeNext());
    }
}
