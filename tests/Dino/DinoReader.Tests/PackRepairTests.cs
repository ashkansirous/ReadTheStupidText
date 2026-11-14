using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 360, Design: pack repair state. A missing or failed pack is tried again every hour, a new listed version at once.
public sealed class PackRepairDueTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_pack_never_tried_in_this_launch_is_due() =>
        Assert.True(PackRepairDue.IsDue(null, Now));

    [Fact]
    public void A_pack_that_failed_59_minutes_ago_is_not_due() =>
        Assert.False(PackRepairDue.IsDue(new PackRepairEntry(Now.AddMinutes(-59), PackOutcome.Failed), Now));

    [Fact]
    public void A_pack_that_failed_61_minutes_ago_is_due() =>
        Assert.True(PackRepairDue.IsDue(new PackRepairEntry(Now.AddMinutes(-61), PackOutcome.Failed), Now));

    [Fact]
    public void A_pack_that_failed_exactly_an_hour_ago_is_due() =>
        Assert.True(PackRepairDue.IsDue(new PackRepairEntry(Now.AddMinutes(-60), PackOutcome.Failed), Now));

    [Fact]
    public void A_404_then_a_new_listed_version_is_due_at_once()
    {
        var entry = new PackRepairEntry(Now.AddMinutes(-1), PackOutcome.Missing404, ListedVersion: 3);
        Assert.False(PackRepairDue.IsDue(entry, Now, listedVersion: 3));
        Assert.True(PackRepairDue.IsDue(entry, Now, listedVersion: 4));
    }

    [Fact]
    public void An_offline_try_is_due_when_the_network_comes_back_but_a_failed_one_is_not()
    {
        Assert.True(PackRepairDue.IsDue(new PackRepairEntry(Now.AddMinutes(-2), PackOutcome.Offline), Now, networkBack: true));
        Assert.False(PackRepairDue.IsDue(new PackRepairEntry(Now.AddMinutes(-2), PackOutcome.Offline), Now));
        Assert.False(PackRepairDue.IsDue(new PackRepairEntry(Now.AddMinutes(-2), PackOutcome.Failed), Now, networkBack: true));
    }
}

public sealed class PackRepairLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_recorded_try_is_remembered_by_book_and_language_ignoring_the_book_id_case()
    {
        var log = new PackRepairLog();
        log.Record("ABC", "fr", PackOutcome.Failed, Now, 2);
        Assert.Equal(PackOutcome.Failed, log.EntryOf("abc", "fr")!.LastOutcome);
        Assert.Null(log.EntryOf("abc", "fa"));
        Assert.False(log.IsDue("abc", "fr", Now.AddMinutes(10)));
        Assert.True(log.IsDue("abc", "fa", Now));
    }

    [Fact]
    public void The_tick_finds_work_only_for_a_pack_that_failed_an_hour_ago_not_for_an_ok_or_a_recent_one()
    {
        var log = new PackRepairLog();
        Assert.False(log.AnyDue(Now));
        log.Record("a", "fr", PackOutcome.Ok, Now.AddMinutes(-90), 1);
        log.Record("b", "fr", PackOutcome.Failed, Now.AddMinutes(-59), 1);
        Assert.False(log.AnyDue(Now));
        Assert.True(log.AnyDue(Now.AddMinutes(1)));
    }

    [Fact]
    public void Pruning_removes_entries_whose_pack_is_installed_or_no_longer_needed_so_they_are_never_due()
    {
        var log = new PackRepairLog();
        log.Record("Installed", "fr", PackOutcome.Failed, Now.AddMinutes(-90), 1);
        log.Record("NoListing", "fr", PackOutcome.Offline, Now.AddMinutes(-90), 1);
        log.Record("Needed", "fr", PackOutcome.Missing404, Now.AddMinutes(-90), 1);
        Assert.True(log.AnyDue(Now));
        log.Prune((book, _) => book == "Needed");
        Assert.Null(log.EntryOf("installed", "fr"));
        Assert.Null(log.EntryOf("nolisting", "fr"));
        Assert.NotNull(log.EntryOf("needed", "fr"));
        Assert.False(log.AnyOffline());
        log.Prune((_, _) => false);
        Assert.False(log.AnyDue(Now));
    }

    [Fact]
    public void A_cancelled_offline_wait_puts_the_earlier_outcome_back_or_removes_the_entry()
    {
        var log = new PackRepairLog();
        var earlier = new PackRepairEntry(Now.AddMinutes(-30), PackOutcome.Failed, 1);
        log.Record("a", "fr", PackOutcome.Offline, Now, 1);
        log.Restore("a", "fr", earlier);
        Assert.Equal(earlier, log.EntryOf("a", "fr"));
        Assert.False(log.AnyOffline());
        log.Record("b", "fr", PackOutcome.Offline, Now, 1);
        log.Restore("b", "fr", null);
        Assert.Null(log.EntryOf("b", "fr"));
        Assert.False(log.AnyOffline());
    }

    [Fact]
    public void Pruning_keeps_an_entry_that_a_try_replaced_after_the_snapshot()
    {
        var log = new PackRepairLog();
        log.Record("a", "fr", PackOutcome.Failed, Now.AddMinutes(-90), 1);
        log.Prune((_, _) =>
        {
            log.Record("a", "fr", PackOutcome.Offline, Now, 1); // a try ends while the judge runs
            return false;
        });
        Assert.Equal(PackOutcome.Offline, log.EntryOf("a", "fr")!.LastOutcome);
    }

    [Fact]
    public void An_ok_entry_is_never_counted_by_the_tick_and_asks_a_first_try_if_the_pack_is_wanted_again()
    {
        Assert.True(PackRepairDue.IsDue(new PackRepairEntry(Now.AddMinutes(-5), PackOutcome.Ok, 1), Now));
        var log = new PackRepairLog();
        log.Record("a", "fr", PackOutcome.Ok, Now.AddMinutes(-300), 1);
        Assert.False(log.AnyDue(Now));
    }

    [Fact]
    public void An_ok_entry_is_never_due_and_a_recent_offline_one_is_not_due_before_its_hour()
    {
        var log = new PackRepairLog();
        log.Record("a", "fr", PackOutcome.Ok, Now.AddMinutes(-300), 1);
        log.Record("b", "fr", PackOutcome.Offline, Now.AddMinutes(-10), 1);
        Assert.False(log.AnyDue(Now));
        Assert.True(log.AnyDue(Now.AddMinutes(50)));
    }

    [Fact]
    public void Offline_is_reported_only_while_a_pack_last_ended_offline()
    {
        var log = new PackRepairLog();
        log.Record("a", "fr", PackOutcome.Failed, Now, 1);
        Assert.False(log.AnyOffline());
        log.Record("b", "fr", PackOutcome.Offline, Now, 1);
        Assert.True(log.AnyOffline());
        log.Record("b", "fr", PackOutcome.Ok, Now, 1);
        Assert.False(log.AnyOffline());
    }

    [Fact]
    public void The_percent_is_clamped_and_forgotten_when_the_try_ends()
    {
        var log = new PackRepairLog();
        log.SetPercent("b", "fr", 140);
        Assert.Equal(100, log.PercentOf("b", "fr"));
        log.Record("b", "fr", PackOutcome.Ok, Now, 1);
        Assert.Null(log.PercentOf("b", "fr"));
    }

    [Theory]
    [InlineData(0L, 1000L, 0)]
    [InlineData(400L, 1000L, 40)]
    [InlineData(5000L, 1000L, 100)]
    [InlineData(400L, null, 0)]
    public void The_percent_of_the_bytes_is_taken_of_the_listed_size(long bytes, long? listed, int expected) =>
        Assert.Equal(expected, PackRepairLog.PercentOfBytes(bytes, listed));
}

