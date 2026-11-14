using Sirous.Reading.Application.Crashes;
using Sirous.Reading.Application.Usage;
using Xunit;

namespace ReadTheStupidText.Tests;

/// <summary>Slice 251: the closed usage channel. RTST and crash reports keep main's yes/no for every input; only Dino maps Beta to sending.</summary>
public class UsageChannelTests
{
    private static UsageFacts Real() => new(false, "1.94.0", false, false);

    private static UsageFacts TestFlight() => Real() with { IsNonStoreInstall = true, Channel = InstallChannel.TestFlight };

    [Fact]
    public void AStoreInstallIsProduction() =>
        Assert.Equal(UsageChannel.Production, UsageEligibility.ChannelOf(Real()));

    [Fact]
    public void ATestFlightInstallIsBeta() =>
        Assert.Equal(UsageChannel.Beta, UsageEligibility.ChannelOf(TestFlight()));

    [Theory]
    [InlineData("debug")]
    [InlineData("unstamped")]
    [InlineData("virtual")]
    [InlineData("testlab")]
    [InlineData("harness")]
    public void EveryOtherBlockStaysABlockOnTestFlight(string block)
    {
        UsageFacts facts = block switch
        {
            "debug" => TestFlight() with { IsDebug = true },
            "unstamped" => TestFlight() with { DisplayVersion = "1.0" },
            "virtual" => TestFlight() with { IsVirtual = true },
            "testlab" => TestFlight() with { IsTestLab = true },
            _ => TestFlight() with { IsTestInstall = true },
        };

        Assert.Equal(UsageChannel.None, UsageEligibility.ChannelOf(facts));
    }

    [Fact]
    public void ANonStoreWindowsInstallIsNotBeta() =>
        Assert.Equal(UsageChannel.None, UsageEligibility.ChannelOf(Real() with { IsNonStoreInstall = true, Channel = InstallChannel.Dev }));

    [Theory]
    [InlineData("receipt", UsageChannel.Production)]
    [InlineData("sandboxReceipt", UsageChannel.Beta)]
    [InlineData(null, UsageChannel.Production)] // today a missing receipt still counts as a store install (Slice 179)
    [InlineData("somethingNew", UsageChannel.Production)]
    public void AnIosReceiptNameChoosesTheChannel(string? receipt, UsageChannel expected)
    {
        UsageFacts facts = Real() with { IsNonStoreInstall = InstallChannels.IsIosSandboxReceipt(receipt), Channel = InstallChannels.FromIosReceipt(receipt) };

        Assert.Equal(expected, UsageEligibility.ChannelOf(facts));
    }

    [Theory]
    [InlineData(UsageChannel.Production, true, "/api/events")]
    [InlineData(UsageChannel.Production, false, "/api/events")]
    [InlineData(UsageChannel.Beta, true, "/api/events/beta")]
    [InlineData(UsageChannel.Beta, false, null)]
    [InlineData(UsageChannel.None, true, null)]
    [InlineData(UsageChannel.None, false, null)]
    public void TheSenderPostsToTheRouteOfItsChannelAndOnlyBetaAppsPostBeta(UsageChannel channel, bool sendsBeta, string? expected) =>
        Assert.Equal(expected, UsageChannels.PathFor("/api/events", channel, sendsBeta));

    [Fact]
    public void Beta_is_the_only_channel_that_needs_a_block_to_be_lifted_and_every_beta_is_a_blocked_usage_check()
    {
        foreach (UsageFacts facts in AllFacts())
        {
            if (UsageEligibility.ChannelOf(facts) == UsageChannel.Beta)
            {
                Assert.Equal(UsageSkipReason.NonStoreInstall, UsageEligibility.Check(facts));
            }
        }
    }

    [Fact]
    public void RtstAndCrashResultsAreMainsForEveryInput()
    {
        foreach (UsageFacts facts in AllFacts())
        {
            Assert.Equal(MainUsageCheck(facts), UsageEligibility.Check(facts));
            Assert.Equal(MainCrashCheck(facts), CrashEligibility.Check(facts));
            Assert.Equal(UsageEligibility.ChannelOf(facts) == UsageChannel.Production, UsageEligibility.Counts(facts));
        }
    }

    // main's rules restated from before Slice 251: order debug, unstamped, emulator, test lab, test install, then the install rule of each caller.
    private static UsageSkipReason MainBuild(UsageFacts f) =>
        f.IsDebug ? UsageSkipReason.DebugBuild
        : f.DisplayVersion is "1.0" or "0.0.0" or "" ? UsageSkipReason.UnstampedBuild
        : f.IsVirtual ? UsageSkipReason.Emulator
        : f.IsTestLab ? UsageSkipReason.TestLab
        : f.IsTestInstall ? UsageSkipReason.TestInstall
        : UsageSkipReason.None;

    private static UsageSkipReason MainUsageCheck(UsageFacts f) =>
        MainBuild(f) != UsageSkipReason.None ? MainBuild(f) : f.IsNonStoreInstall ? UsageSkipReason.NonStoreInstall : UsageSkipReason.None;

    private static UsageSkipReason MainCrashCheck(UsageFacts f) =>
        MainBuild(f) != UsageSkipReason.None ? MainBuild(f) : f.Channel == InstallChannel.Dev ? UsageSkipReason.NonStoreInstall : UsageSkipReason.None;

    private static IEnumerable<UsageFacts> AllFacts()
    {
        bool[] flags = [false, true];
        foreach (string version in new[] { "1.94.0", "1.0", "0.0.0", "" })
        foreach (bool debug in flags)
        foreach (bool virt in flags)
        foreach (bool lab in flags)
        foreach (bool harness in flags)
        foreach (bool nonStore in flags)
        foreach (InstallChannel channel in Enum.GetValues<InstallChannel>())
        {
            yield return new UsageFacts(debug, version, virt, lab, harness, nonStore, channel);
        }
    }
}
