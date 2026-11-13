using System.Text.Json;
using System.Text.RegularExpressions;
using DinoReader.BookBuilder;
using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 352: the closed lists of the screen, tap, setting and read-origin counts, and that the app and the Worker keep them equal.
public sealed class UiUsageTests
{
    [Fact]
    public void The_Worker_lists_equal_the_apps_lists()
    {
        string source = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "worker", "events.ts"));

        Assert.Equal(UiScreens.All.Order(), ListIn(source, "SCREENS").Order());
        Assert.Equal(UiScreenActions.All.Order(), ListIn(source, "SCREEN_ACTIONS").Order());
        Assert.Equal(UiControls.All.Order(), ListIn(source, "CONTROLS").Order());
        Assert.Equal(UiSettings.Values.Keys.Order(), ListIn(source, "SETTINGS").Order());
        Assert.Equal(UiSettings.SpeedBuckets.Order(), ListIn(source, "SPEED_BUCKETS").Order());
        Assert.Equal(UiSettings.Modes.Order(), ListIn(source, "READING_MODES").Order());
        Assert.Equal(new[] { UiSettings.On, UiSettings.Off }.Order(), ListIn(source, "SETTING_SWITCH").Order());
        Assert.Equal(ReadOrigins.All.Order(), ListIn(source, "READ_ORIGINS").Order());
        Assert.Contains($"NO_SCREEN = \"{UiScreens.None}\"", source);
    }

    [Fact]
    public void The_Worker_accepts_every_new_event_name()
    {
        string source = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "worker", "events.ts"));

        foreach (string name in new[] { UsageEventName.Screen, UsageEventName.Tap, UsageEventName.SettingChanged, UsageEventName.AppBackgrounded, UsageEventName.AppResumed })
        {
            Assert.Matches($@"\n  {name}: \{{ sources: \[NO_BOOK\]", source);
        }
    }

    [Fact]
    public void Every_list_is_lower_case_snake_case_with_no_duplicates_and_no_free_text()
    {
        foreach (IReadOnlyList<string> list in new[] { UiScreens.All, UiScreenActions.All, UiControls.All, UiSettings.SpeedBuckets, UiSettings.Modes, ReadOrigins.All, [.. UiSettings.Values.Keys] })
        {
            Assert.Equal(list.Count, list.Distinct().Count());
            Assert.All(list, value => Assert.Matches("^[a-z][a-z0-9_]*$", value));
        }
    }

    [Fact]
    public void The_wizard_has_one_screen_per_step_and_none_outside_them()
    {
        Assert.Equal(IntroSteps.Count, Enumerable.Range(0, 10).Count(step => UiScreens.IntroOf(step) is not null));
        Assert.Equal(["intro_1", "intro_2", "intro_3", "intro_4"], Enumerable.Range(0, IntroSteps.Count).Select(step => UiScreens.IntroOf(step)!));
        Assert.Null(UiScreens.IntroOf(-1));
        Assert.All(Enumerable.Range(0, IntroSteps.Count), step => Assert.Contains(UiScreens.IntroOf(step)!, UiScreens.All));
    }

    [Fact]
    public void Details_join_two_closed_halves_with_a_colon()
    {
        Assert.Equal("intro_2:next", UiUsageDetail.Screen(UiScreens.Intro2, UiScreenActions.Next));
        Assert.Equal("shelf:book", UiUsageDetail.Tap(UiScreens.Shelf, UiControls.Book));
        Assert.Equal("reading_speed:fast", UiUsageDetail.Setting(UiSettings.ReadingSpeed, "fast"));
    }

    [Theory]
    [InlineData(0.8, "slow")]
    [InlineData(0.9, "slow")]
    [InlineData(1.0, "normal")]
    [InlineData(1.05, "normal")]
    [InlineData(1.1, "fast")]
    [InlineData(1.2, "fast")]
    public void A_speed_is_counted_as_a_bucket_never_a_number(double rate, string expected)
    {
        string bucket = UiSettings.SpeedBucketOf(rate);

        Assert.Equal(expected, bucket);
        Assert.Contains(bucket, UiSettings.Values[UiSettings.ReadingSpeed]);
    }

    [Fact]
    public void Every_reading_mode_has_a_counted_name_on_the_list()
    {
        foreach (ReadingMode mode in Enum.GetValues<ReadingMode>())
        {
            Assert.Contains(UiSettings.ModeOf(mode), UiSettings.Values[UiSettings.ModeSetting]);
        }

        Assert.Equal(3, UiSettings.Modes.Count);
        Assert.Equal(Enum.GetValues<ReadingMode>().Length, UiSettings.Modes.Count);
    }

    [Fact]
    public void The_app_language_setting_takes_exactly_the_fifteen_app_languages()
    {
        Assert.Equal(UiLanguage.Supported, UiSettings.Values[UiSettings.AppLanguage]);
        Assert.Equal(15, UiSettings.Values[UiSettings.AppLanguage].Count);
    }

    [Fact]
    public void The_queue_sends_at_forty_counts_so_a_batch_never_meets_the_Workers_cap()
    {
        Assert.Equal(40, UsageBatch.FlushAt);
        Assert.True(UsageBatch.FlushAt < UsageBatch.MaxEvents);

        string json = UsageBatch.ToJson(Enumerable.Range(0, UsageBatch.FlushAt).Select(i => Count("tap", "reading:play") with { Seq = i + 1, Session = new string('a', 32), Screen = UiScreens.Reading }).ToList());

        Assert.True(json.Length < 32 * 1024, $"{UsageBatch.FlushAt} counts are {json.Length} bytes");
    }

    [Fact]
    public void The_screen_is_sent_as_a_field_and_left_out_of_an_older_counts_json()
    {
        UsageEvent with = Count("tap", "reading:play") with { Screen = UiScreens.Reading };

        using JsonDocument body = JsonDocument.Parse(UsageBatch.ToJson([with]));
        JsonElement sent = body.RootElement.GetProperty("events")[0];
        Assert.Equal("reading", sent.GetProperty("screen").GetString());
        Assert.False(JsonDocument.Parse(UsageBatch.ToJson([Count("tap", "reading:play")])).RootElement.GetProperty("events")[0].TryGetProperty("screen", out _));
        Assert.Equal("reading", UsageBatch.ParseQueue(UsageBatch.QueueToJson([with]))[0].Screen);
    }

    [Fact]
    public void A_read_says_where_it_started_from_the_shelf_the_corner_or_the_wizard()
    {
        string app = Path.Combine(StarterBuilder.RepoRoot(), "src", "DinoReader.App");
        string reading = File.ReadAllText(Path.Combine(app, "ReadingPage.xaml.cs"));

        Assert.Contains("name == UsageEventName.ReadStarted ? ReadOrigin", reading);
        Assert.Contains("RecordBookCount(UsageEventName.BookOpened, story, ReadOrigin)", reading);
        Assert.Contains("string origin = ReadOrigins.Shelf", File.ReadAllText(Path.Combine(app, "StoryActions.cs")));
        Assert.Contains("_actions.ReadAsync(Navigation, story, ReadOrigins.Corner)", File.ReadAllText(Path.Combine(app, "GalleryPage.xaml.cs")));    }

    [Fact]
    public void The_launch_screen_is_claimed_once_when_no_shown_was_recorded()
    {
        var once = new ShownCountedThisLaunch();
        Assert.True(once.TryClaim());
        Assert.False(once.TryClaim());
    }

    [Fact]
    public void The_launch_screen_is_not_claimed_after_a_shown_was_recorded()
    {
        var once = new ShownCountedThisLaunch();
        once.Recorded();
        Assert.False(once.TryClaim());
    }

    [Fact]
    public void A_closed_modal_gives_the_screen_back_to_the_page_beneath()
    {
        var tracker = new UiScreenTracker();
        var gate = new object();
        tracker.Show(UiScreens.Shelf);
        tracker.Show(UiScreens.Gate, gate);
        Assert.Equal(UiScreens.Gate, tracker.Current);

        Assert.Equal(UiScreens.Shelf, tracker.Restore(gate));
        Assert.Equal(UiScreens.Shelf, tracker.Current);
    }

    [Fact]
    public void A_modal_that_did_not_show_the_current_screen_restores_nothing()
    {
        var tracker = new UiScreenTracker();
        var gate = new object();
        var other = new object();
        tracker.Show(UiScreens.Shelf);
        tracker.Show(UiScreens.Gate, gate);

        Assert.Null(tracker.Restore(other));
        Assert.Null(tracker.Restore(null));
        Assert.Equal(UiScreens.Gate, tracker.Current);
        Assert.Equal(UiScreens.Shelf, tracker.Restore(gate));
    }

    [Fact]
    public void Nothing_is_restored_when_the_page_beneath_already_showed_itself()
    {
        var tracker = new UiScreenTracker();
        var close = new object();
        tracker.Show(UiScreens.Shelf);
        tracker.Show(UiScreens.CloseQuestion, close);
        tracker.Show(UiScreens.Shelf);

        Assert.Null(tracker.Restore(close));
        Assert.Equal(UiScreens.Shelf, tracker.Current);
    }

    [Fact]
    public void Wizard_steps_share_one_place_beneath_and_a_nested_modal_returns_to_its_step()
    {
        var tracker = new UiScreenTracker();
        var wizard = new object();
        var close = new object();
        tracker.Show(UiScreens.Shelf);
        tracker.Show(UiScreens.Intro1, wizard);
        tracker.Show(UiScreens.Intro2, wizard);
        tracker.Show(UiScreens.CloseQuestion, close);

        Assert.Equal(UiScreens.Intro2, tracker.Restore(close));
        Assert.Equal(UiScreens.Shelf, tracker.Restore(wizard));
        Assert.Null(tracker.Restore(wizard));
    }

    [Fact]
    public void Showing_the_same_screen_again_is_no_change()
    {
        var tracker = new UiScreenTracker();
        Assert.True(tracker.Show(UiScreens.Shelf));
        Assert.False(tracker.Show(UiScreens.Shelf));
    }

    [Fact]
    public void A_resume_is_counted_only_after_a_counted_background()
    {
        var lifecycle = new UiLifecycle();
        Assert.False(lifecycle.Resumed()); // the cold-start resume

        lifecycle.Backgrounded();
        Assert.True(lifecycle.Resumed());
        Assert.False(lifecycle.Resumed());
    }

    [Theory]
    [InlineData(1.0, 1.05, false)]
    [InlineData(1.0, 1.1, true)]
    [InlineData(0.9, 0.5, false)]
    [InlineData(0.9, 1.0, true)]
    public void A_speed_change_inside_one_bucket_is_not_a_changed_setting(double before, double after, bool changed)
    {
        Assert.Equal(changed, UiSettings.SpeedBucketChanged(before, after));
    }

    private static UsageEvent Count(string name, string detail) =>
        new(name, "none", "und", "none", false, "1.200.0", "android", "2026-11-12", 1, detail);

    private static string[] ListIn(string source, string name)
    {
        Match list = Regex.Match(source, name + @" = \[(?<names>[^\]]*)\]");
        Assert.True(list.Success, name);
        return Regex.Matches(list.Groups["names"].Value, "\"(?<name>[a-z0-9_:]+)\"").Select(m => m.Groups["name"].Value).ToArray();
    }
}
