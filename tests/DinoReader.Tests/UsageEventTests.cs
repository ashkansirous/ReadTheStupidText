using DinoReader.Core;

namespace DinoReader.Tests;

public class UsageEventTests
{
    private const string PackageId = "0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 23, 59, 0, TimeSpan.FromHours(2));

    private static Story StoryWith(string? sourceId, string language = "en") =>
        new("local-id", "A private title", language, Now, [], [], CoverStyle.FirstPage, 0, string.Empty, SourceId: sourceId);

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef")]
    [InlineData("01234567-89AB-CDEF-0123-456789ABCDEF")]
    public void A_shelf_book_is_counted_by_its_package_id(string sourceId)
    {
        UsageEvent count = UsageEvent.For(UsageEventName.BookOpened, StoryWith(sourceId, "nb"), true, 0.5, "1.42.0", AppPlatform.Android, Now);

        Assert.Equal(new UsageEvent("book_opened", PackageId, "nb", "shelf", false, "1.42.0", "android", "2026-10-25", 0.5), count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-id")]
    public void An_own_book_is_counted_as_own_with_no_id_or_title(string? sourceId)
    {
        UsageEvent count = UsageEvent.For(UsageEventName.BookFinished, StoryWith(sourceId), true, 1, "1.0", AppPlatform.Windows, Now);

        Assert.Equal("own", count.Book);
        Assert.Equal("own", count.Source);
        Assert.DoesNotContain("private", UsageBatch.ToJson([count]), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("local-id", UsageBatch.ToJson([count]), StringComparison.Ordinal);
    }

    [Fact]
    public void A_book_with_an_id_that_is_not_from_the_starters_or_the_store_is_counted_as_own()
    {
        UsageEvent count = UsageEvent.For(UsageEventName.BookOpened, StoryWith(PackageId), false, 1, "1.0", AppPlatform.Android, Now);

        Assert.Equal("own", count.Book);
        Assert.Equal("own", count.Source);
        Assert.DoesNotContain(PackageId, UsageBatch.ToJson([count]), StringComparison.Ordinal);
    }

    [Fact]
    public void The_day_is_the_utc_date_and_nothing_finer()
    {
        UsageEvent count = UsageEvent.For(UsageEventName.BookOpened, StoryWith(null), false, 1, "1.0", AppPlatform.Ios, new DateTimeOffset(2026, 10, 26, 0, 30, 0, TimeSpan.FromHours(2)));

        Assert.Equal("2026-10-25", count.Day);
    }

    [Fact]
    public void The_batch_body_carries_only_the_named_fields_in_camel_case()
    {
        UsageEvent count = UsageEvent.For(UsageEventName.BookOpened, StoryWith(PackageId), true, 0.5, "1.42.0", AppPlatform.Android, Now);

        string json = UsageBatch.ToJson([count]);

        Assert.Equal(
            """{"schemaVersion":1,"events":[{"name":"book_opened","book":"0123456789abcdef0123456789abcdef","language":"en","source":"shelf","hasRecording":false,"version":"1.42.0","platform":"android","day":"2026-10-25","sampleRate":0.5}]}""",
            json);
    }

    [Fact]
    public void A_stored_queue_round_trips_and_a_damaged_one_is_empty()
    {
        UsageEvent count = UsageEvent.For(UsageEventName.BookFinished, StoryWith(PackageId), true, 0.5, "1.42.0", AppPlatform.Android, Now);

        Assert.Equal([count], UsageBatch.ParseQueue(UsageBatch.QueueToJson([count])));
        Assert.Empty(UsageBatch.ParseQueue("{broken"));
        Assert.Empty(UsageBatch.ParseQueue(null));
    }

    [Theory]
    [InlineData(true, 0.25, 0.10, true)]
    [InlineData(true, 0.25, 0.25, false)]
    [InlineData(true, 1.0, 0.999, true)]
    [InlineData(true, 0.0, 0.0, false)]
    [InlineData(false, 1.0, 0.0, false)]
    public void A_launch_is_counted_only_when_telemetry_is_on_and_the_roll_is_under_the_rate(bool enabled, double rate, double roll, bool expected) =>
        Assert.Equal(expected, UsageBatch.Samples(new TelemetryConfig(enabled, rate), roll));

    [Fact]
    public void The_default_config_counts_nothing() => Assert.False(UsageBatch.Samples(AppConfig.Default.Telemetry, 0));
}
