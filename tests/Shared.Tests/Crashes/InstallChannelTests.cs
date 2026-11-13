using ReadTheStupidText.Application.Crashes;
using ReadTheStupidText.Application.Usage;
using Xunit;

namespace ReadTheStupidText.Tests.Crashes;

public class InstallChannelTests
{
    [Theory]
    [InlineData("receipt", InstallChannel.Store)]
    [InlineData("sandboxReceipt", InstallChannel.TestFlight)]
    [InlineData(null, InstallChannel.TestFlight)]
    [InlineData("", InstallChannel.TestFlight)]
    [InlineData("Receipt", InstallChannel.TestFlight)]
    [InlineData("somethingNew", InstallChannel.TestFlight)]
    public void For_crash_reports_an_ios_receipt_named_receipt_is_the_store_and_anything_else_is_testflight(string? name, InstallChannel expected) =>
        Assert.Equal(expected, InstallChannels.FromIosReceipt(name));

    [Theory]
    [InlineData("sandboxReceipt", true)]
    [InlineData("receipt", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("somethingNew", false)]
    public void For_usage_counts_only_a_sandbox_receipt_is_not_a_store_install_exactly_as_before(string? name, bool expectedNonStore) =>
        Assert.Equal(expectedNonStore, InstallChannels.IsIosSandboxReceipt(name));

    [Theory]
    [InlineData("com.android.vending", InstallChannel.Store)]
    [InlineData(null, InstallChannel.Dev)]
    [InlineData("com.google.android.packageinstaller", InstallChannel.Dev)]
    [InlineData("com.amazon.venezia", InstallChannel.Dev)]
    public void Only_google_play_is_a_store_install_on_android(string? installer, InstallChannel expected) =>
        Assert.Equal(expected, InstallChannels.FromAndroidInstaller(installer));

    [Fact]
    public void The_names_the_worker_knows_are_store_and_testflight_and_a_dev_install_has_none()
    {
        Assert.Equal("store", InstallChannels.NameOf(InstallChannel.Store));
        Assert.Equal("testflight", InstallChannels.NameOf(InstallChannel.TestFlight));
        Assert.Null(InstallChannels.NameOf(InstallChannel.Dev));
    }
}

public class CrashEligibilityTests
{
    private static UsageFacts Real(InstallChannel channel) => new(false, "1.83.0", false, false, Channel: channel);

    [Fact]
    public void A_store_install_may_report_and_counts()
    {
        Assert.Equal(UsageSkipReason.None, CrashEligibility.Check(Real(InstallChannel.Store)));
        Assert.Equal(UsageSkipReason.None, UsageEligibility.Check(Real(InstallChannel.Store)));
    }

    [Fact]
    public void A_testflight_install_may_report_and_is_kept_out_of_usage_counts_by_the_sandbox_receipt_flag()
    {
        UsageFacts testFlight = Real(InstallChannel.TestFlight) with { IsNonStoreInstall = true };
        Assert.Equal(UsageSkipReason.None, CrashEligibility.Check(testFlight));
        Assert.Equal(UsageSkipReason.NonStoreInstall, UsageEligibility.Check(testFlight));
    }

    [Fact]
    public void An_ios_install_with_an_unreadable_receipt_reports_as_testflight_but_still_counts_as_before()
    {
        UsageFacts unreadable = Real(InstallChannels.FromIosReceipt(null)) with { IsNonStoreInstall = InstallChannels.IsIosSandboxReceipt(null) };
        Assert.Equal(UsageSkipReason.None, CrashEligibility.Check(unreadable));
        Assert.Equal(UsageSkipReason.None, UsageEligibility.Check(unreadable));
    }

    [Fact]
    public void A_dev_install_neither_reports_nor_counts_when_the_head_marks_it_non_store()
    {
        UsageFacts dev = Real(InstallChannel.Dev) with { IsNonStoreInstall = true };
        Assert.Equal(UsageSkipReason.NonStoreInstall, CrashEligibility.Check(dev));
        Assert.Equal(UsageSkipReason.NonStoreInstall, UsageEligibility.Check(dev));
    }

    [Fact]
    public void Android_dev_installs_still_count_as_before_but_do_not_report()
    {
        UsageFacts sideload = Real(InstallChannel.Dev);
        Assert.Equal(UsageSkipReason.None, UsageEligibility.Check(sideload));
        Assert.Equal(UsageSkipReason.NonStoreInstall, CrashEligibility.Check(sideload));
    }

    [Theory]
    [InlineData(InstallChannel.Store)]
    [InlineData(InstallChannel.TestFlight)]
    public void Every_build_and_device_rule_of_the_usage_gate_still_drops_reports(InstallChannel channel)
    {
        UsageFacts real = Real(channel);
        Assert.Equal(UsageSkipReason.DebugBuild, CrashEligibility.Check(real with { IsDebug = true }));
        Assert.Equal(UsageSkipReason.UnstampedBuild, CrashEligibility.Check(real with { DisplayVersion = "1.0.0" }));
        Assert.Equal(UsageSkipReason.UnstampedBuild, CrashEligibility.Check(real with { DisplayVersion = "0.9.1" }));
        Assert.Equal(UsageSkipReason.Emulator, CrashEligibility.Check(real with { IsVirtual = true }));
        Assert.Equal(UsageSkipReason.TestLab, CrashEligibility.Check(real with { IsTestLab = true }));
        Assert.Equal(UsageSkipReason.TestInstall, CrashEligibility.Check(real with { IsTestInstall = true }));
    }
}
