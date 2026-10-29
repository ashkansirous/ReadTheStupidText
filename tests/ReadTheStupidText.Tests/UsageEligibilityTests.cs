using ReadTheStupidText.Application.Usage;
using Xunit;

namespace ReadTheStupidText.Tests;

public class UsageEligibilityTests
{
    private static UsageFacts Real() => new(false, "1.71.1", false, false);

    [Fact]
    public void AllClearCounts()
    {
        Assert.Equal(UsageSkipReason.None, UsageEligibility.Check(Real()));
        Assert.True(UsageEligibility.Counts(Real()));
    }

    [Fact]
    public void DebugDoesNotCount() =>
        Assert.Equal(UsageSkipReason.DebugBuild, UsageEligibility.Check(Real() with { IsDebug = true }));

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.0.0")]
    [InlineData("1.0.0.0")]
    [InlineData("0.0.0")]
    [InlineData("0.0.0.0")]
    [InlineData("")]
    [InlineData(" 1.0 ")]
    public void UnstampedDoesNotCount(string version) =>
        Assert.Equal(UsageSkipReason.UnstampedBuild, UsageEligibility.Check(Real() with { DisplayVersion = version }));

    [Theory]
    [InlineData("1.0.1")]
    [InlineData("1.71.1")]
    [InlineData("2.0")]
    public void StampedVersionCounts(string version) =>
        Assert.True(UsageEligibility.Counts(Real() with { DisplayVersion = version }));

    [Fact]
    public void EmulatorDoesNotCount() =>
        Assert.Equal(UsageSkipReason.Emulator, UsageEligibility.Check(Real() with { IsVirtual = true }));

    [Fact]
    public void TestLabDoesNotCount() =>
        Assert.Equal(UsageSkipReason.TestLab, UsageEligibility.Check(Real() with { IsTestLab = true }));

    [Fact]
    public void TestInstallDoesNotCount() =>
        Assert.Equal(UsageSkipReason.TestInstall, UsageEligibility.Check(Real() with { IsTestInstall = true }));

    [Fact]
    public void NonStoreInstallDoesNotCount() =>
        Assert.Equal(UsageSkipReason.NonStoreInstall, UsageEligibility.Check(Real() with { IsNonStoreInstall = true }));

    [Fact]
    public void TestInstallBeatsNonStoreInstallAndTestLabBeatsTestInstall()
    {
        Assert.Equal(UsageSkipReason.TestInstall, UsageEligibility.Check(Real() with { IsTestInstall = true, IsNonStoreInstall = true }));
        Assert.Equal(UsageSkipReason.TestLab, UsageEligibility.Check(Real() with { IsTestLab = true, IsTestInstall = true }));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData(" 1 ", true)]
    [InlineData("0", false)]
    [InlineData("true", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyOneSwitchesTheNoUsageVariable(string? value, bool expected) =>
        Assert.Equal(expected, UsageEligibility.IsNoUsageValue(value));

    [Fact]
    public void SeveralAtOnceReportTheFirstRule()
    {
        Assert.Equal(UsageSkipReason.DebugBuild, UsageEligibility.Check(new UsageFacts(true, "1.0", true, true)));
        Assert.Equal(UsageSkipReason.UnstampedBuild, UsageEligibility.Check(new UsageFacts(false, "1.0", true, true)));
        Assert.Equal(UsageSkipReason.Emulator, UsageEligibility.Check(new UsageFacts(false, "1.5.0", true, true)));
    }

    [Theory]
    [InlineData("google/sdk_gphone64_x86_64/emu:14/UE1A/1:userdebug/dev-keys", "ranchu", "Pixel 8", "x")]
    [InlineData("generic/vbox86p/vbox86p:9/PI/1:userdebug/test-keys", "x", "Pixel 8", "x")]
    [InlineData("google/oriole/oriole:14/AP1/1:user/release-keys", "goldfish", "Pixel 6", "oriole")]
    [InlineData("google/oriole/oriole:14/AP1/1:user/release-keys", "tensor", "Android SDK built for x86", "oriole")]
    [InlineData("google/oriole/oriole:14/AP1/1:user/release-keys", "tensor", "Pixel 6", "sdk_gphone")]
    public void EmulatorAndRobotBuildStringsAreDetected(string fingerprint, string hardware, string model, string product) =>
        Assert.True(UsageEligibility.IsEmulatorBuild(fingerprint, hardware, model, product));

    [Theory]
    [InlineData("samsung/dm1qxx/dm1q:14/UP1A/1:user/release-keys", "qcom", "SM-S911B", "dm1qxx")]
    [InlineData("google/oriole/oriole:14/AP1/1:user/release-keys", "tensor", "Pixel 6", "oriole")]
    [InlineData("lineage/lineage_dm1q/dm1q:14/UP1A/1:userdebug/test-keys", "qcom", "SM-S911B", "lineage_dm1q")]
    [InlineData(null, null, null, null)]
    public void RealPhoneBuildStringsAreNotEmulators(string? fingerprint, string? hardware, string? model, string? product) =>
        Assert.False(UsageEligibility.IsEmulatorBuild(fingerprint, hardware, model, product));
}
