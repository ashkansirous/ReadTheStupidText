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
    public void SeveralAtOnceReportTheFirstRule()
    {
        Assert.Equal(UsageSkipReason.DebugBuild, UsageEligibility.Check(new UsageFacts(true, "1.0", true, true)));
        Assert.Equal(UsageSkipReason.UnstampedBuild, UsageEligibility.Check(new UsageFacts(false, "1.0", true, true)));
        Assert.Equal(UsageSkipReason.Emulator, UsageEligibility.Check(new UsageFacts(false, "1.5.0", true, true)));
    }
}
