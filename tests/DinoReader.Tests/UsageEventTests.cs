using System.Text.RegularExpressions;
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
            """{"schemaVersion":1,"events":[{"name":"book_opened","book":"0123456789abcdef0123456789abcdef","language":"en","source":"shelf","hasRecording":false,"version":"1.42.0","platform":"android","day":"2026-10-25","sampleRate":0.5,"detail":""}]}""",
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

    [Theory]
    [InlineData(true, true, "age+author")]
    [InlineData(true, false, "age")]
    [InlineData(false, true, "author")]
    [InlineData(false, false, "none")]
    public void Store_filters_are_counted_as_set_or_not_never_by_value(bool age, bool author, string expected) =>
        Assert.Equal(expected, UsageDetail.StoreFilters(age, author));

    [Theory]
    [InlineData(0, "1")]
    [InlineData(1, "1")]
    [InlineData(2, "2-5")]
    [InlineData(5, "2-5")]
    [InlineData(6, "6-10")]
    [InlineData(10, "6-10")]
    [InlineData(11, "11+")]
    [InlineData(40, "11+")]
    public void Recorded_pages_are_a_bucket_not_a_count(int recorded, string expected) =>
        Assert.Equal(expected, UsageDetail.PagesBucket(recorded));

    [Fact]
    public void A_store_download_is_counted_by_package_id_language_and_filters()
    {
        UsageEvent? count = UsageEvent.ForShelfBook(UsageEventName.StoreDownload, "01234567-89AB-CDEF-0123-456789ABCDEF", "nb", UsageDetail.StoreAge, 1, "1.0", AppPlatform.Android, Now);

        Assert.Equal(new UsageEvent("store_download", PackageId, "nb", "shelf", false, "1.0", "android", "2026-10-25", 1, "age"), count);
        Assert.Null(UsageEvent.ForShelfBook(UsageEventName.StoreDownload, "My Story", "en", UsageDetail.StoreNoFilter, 1, "1.0", AppPlatform.Android, Now));
    }

    [Fact]
    public void A_count_about_no_book_names_none_and_carries_only_its_detail()
    {
        UsageEvent count = UsageEvent.ForNoBook(UsageEventName.ReadingLanguageChanged, "nb", "en", 0.5, "1.0", AppPlatform.Ios, Now);

        Assert.Equal(new UsageEvent("reading_language_changed", "none", "nb", "none", false, "1.0", "ios", "2026-10-25", 0.5, "en"), count);
    }

    [Fact]
    public void A_recording_count_keeps_the_book_rule_and_adds_the_bucket()
    {
        UsageEvent shelf = UsageEvent.For(UsageEventName.RecordingMade, StoryWith(PackageId), true, 1, "1.0", AppPlatform.Android, Now, UsageDetail.PagesTwoToFive);
        UsageEvent own = UsageEvent.For(UsageEventName.RecordingMade, StoryWith(PackageId), false, 1, "1.0", AppPlatform.Android, Now, UsageDetail.PagesOne);

        Assert.Equal((PackageId, "2-5"), (shelf.Book, shelf.Detail));
        Assert.Equal(("own", "1"), (own.Book, own.Detail));
    }

    [Fact]
    public void A_queue_stored_before_Slice_134_reads_with_an_empty_detail()
    {
        const string old = """[{"name":"book_opened","book":"own","language":"en","source":"own","hasRecording":false,"version":"1.0","platform":"android","day":"2026-10-25","sampleRate":1}]""";

        Assert.Equal(string.Empty, Assert.Single(UsageBatch.ParseQueue(old)).Detail);
    }

    [Fact]
    public void The_Worker_lists_exactly_the_apps_supported_reading_languages()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "worker", "events.ts"));
        Match list = Regex.Match(source, @"READING_LANGUAGES = \[(?<codes>[^\]]*)\]");

        Assert.True(list.Success);
        string[] inWorker = Regex.Matches(list.Groups["codes"].Value, "\"(?<code>[a-z]+)\"").Select(m => m.Groups["code"].Value).ToArray();
        Assert.Equal(UiLanguage.Supported.Order(), inWorker.Order());
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReadTheStupidText.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repo root (ReadTheStupidText.slnx) not found.");
    }
}
