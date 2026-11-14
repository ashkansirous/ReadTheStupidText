namespace DinoReader.Tests;

/// <summary>Slice 377 item 16: the connection-cancel scope, driven through its seam (no MAUI).</summary>
public sealed class ConnectionCancelScopeTests
{
    private sealed class FakeConnection
    {
        public bool ShouldStop;
        public EventHandler? Handlers;
        public int Subscribed => Handlers?.GetInvocationList().Length ?? 0;

        public ConnectionCancelScope Watch(CancellationToken outer) =>
            new(() => ShouldStop, h => Handlers += h, h => Handlers -= h, outer);

        public void Raise() => Handlers?.Invoke(this, EventArgs.Empty);
    }

    [Fact]
    public void ALinkAlreadyStoppingWhenTheScopeStarts_CancelsAtOnce_WithoutAnyEvent()
    {
        var connection = new FakeConnection { ShouldStop = true };

        using ConnectionCancelScope scope = connection.Watch(CancellationToken.None);

        Assert.True(scope.Token.IsCancellationRequested);
        Assert.True(scope.StoppedByConnection);
    }

    [Fact]
    public void AChangeToAStoppingLink_CancelsAndIsMarkedAsTheConnections()
    {
        var connection = new FakeConnection();
        using ConnectionCancelScope scope = connection.Watch(CancellationToken.None);
        Assert.False(scope.Token.IsCancellationRequested);

        connection.ShouldStop = true;
        connection.Raise();

        Assert.True(scope.Token.IsCancellationRequested);
        Assert.True(scope.StoppedByConnection);
    }

    [Fact]
    public void AChangeToAnAcceptableLink_CancelsNothing()
    {
        var connection = new FakeConnection();
        using ConnectionCancelScope scope = connection.Watch(CancellationToken.None);

        connection.Raise();

        Assert.False(scope.Token.IsCancellationRequested);
        Assert.False(scope.StoppedByConnection);
    }

    [Fact]
    public void AnOuterCancelFirst_IsNotMarkedAsTheConnections_WhenTheLinkStopsAfterwards()
    {
        var connection = new FakeConnection();
        using var outer = new CancellationTokenSource();
        using ConnectionCancelScope scope = connection.Watch(outer.Token);

        outer.Cancel();
        connection.ShouldStop = true;
        connection.Raise();

        Assert.True(scope.Token.IsCancellationRequested);
        Assert.False(scope.StoppedByConnection);
    }

    [Fact]
    public void AChangeAfterDispose_IsIgnoredAndTheHandlerIsGone()
    {
        var connection = new FakeConnection();
        ConnectionCancelScope scope = connection.Watch(CancellationToken.None);
        scope.Dispose();
        Assert.Equal(0, connection.Subscribed);

        connection.ShouldStop = true;
        connection.Raise();

        Assert.False(scope.StoppedByConnection);
    }

    [Fact]
    public void AnEventRacingTheDispose_NeverThrowsAndLeavesTheFlagUnset()
    {
        var connection = new FakeConnection();
        ConnectionCancelScope scope = connection.Watch(CancellationToken.None);
        EventHandler? stale = connection.Handlers; // the platform thread still holds the handler it was about to call
        scope.Dispose();
        connection.ShouldStop = true;

        stale!.Invoke(this, EventArgs.Empty);

        Assert.False(scope.StoppedByConnection);
    }
}
