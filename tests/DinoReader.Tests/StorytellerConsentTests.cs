using Xunit;

namespace DinoReader.Tests;

/// <summary>The "Download now" tap is consent the run reads, and the guard is released with the consent check in one step (Slice 197).</summary>
public class StorytellerConsentTests
{
    [Fact]
    public void TapWithNoRunInFlight_StartsARun()
    {
        var consent = new StorytellerConsent();

        Assert.True(consent.RecordConsentAndTryBegin());
        Assert.True(consent.IsGiven);
        Assert.True(consent.IsActive);
    }

    [Fact]
    public void TapBeforeTheRunEnds_RunsAgainAndKeepsTheGuard()
    {
        var consent = new StorytellerConsent();
        Assert.True(consent.TryBegin());

        Assert.False(consent.RecordConsentAndTryBegin());

        Assert.True(consent.EndRun(endedWaitingForWifi: true, packInstalled: false));
        Assert.True(consent.IsActive);
        Assert.False(consent.EndRun(endedWaitingForWifi: false, packInstalled: false));
        Assert.False(consent.IsActive);
    }

    [Fact]
    public void TapAfterTheRunEnds_StartsItsOwnRun()
    {
        var consent = new StorytellerConsent();
        Assert.True(consent.TryBegin());
        Assert.False(consent.EndRun(endedWaitingForWifi: true, packInstalled: false));

        Assert.True(consent.RecordConsentAndTryBegin());
    }

    [Fact]
    public void TapDuringTheEnd_IsNeverDropped()
    {
        for (int i = 0; i < 2000; i++)
        {
            var consent = new StorytellerConsent();
            consent.TryBegin();
            bool tapStarted = false;
            bool endLoops = false;
            using var go = new ManualResetEventSlim();
            var tap = Task.Run(() => { go.Wait(); tapStarted = consent.RecordConsentAndTryBegin(); });
            var end = Task.Run(() => { go.Wait(); endLoops = consent.EndRun(endedWaitingForWifi: true, packInstalled: false); });
            go.Set();
            Task.WaitAll(tap, end);

            Assert.True(tapStarted ^ endLoops);
        }
    }

    [Fact]
    public void WifiArrivingAfterTheTap_RunEndsDownloading_ReleasesTheGuard()
    {
        var consent = new StorytellerConsent();
        consent.RecordConsentAndTryBegin();

        Assert.False(consent.EndRun(endedWaitingForWifi: false, packInstalled: false));
        Assert.False(consent.IsActive);
    }

    [Fact]
    public void NoConsent_NoSecondRun()
    {
        var consent = new StorytellerConsent();
        consent.TryBegin();

        Assert.False(consent.EndRun(endedWaitingForWifi: true, packInstalled: false));
    }

    [Fact]
    public void ConsentSurvivesAFailedRun_ButNotAnInstalledPack()
    {
        var consent = new StorytellerConsent();
        consent.RecordConsentAndTryBegin();
        consent.EndRun(endedWaitingForWifi: false, packInstalled: false);
        Assert.True(consent.IsGiven);

        consent.TryBegin();
        consent.EndRun(endedWaitingForWifi: false, packInstalled: true);
        Assert.False(consent.IsGiven);
    }

    [Fact]
    public void Cancel_ClearsTheConsent()
    {
        var consent = new StorytellerConsent();
        consent.RecordConsentAndTryBegin();

        consent.Clear();

        Assert.False(consent.IsGiven);
    }
}