public sealed class PackSoundViewTests
{
    [Fact]
    public void A_download_in_progress_is_state_SS1_online()
    {
        PackSoundView view = PackSoundView.For(40, online: true, PackOutcome.Failed, "en");
        Assert.Equal(PackSoundKind.Getting, view.Kind);
        Assert.Equal(40, view.Percent);
    }

    [Fact]
    public void Offline_replaces_a_stale_percent_SS2()
    {
        PackSoundView view = PackSoundView.For(0, online: false, null, "en");
        Assert.Equal(PackSoundKind.NoInternet, view.Kind);
        Assert.True(view.HasSwitch);
    }

    [Fact]
    public void No_internet_with_another_language_on_the_phone_has_the_button_SS2()
    {
        PackSoundView view = PackSoundView.For(null, online: false, null, "en");
        Assert.Equal(PackSoundKind.NoInternet, view.Kind);
        Assert.True(view.HasSwitch);
    }

    [Fact]
    public void No_internet_with_nothing_to_switch_to_has_no_button_SS4()
    {
        PackSoundView view = PackSoundView.For(null, online: false, null, null);
        Assert.Equal(PackSoundKind.NoInternet, view.Kind);
        Assert.False(view.HasSwitch);
    }

    [Theory]
    [InlineData(PackOutcome.Failed)]
    [InlineData(PackOutcome.Missing404)]
    public void A_failed_or_missing_pack_online_is_SS3_with_the_button_when_there_is_one(PackOutcome outcome)
    {
        Assert.True(PackSoundView.For(null, online: true, outcome, "en").HasSwitch);
        PackSoundView alone = PackSoundView.For(null, online: true, outcome, null);
        Assert.Equal(PackSoundKind.Failed, alone.Kind);
        Assert.False(alone.HasSwitch);
    }

    [Fact]
    public void An_expected_missing_pack_online_with_no_try_yet_is_SS1_at_zero_percent()
    {
        PackSoundView view = PackSoundView.For(null, online: true, null, "en");
        Assert.Equal(PackSoundKind.Getting, view.Kind);
        Assert.Equal(0, view.Percent);
    }

    [Fact]
    public void An_installed_or_not_expected_pack_says_nothing_whatever_the_state()
    {
        Assert.Equal(PackSoundKind.None, PackSoundView.ForPack(PackExpectationKind.Expected, installed: true, null, online: false, PackOutcome.Failed, "en").Kind);
        Assert.Equal(PackSoundKind.None, PackSoundView.ForPack(PackExpectationKind.NotExpected, installed: false, 40, online: false, PackOutcome.Failed, "en").Kind);
        Assert.Equal(PackSoundKind.NoInternet, PackSoundView.ForPack(PackExpectationKind.Expected, installed: false, null, online: false, null, null).Kind);
    }

    [Fact]
    public void The_other_language_is_the_first_installed_one_that_is_not_the_reading_language()
    {
        string[] family = ["fa", "en", "fr"];
        Assert.Equal("fr", PackSoundView.OtherInstalledLanguage(family, "fa", l => l == "fr"));
        Assert.Null(PackSoundView.OtherInstalledLanguage(family, "fa", l => l == "fa"));
    }
}
