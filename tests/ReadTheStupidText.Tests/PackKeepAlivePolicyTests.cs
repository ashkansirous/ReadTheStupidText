using ReadTheStupidText.Mobile.Shared;

namespace ReadTheStupidText.Tests;

public class PackKeepAlivePolicyTests
{
    [Theory]
    [InlineData(true, false, false)] // running service (grace or active): joined, nothing started, no visibility needed
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]  // cold start needs the app on screen
    [InlineData(false, false, false)]
    public void A_running_service_is_joined_and_only_a_cold_start_needs_the_app_visible(bool running, bool visible, bool start) =>
        Assert.Equal(start, PackKeepAlivePolicy.ShouldStartService(running, visible));

    [Fact]
    public void The_grace_timer_stops_the_service_only_when_nothing_is_registered_and_it_is_the_latest_timer()
    {
        Assert.True(PackKeepAlivePolicy.ShouldStopAfterGrace(0, 3, 3));
        Assert.False(PackKeepAlivePolicy.ShouldStopAfterGrace(1, 3, 3)); // a pack joined
        Assert.False(PackKeepAlivePolicy.ShouldStopAfterGrace(0, 3, 4)); // a later Begin or End superseded it
    }

    [Fact]
    public void A_throwing_cancel_handler_does_not_stop_the_others()
    {
        var seen = new List<string>();
        Action<string> first = id => seen.Add("first" + id);
        Action<string> throwing = _ => throw new InvalidOperationException();
        Action<string> last = id => seen.Add("last" + id);
        PackDownloadHost.CancelRequested += first;
        PackDownloadHost.CancelRequested += throwing;
        PackDownloadHost.CancelRequested += last;
        try
        {
            PackDownloadHost.RaiseCancelRequested("p");
        }
        finally
        {
            PackDownloadHost.CancelRequested -= first;
            PackDownloadHost.CancelRequested -= throwing;
            PackDownloadHost.CancelRequested -= last;
        }

        Assert.Equal(["lastp", "firstp"], seen); // newest first
    }
}
