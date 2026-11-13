using System.Text.RegularExpressions;
using ReadTheStupidText.Application.Usage;

namespace ReadTheStupidText.Tests;

/// <summary>Slice 273 part A: the phone's <c>fallback</c> count, the once-per-session reporter and the Worker's matching lists.</summary>
public sealed class UsageFallbackTests
{
    private static readonly UsageContext Context = new("1.4.0", "android", 1, new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero));

    private sealed class FakeRecorder : IUsageRecorder
    {
        public List<UsageEvent> Recorded { get; } = [];

        public bool Enabled { get; set; } = true;

        public void Record(Func<UsageContext, UsageEvent?> make)
        {
            if (make(Context) is { } count)
            {
                Recorded.Add(count);
            }
        }
    }

    [Fact]
    public void A_fallback_carries_its_area_reason_count_and_language()
    {
        UsageEvent? count = UsageEvents.Fallback(Context, FallbackArea.PremadeAudio, FallbackReason.NoPack, 3, "fa-IR");

        Assert.NotNull(count);
        Assert.Equal(("fallback", "premade_audio", "no_pack", 3, "fa"), (count.Name, count.FallbackArea, count.FallbackReason, count.Count, count.Language));
    }

    [Theory]
    [InlineData("somewhere", "no_pack", 1)]
    [InlineData("premade_audio", "a book title", 1)]
    [InlineData("premade_audio", "no_pack", 0)]
    public void A_fallback_outside_the_closed_lists_or_without_a_count_is_not_made(string area, string reason, int count)
    {
        Assert.Null(UsageEvents.Fallback(Context, area, reason, count));
    }

    [Fact]
    public void The_reporter_queues_the_first_of_a_kind_at_once_and_a_repeat_once_with_the_session_total_at_the_flush()
    {
        var recorder = new FakeRecorder();
        var reporter = new FallbackReporter(recorder);

        for (int i = 0; i < 120; i++)
        {
            reporter.Report(FallbackArea.PremadeAudio, FallbackReason.Partial, "fa");
        }

        reporter.Report(FallbackArea.PremadeAudio, FallbackReason.Partial, "en");
        Assert.Equal([1, 1], recorder.Recorded.Select(e => e.Count));

        reporter.FlushRepeats();
        reporter.FlushRepeats();

        Assert.Equal([1, 1, 120], recorder.Recorded.Select(e => e.Count));
        Assert.Equal(["fa", "en", "fa"], recorder.Recorded.Select(e => e.Language));
    }

    [Fact]
    public void A_reporter_whose_recorder_throws_never_throws()
    {
        var reporter = new FallbackReporter(new ThrowingRecorder());

        reporter.Report(FallbackArea.PremadeAudio, FallbackReason.NoPack);
    }

    private sealed class ThrowingRecorder : IUsageRecorder
    {
        public bool Enabled { get; set; }

        public void Record(Func<UsageContext, UsageEvent?> make) => throw new InvalidOperationException("boom");
    }

    [Fact]
    public void The_Worker_takes_exactly_the_areas_and_reasons_the_app_can_send()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "worker", "events.ts"));

        Assert.Equal(FallbackArea.All.Order(), ListIn(source, "FALLBACK_AREAS").Order());
        Assert.Equal(FallbackReason.All.Order(), ListIn(source, "FALLBACK_REASONS").Order());
    }

    private static string[] ListIn(string source, string name)
    {
        Match list = Regex.Match(source, name + @" = \[(?<names>[^\]]*)\]");
        Assert.True(list.Success, name);
        return Regex.Matches(list.Groups["names"].Value, "\"(?<name>[a-z_:]+)\"").Select(m => m.Groups["name"].Value).ToArray();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReadTheStupidText.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repo root not found.");
    }
}
