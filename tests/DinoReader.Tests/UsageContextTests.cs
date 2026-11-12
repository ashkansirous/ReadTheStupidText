using System.Text.Json;
using DinoReader.BookBuilder;
using DinoReader.Core;
using Xunit;

namespace DinoReader.Tests;

// Slice 351: who and when on every count. Buckets only, no id; the stored first-launch day and count never reach the JSON.
public class UsageContextTests
{
    private static readonly DateOnly Day1 = new(2026, 11, 1);

    [Theory]
    [InlineData(1, 0, "first")]
    [InlineData(2, 0, "d0")]
    [InlineData(2, 1, "d1_7")]
    [InlineData(3, 7, "d1_7")]
    [InlineData(3, 8, "d8_30")]
    [InlineData(9, 30, "d8_30")]
    [InlineData(9, 31, "d31_plus")]
    [InlineData(40, 400, "d31_plus")]
    public void InstallAge_ChangesAtTheBucketEdges(int launchNumber, int daysLater, string expected)
    {
        Assert.Equal(expected, UsageContext.InstallAgeOf(Day1, Day1.AddDays(daysLater), launchNumber));
    }

    [Fact]
    public void InstallAge_AClockThatWentBackwardsIsStillTheSameDay()
    {
        Assert.Equal("d0", UsageContext.InstallAgeOf(Day1, Day1.AddDays(-3), 4));
    }

