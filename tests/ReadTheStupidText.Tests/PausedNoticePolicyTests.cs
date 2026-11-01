using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared;

namespace ReadTheStupidText.Tests;

// Slice 233: one rule for the "Download paused. Tap to finish." notice on Android and iOS.
[Collection("PackDownloadHost")]
public class PausedNoticePolicyTests
{
    [Theory]
    [InlineData(PausedNoticeEnd.Resumable, false, PausedNoticeAction.Post)]
    [InlineData(PausedNoticeEnd.Resumable, true, PausedNoticeAction.Nothing)]
    [InlineData(PausedNoticeEnd.Cancelled, false, PausedNoticeAction.Clear)]
    [InlineData(PausedNoticeEnd.Cancelled, true, PausedNoticeAction.Clear)]
    [InlineData(PausedNoticeEnd.NotResumable, false, PausedNoticeAction.Clear)]
    [InlineData(PausedNoticeEnd.NotResumable, true, PausedNoticeAction.Clear)]
    [InlineData(PausedNoticeEnd.Finished, false, PausedNoticeAction.Clear)]
    [InlineData(PausedNoticeEnd.Finished, true, PausedNoticeAction.Clear)]
    public void Each_end_and_foreground_state_has_one_action(PausedNoticeEnd end, bool appInForeground, PausedNoticeAction expected)
    {
        Assert.Equal(expected, PausedNoticePolicy.Decide(end, appInForeground));
    }

    // Both platform hooks call Decide(Resumable, <their own foreground test>): whatever the platform, the answer depends only on that one flag.
    [Theory]
    [InlineData("android-on-screen", true, false)]
    [InlineData("android-background-time-limit", false, true)]
    [InlineData("android-background-network-lost", false, true)]
    [InlineData("ios-active", true, false)]
    [InlineData("ios-inactive", false, true)]
    [InlineData("ios-background", false, true)]
    public void A_resumable_end_posts_exactly_when_the_app_is_away_on_either_platform(string scenario, bool appInForeground, bool posts)
    {
        Assert.Equal(posts, PausedNoticePolicy.Decide(PausedNoticeEnd.Resumable, appInForeground) == PausedNoticeAction.Post);
        Assert.NotEmpty(scenario);
    }

    [Fact]
    public void The_paused_marker_keeps_the_pack_id_and_the_reason_as_separate_fields()
    {
        var lines = new List<string>();
        var previous = PackDownloadLog.Sink;
        PackDownloadLog.Sink = lines.Add;
        try
        {
            PackDownloadHost.LogService(PackDownloadHost.PausedNotifiedAction, "piper-fr", PackDownloadHost.PausedReasonOffline);
        }
        finally
        {
            PackDownloadLog.Sink = previous;
        }

        Assert.Contains(lines, line => line.EndsWith("paused-notified pack=piper-fr reason=offline", StringComparison.Ordinal));
    }

    [Fact]
    public void Paused_hands_the_end_reason_to_the_platform_notifier_which_logs_the_marker_only_if_it_posts()
    {
        var notifier = new NullNotifier();
        PackDownloadHost.PausedNotifier = notifier;
        try
        {
            PackDownloadHost.Paused("piper-fr", 0.5, PackDownloadHost.PausedReasonOffline);
        }
        finally
        {
            PackDownloadHost.PausedNotifier = null;
        }

        Assert.Equal(PackDownloadHost.PausedReasonOffline, notifier.Reason);
    }

    private sealed class NullNotifier : IPackPausedNotifier
    {
        public string? Reason { get; private set; }

        public void Post(string packId, double? fraction, string reason)
        {
            Reason = reason;
        }

        public void Clear(string packId)
        {
        }
    }
}

// Slice 233: the person is alerted once per pause episode; failed automatic retries post silently.
public class PausedEpisodesTests
{
    [Fact]
    public void The_first_post_of_an_episode_alerts_and_later_ones_are_silent()
    {
        var episodes = new PausedEpisodes();
        Assert.True(episodes.ClaimAlert("piper-fr"));
        Assert.False(episodes.ClaimAlert("piper-fr"));
        Assert.False(episodes.ClaimAlert("piper-fr"));
    }

    [Fact]
    public void Ending_the_episode_makes_the_next_pause_alert_again()
    {
        var episodes = new PausedEpisodes();
        episodes.ClaimAlert("piper-fr");
        episodes.End("piper-fr");
        Assert.True(episodes.ClaimAlert("piper-fr"));
    }

    [Fact]
    public void Packs_have_separate_episodes()
    {
        var episodes = new PausedEpisodes();
        episodes.ClaimAlert("piper-fr");
        Assert.True(episodes.ClaimAlert("piper-de"));
        episodes.End("piper-de");
        Assert.False(episodes.ClaimAlert("piper-fr"));
    }

    [Fact]
    public void Ending_an_episode_that_never_started_is_harmless()
    {
        var episodes = new PausedEpisodes();
        episodes.End("piper-fr");
        Assert.True(episodes.ClaimAlert("piper-fr"));
    }
}