    [Theory]
    [InlineData(0, "1")]
    [InlineData(1, "1")]
    [InlineData(2, "2_5")]
    [InlineData(5, "2_5")]
    [InlineData(6, "6_20")]
    [InlineData(20, "6_20")]
    [InlineData(21, "21_plus")]
    [InlineData(5000, "21_plus")]
    public void LaunchNo_ChangesAtTheBucketEdges(int launchNumber, string expected)
    {
        Assert.Equal(expected, UsageContext.LaunchNoOf(launchNumber));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(999, 0)]
    [InlineData(1000, 1)]
    [InlineData(95_900, 95)]
    [InlineData(86_400_000, 86_400)]
    [InlineData(999_999_999, 86_400)]
    [InlineData(-5000, 0)]
    public void SinceLaunch_IsWholeSecondsCappedAtADay(long milliseconds, int expected)
    {
        Assert.Equal(expected, UsageContext.SinceLaunchOf(TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Fact]
    public void LaunchHistory_NothingStoredIsTheFirstLaunch()
    {
        LaunchHistory.Launch launch = LaunchHistory.Begin(null, 0, Day1);

        Assert.Equal(new LaunchHistory.Launch("2026-11-01", 1, "first", "1"), launch);
    }

    [Fact]
    public void LaunchHistory_AReturningLaunchCountsOnAndKeepsTheFirstDay()
    {
        LaunchHistory.Launch launch = LaunchHistory.Begin("2026-11-01", 4, Day1.AddDays(3));

        Assert.Equal(new LaunchHistory.Launch("2026-11-01", 5, "d1_7", "2_5"), launch);
    }

    [Theory]
    [InlineData("garbage", 3)]
    [InlineData("", 3)]
    [InlineData("2026-11-01", -2)]
    public void LaunchHistory_DamagedStorageStartsOverAsAFirstLaunch(string? day, int launches)
    {
        Assert.Equal(new LaunchHistory.Launch("2026-11-03", 1, "first", "1"), LaunchHistory.Begin(day, launches, new DateOnly(2026, 11, 3)));
    }

    [Fact]
    public void UsageLaunch_SavesTheNewValuesAndStampsTheBuckets()
    {
        var store = new MemoryLaunchStore { FirstDay = "2026-11-01", Launches = 5 };
        long now = 10_000;

        var launch = new UsageLaunch(store, Day1.AddDays(40), () => now);
        now += 12_500;

        Assert.Equal("d31_plus", launch.InstallAge);
        Assert.Equal("6_20", launch.LaunchNo);
        Assert.Equal(12, launch.SinceLaunch());
        Assert.Equal(("2026-11-01", 6), (store.FirstDay, store.Launches));
    }

    [Fact]
    public void UsageLaunch_AStoreThatThrowsLeavesNoneBucketsAndSaysSo()
    {
        var launch = new UsageLaunch(new ThrowingLaunchStore(), Day1, () => 0);

        Assert.Equal("none", launch.InstallAge);
        Assert.Equal("none", launch.LaunchNo);
        Assert.True(launch.StoreFailed);
    }

    [Fact]
    public void TheContextFieldsGoOutAsCamelCaseJson_AndTheStoredDayAndCountDoNot()
    {
        var count = new UsageEvent("app_opened", "none", "en", "none", false, "1.124.0", "android", "2026-11-12", 1, UiLang: "fa", InstallAge: "first", LaunchNo: "1", SinceLaunch: 3);

        using JsonDocument json = JsonDocument.Parse(UsageBatch.ToJson([count]));
        JsonElement sent = json.RootElement.GetProperty("events")[0];

        Assert.Equal("fa", sent.GetProperty("uiLang").GetString());
        Assert.Equal("first", sent.GetProperty("installAge").GetString());
        Assert.Equal("1", sent.GetProperty("launchNo").GetString());
        Assert.Equal(3, sent.GetProperty("sinceLaunch").GetInt32());
        Assert.DoesNotContain("firstDay", json.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ACountQueuedByAnOlderBuildStillParses_AndLeavesTheFieldsOut()
    {
        var old = new UsageEvent("book_opened", "own", "en", "own", false, "1.123.0", "android", "2026-11-10", 1);

        Assert.DoesNotContain("uiLang", UsageBatch.ToJson([old]));
        UsageEvent back = UsageBatch.ParseQueue(UsageBatch.QueueToJson([old with { UiLang = "de", SinceLaunch = 7 }]))[0];

        Assert.Equal(("de", 7, null), (back.UiLang, back.SinceLaunch, back.InstallAge));
    }

    [Fact]
    public void TheWorkersClosedListsMatchTheAppsBuckets()
    {
        string events = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "worker", "events.ts"));

        foreach (string value in new[] { UsageContext.AgeFirst, UsageContext.AgeSameDay, UsageContext.AgeFirstWeek, UsageContext.AgeFirstMonth, UsageContext.AgeLater })
        {
            Assert.Contains($"\"{value}\"", events);
        }

        Assert.Contains("INSTALL_AGES = [\"first\", \"d0\", \"d1_7\", \"d8_30\", \"d31_plus\"]", events);
        Assert.Contains("LAUNCH_NUMBERS = [\"1\", \"2_5\", \"6_20\", \"21_plus\"]", events);
        string languages = string.Join(", ", UiLanguage.Supported.Select(code => $"\"{code}\""));
        Assert.Contains($"READING_LANGUAGES = [{languages}]", events); // uiLang is checked against the same fifteen codes
        Assert.Equal(86_400, UsageContext.MaxSinceLaunch);
        Assert.Contains("MAX_SINCE_LAUNCH = 86_400", events);
    }

    [Fact]
    public void ReentryGuard_SkipsANestedEntryOnTheSameThreadAndAllowsTheNextAfterDispose()
    {
        var guard = new ReentryGuard();

        using (IDisposable? outer = guard.TryEnter())
        {
            Assert.NotNull(outer);
            Assert.Null(guard.TryEnter());
        }

        using IDisposable? again = guard.TryEnter();
        Assert.NotNull(again);
    }

    private sealed class ThrowingLaunchStore : ILaunchStore
    {
        public string? FirstDay => throw new InvalidOperationException("preferences unavailable");

        public int Launches => 0;

        public void Save(string firstDay, int launches) => throw new InvalidOperationException("preferences unavailable");
    }

    private sealed class MemoryLaunchStore : ILaunchStore
    {
        public string? FirstDay { get; set; }

        public int Launches { get; set; }

        public void Save(string firstDay, int launches) => (FirstDay, Launches) = (firstDay, launches);
    }
}
